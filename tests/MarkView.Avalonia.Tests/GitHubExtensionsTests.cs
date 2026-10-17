// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using Markdig;
using Markdig.Parsers.Inlines;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Emoji;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Parsing;
using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests;

public class GitHubExtensionsTests : RenderTestBase
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();

    private static MarkdownDocument Parse(string markdown) => Markdown.Parse(markdown, Pipeline);

    private static T[] Descendants<T>(string markdown) where T : MarkdownObject =>
        Parse(markdown).Descendants<T>().ToArray();

    [Theory]
    [InlineData("~~gone~~")]
    [InlineData("~gone~")]
    public void Both_tilde_forms_are_strikethrough(string markdown)
    {
        var emphasis = Assert.Single(Descendants<EmphasisInline>(markdown));
        Assert.Equal('~', emphasis.DelimiterChar);
        Assert.Equal(2, emphasis.DelimiterCount);
        Assert.Equal("gone", Assert.IsType<LiteralInline>(emphasis.FirstChild).Content.ToString());
    }

    [Fact]
    public void Single_tilde_strikethrough_spans_its_delimiters()
    {
        var emphasis = Assert.Single(Descendants<EmphasisInline>("a ~gone~ b"));
        Assert.Equal(2, emphasis.Span.Start);
        Assert.Equal(7, emphasis.Span.End);
    }

    [Theory]
    [InlineData("^up^")]
    [InlineData("++inserted++")]
    [InlineData("==marked==")]
    public void Other_emphasis_extras_stay_plain_text(string markdown)
    {
        Assert.Empty(Descendants<EmphasisInline>(markdown));
    }

    [Fact]
    public void Bold_and_italic_are_unchanged()
    {
        var emphases = Descendants<EmphasisInline>("**bold** *italic*");
        Assert.Equal([('*', 2), ('*', 1)], emphases.Select(e => (e.DelimiterChar, e.DelimiterCount)));
    }

    [Fact]
    public void Alerts_are_parsed()
    {
        var alert = Assert.Single(Descendants<AlertBlock>("> [!NOTE]\n> Useful information."));
        Assert.Equal("NOTE", alert.Kind.ToString());
    }

    [Fact]
    public void Footnotes_are_parsed()
    {
        Assert.Single(Descendants<FootnoteGroup>("Text[^1].\n\n[^1]: The note."));
    }

    [Fact]
    public void Pipe_tables_task_lists_autolinks_emoji_and_front_matter_are_parsed()
    {
        Assert.Single(Descendants<Table>("| A | B |\n|---|---|\n| 1 | 2 |"));
        Assert.Single(Descendants<TaskList>("- [x] done"));
        Assert.True(Assert.Single(Descendants<LinkInline>("Visit https://example.com today")).IsAutoLink);
        Assert.Single(Descendants<EmojiInline>("Launch :rocket: now"));
        Assert.Single(Descendants<YamlFrontMatterBlock>("---\ntitle: Test\n---\n# Heading"));
    }

    [Fact]
    public void Details_and_the_html_subset_are_parsed()
    {
        var details = Assert.Single(Descendants<DetailsBlock>("<details>\n<summary><kbd>K</kbd></summary>\n\n<p align=\"center\"><b>x</b></p>\n\n</details>"));
        Assert.NotNull(details.SummaryInline);
        Assert.Single(details.Descendants<HtmlAlignedBlock>());
        Assert.Single(details.Descendants<HtmlElementInline>(), e => e.Kind == HtmlElementKind.Bold);
    }

    [Fact]
    public void Ascii_smileys_and_grid_tables_are_not_parsed()
    {
        Assert.Empty(Descendants<EmojiInline>("Great :) job"));
        Assert.Empty(Descendants<Table>("+---+---+\n| a | b |\n+---+---+"));
    }

    [Fact]
    public void Pipe_table_options_are_forwarded()
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseGitHubExtensions(new MarkdownExtensionsOptions
            {
                PipeTable = new PipeTableOptions { InferColumnWidthsFromSeparator = true },
            })
            .Build();

        var table = Assert.Single(Markdown.Parse("| A | B |\n|--|------|\n| 1 | 2 |", pipeline).Descendants<Table>());
        Assert.True(table.ColumnDefinitions[1].Width > table.ColumnDefinitions[0].Width);
    }

    [Fact]
    public void Emphasis_extras_on_top_add_superscript_and_keep_single_tilde_strikethrough()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().UseEmphasisExtras().Build();
        var emphases = Markdown.Parse("~gone~ ^up^", pipeline).Descendants<EmphasisInline>().ToArray();
        Assert.Equal([('~', 2), ('^', 1)], emphases.Select(e => (e.DelimiterChar, e.DelimiterCount)));
    }

    [Fact]
    public void On_top_of_the_supported_extensions_single_tilde_becomes_strikethrough()
    {
        var builder = new MarkdownPipelineBuilder().UseSupportedExtensions().UseGitHubExtensions();
        var pipeline = builder.Build();

        Assert.Single(builder.InlineParsers.FindExact<EmphasisInlineParser>()!.EmphasisDescriptors, d => d.Character == '~');
        var emphases = Markdown.Parse("~gone~ ^up^", pipeline).Descendants<EmphasisInline>().ToArray();
        Assert.Equal([('~', 2), ('^', 1)], emphases.Select(e => (e.DelimiterChar, e.DelimiterCount)));
    }

    [Fact]
    public void Strikethrough_extension_does_nothing_without_an_emphasis_parser()
    {
        var builder = new MarkdownPipelineBuilder();
        builder.InlineParsers.RemoveAll(p => p is EmphasisInlineParser);

        new GitHubStrikethroughExtension().Setup(builder);

        Assert.Null(builder.InlineParsers.FindExact<EmphasisInlineParser>());
    }

    [Fact]
    public void Calling_it_twice_adds_its_extensions_once()
    {
        var builder = new MarkdownPipelineBuilder().UseGitHubExtensions().UseGitHubExtensions();
        Assert.Equal(builder.Extensions.Count, builder.Extensions.Select(e => e.GetType()).Distinct().Count());

        var emphasis = Assert.Single(Markdown.Parse("~gone~", builder.Build()).Descendants<EmphasisInline>());
        Assert.Equal(2, emphasis.DelimiterCount);
    }

    [AvaloniaFact]
    public void Single_tilde_renders_as_strikethrough()
    {
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Render("~gone~", Pipeline).Children));
        var span = Assert.IsType<Span>(Assert.Single(textBlock.Inlines!));
        Assert.Equal(TextDecorations.Strikethrough, span.TextDecorations);
    }

    [AvaloniaFact]
    public void Viewer_default_pipeline_renders_alerts_and_single_tilde_strikethrough()
    {
        using var _ = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.Pipeline = null;

        var viewer = new MarkdownViewer { Markdown = "> [!TIP]\n> Try it.\n\n~gone~" };

        var root = Assert.IsType<Grid>(viewer.Content).Children.OfType<StackPanel>().First();
        Assert.Contains("markdown-alert", Assert.IsType<Border>(root.Children[0]).Classes);
        var span = Assert.IsType<Span>(Assert.Single(Assert.IsType<MarkdownSelectableTextBlock>(root.Children[1]).Inlines!));
        Assert.Equal(TextDecorations.Strikethrough, span.TextDecorations);
    }

    [AvaloniaFact]
    public void Viewer_shortcuts_keep_the_default_extensions()
    {
        var viewer = new MarkdownViewer().UseHardlineBreaks();

        var document = Markdown.Parse("> [!NOTE]\n> a\n> b\n\n~gone~", viewer.Pipeline!);
        Assert.Single(document.Descendants<AlertBlock>());
        Assert.Single(document.Descendants<LineBreakInline>(), l => l.IsHard);
        Assert.Equal(2, Assert.Single(document.Descendants<EmphasisInline>()).DelimiterCount);
    }
}
