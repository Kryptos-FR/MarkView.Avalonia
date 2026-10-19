// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Parsing;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

/// <summary>How each <see cref="HtmlMode"/> treats the HTML of a document.</summary>
public class HtmlModeTests : RenderTestBase
{
    private static MarkdownPipeline GitHub(HtmlMode mode) =>
        new MarkdownPipelineBuilder().UseGitHubExtensions().UseHtmlMode(mode).Build();

    private static string Text(StackPanel root) =>
        MarkdownSelectableTextBlock.ExtractPlainText(Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children)).Inlines!);

    // ── Pipeline setup ────────────────────────────────────────────────────────

    [Fact]
    public void UseHtmlMode_adds_one_extension_and_the_last_call_wins()
    {
        var builder = new MarkdownPipelineBuilder().UseHtmlMode(HtmlMode.Escape).UseGitHubExtensions().UseHtmlMode(HtmlMode.Ignore);

        var extension = Assert.IsType<HtmlModeExtension>(Assert.Single(builder.Extensions, e => e is HtmlModeExtension));
        Assert.Equal(HtmlMode.Ignore, extension.Mode);
    }

    [Fact]
    public void The_safe_subset_mode_adds_the_subset_and_details_blocks()
    {
        var builder = new MarkdownPipelineBuilder().UseHtmlMode(HtmlMode.SafeSubset);

        Assert.Contains(builder.Extensions, e => e is HtmlSubsetExtension);
        Assert.Contains(builder.Extensions, e => e is HtmlDetailsExtension);
    }

    [Theory]
    [InlineData(HtmlMode.Escape)]
    [InlineData(HtmlMode.Ignore)]
    public void Other_modes_add_neither(HtmlMode mode)
    {
        var builder = new MarkdownPipelineBuilder().UseHtmlMode(mode);

        Assert.DoesNotContain(builder.Extensions, e => e is HtmlSubsetExtension or HtmlDetailsExtension);
    }

    // ── Escape ────────────────────────────────────────────────────────────────

    [Fact]
    public void Escape_keeps_inline_tags_as_literal_text()
    {
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(Markdown.Parse("a <b>x</b> List<string>", GitHub(HtmlMode.Escape))));

        Assert.DoesNotContain(paragraph.Inline!, inline => inline is HtmlInline);
        Assert.Equal("a <b>x</b> List<string>", string.Concat(paragraph.Inline!.OfType<LiteralInline>().Select(l => l.Content.ToString())));
    }

    [Fact]
    public void Escape_parses_the_markdown_between_the_tags_of_an_html_block()
    {
        var document = Markdown.Parse("<div align=\"center\">\n**x**\n</div>", GitHub(HtmlMode.Escape));

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document));
        Assert.Contains(paragraph.Inline!, inline => inline is EmphasisInline);
    }

    [Fact]
    public void Escape_keeps_details_tags_as_text()
    {
        var document = Markdown.Parse("<details>\n<summary>Title</summary>\n\nBody\n\n</details>", GitHub(HtmlMode.Escape));

        Assert.All(document, block => Assert.IsType<ParagraphBlock>(block));
        Assert.Equal(3, document.Count);
    }

    [Fact]
    public void Escape_keeps_autolinks_and_entities()
    {
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(Markdown.Parse("<https://example.com> &amp;", GitHub(HtmlMode.Escape))));

        Assert.Contains(paragraph.Inline!, inline => inline is AutolinkInline);
        Assert.Contains(paragraph.Inline!, inline => inline is HtmlEntityInline);
    }

    [AvaloniaFact]
    public void Escape_renders_the_tags_as_text()
    {
        Assert.Equal("Press <kbd>Ctrl</kbd>", Text(Render("Press <kbd>Ctrl</kbd>", GitHub(HtmlMode.Escape))));
    }

    [AvaloniaFact]
    public void Escape_wins_over_extensions_added_after_it()
    {
        var pipeline = new MarkdownPipelineBuilder().UseHtmlMode(HtmlMode.Escape).UseGitHubExtensions().Build();

        Assert.Equal("<sub>2</sub>", Text(Render("<sub>2</sub>", pipeline)));
    }

    // ── Ignore ────────────────────────────────────────────────────────────────

    [Fact]
    public void Ignore_leaves_the_html_to_the_renderer()
    {
        var document = Markdown.Parse("<details>\n<summary>Title</summary>\n\nBody\n\n</details>\n\na <b>x</b>", GitHub(HtmlMode.Ignore));

        Assert.IsType<HtmlBlock>(document[0]);
        Assert.Contains(((ParagraphBlock)document[^1]).Inline!, inline => inline is HtmlInline);
        Assert.DoesNotContain(document, block => block is DetailsBlock or HtmlAlignedBlock);
    }

    [AvaloniaFact]
    public void Ignore_drops_the_tags_and_keeps_the_text_between_inline_ones()
    {
        var root = Render("a <b>x</b> <kbd>y</kbd> <span>z</span>", GitHub(HtmlMode.Ignore));

        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children));
        Assert.All(textBlock.Inlines!, inline => Assert.IsType<Run>(inline));
        Assert.Equal("a x y z", MarkdownSelectableTextBlock.ExtractPlainText(textBlock.Inlines!));
    }

    [AvaloniaFact]
    public void Ignore_does_not_show_html_blocks()
    {
        var root = Render("<p align=\"center\">\n<b>Title</b>\n</p>\n\n<details>\n<summary>S</summary>\n\nBody\n\n</details>", GitHub(HtmlMode.Ignore));

        Assert.Equal("Body", Text(root));
    }

    [AvaloniaFact]
    public void Ignore_still_breaks_lines_at_br()
    {
        var root = Render("a<br>b", GitHub(HtmlMode.Ignore));

        var inlines = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children)).Inlines!.ToList();
        Assert.IsType<LineBreak>(inlines[1]);
    }

    // ── Safe subset ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void The_safe_subset_mode_set_after_another_one_renders_the_subset_again()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().UseHtmlMode(HtmlMode.Escape).UseHtmlMode(HtmlMode.SafeSubset).Build();

        var root = Render("Press <kbd>Ctrl</kbd>", pipeline);

        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children));
        Assert.Contains(textBlock.Inlines!, inline => inline is InlineUIContainer { Child: HtmlKeyboardKey });
    }

    [Fact]
    public void The_safe_subset_mode_groups_details_blocks()
    {
        var document = Markdown.Parse("<details>\n<summary>Title</summary>\n\nBody\n\n</details>", new MarkdownPipelineBuilder().UseHtmlMode(HtmlMode.SafeSubset).Build());

        Assert.IsType<DetailsBlock>(Assert.Single(document));
    }
}
