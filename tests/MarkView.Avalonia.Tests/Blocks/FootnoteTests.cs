// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class FootnoteTests : RenderTestBase
{
    private static MarkdownPipeline FootnotePipeline() =>
        new MarkdownPipelineBuilder().UseFootnotes().Build();

    [AvaloniaFact]
    public void Footnote_ref_inline_renders_with_class()
    {
        var result = Render("Text[^1]\n\n[^1]: Definition", FootnotePipeline());
        // First child is a paragraph
        var para = Assert.IsType<MarkdownSelectableTextBlock>(result.Children[0]);
        // There should be an inline with markdown-footnote-ref class somewhere in the inlines
        bool found = ContainsFootnoteRef(para.Inlines!);
        Assert.True(found, "Expected a markdown-footnote-ref inline but found none");
    }

    [AvaloniaFact]
    public void Footnote_group_renders_at_bottom()
    {
        var result = Render("Text[^1]\n\n[^1]: Definition", FootnotePipeline());
        // Last block should be the footnote group panel
        var last = result.Children[^1];
        bool isGroup = (last is StackPanel sp && sp.Classes.Contains("markdown-footnote-group"));
        Assert.True(isGroup, $"Expected StackPanel.markdown-footnote-group at bottom, got {last?.GetType().Name}");
    }

    [AvaloniaFact]
    public void Footnote_anchor_is_registered()
    {
        var pipeline = FootnotePipeline();
        var document = Markdig.Markdown.Parse("Text[^1]\n\n[^1]: Definition", pipeline);
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(document);
        Assert.True(renderer.Anchors.Count > 0, "Expected at least one anchor to be registered for footnote");
    }

    [AvaloniaFact]
    public void Footnote_group_contains_definition_text()
    {
        var result = Render("Text[^1]\n\n[^1]: My footnote text", FootnotePipeline());
        var group = (StackPanel)result.Children[^1];
        // Group should have at least one child (the footnote item row)
        Assert.NotEmpty(group.Children);
    }

    [AvaloniaFact]
    public void Footnote_anchors_map_to_their_definition_rows()
    {
        var pipeline = FootnotePipeline();
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);

        renderer.Render(Markdig.Markdown.Parse(TwoFootnotes, pipeline));

        var group = Assert.IsType<StackPanel>(renderer.RootPanel.Children[^1]);
        Assert.Equal(["fn-1", "fn-2"], renderer.Anchors.Keys.Order());
        Assert.Same(group.Children[0], renderer.Anchors["fn-1"]);
        Assert.Same(group.Children[1], renderer.Anchors["fn-2"]);
    }

    [AvaloniaFact]
    public void Footnote_definition_content_has_no_hyperlink_for_the_back_reference()
    {
        var root = Render(TwoFootnotes, FootnotePipeline());

        var group = Assert.IsType<StackPanel>(root.Children[^1]);
        var texts = group.Children.Cast<Grid>()
            .SelectMany(g => g.Children.OfType<StackPanel>().Single().Children.OfType<MarkdownSelectableTextBlock>());
        Assert.All(texts, t => Assert.DoesNotContain(t.Inlines!, i => i is MarkdownHyperlink));
    }

    private static bool ContainsFootnoteRef(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline.Classes.Contains("markdown-footnote-ref"))
                return true;
            if (inline is Span s && s.Inlines != null && ContainsFootnoteRef(s.Inlines))
                return true;
        }
        return false;
    }

    private const string TwoFootnotes = """
        A[^x] B[^y]

        [^x]: first
        [^y]: second
        """;

    [AvaloniaFact]
    public void Footnote_group_is_preceded_by_a_separator_and_numbers_items_in_order()
    {
        var root = Render(TwoFootnotes, FootnotePipeline());

        var separator = Assert.IsType<Separator>(root.Children[^2]);
        Assert.Contains("markdown-thematic-break", separator.Classes);
        var group = Assert.IsType<StackPanel>(root.Children[^1]);
        Assert.Contains("markdown-footnote-group", group.Classes);
        var labels = group.Children.Cast<Grid>().Select(g => g.Children.OfType<TextBlock>().First(t => Grid.GetColumn(t) == 0).Text);
        Assert.Equal(["1.", "2."], labels);
        Assert.All(group.Children.Cast<Grid>(), g => Assert.Contains("markdown-footnote-item", g.Classes));
    }

    [AvaloniaFact]
    public void Footnote_item_is_a_two_column_grid_with_label_and_content()
    {
        var root = Render(TwoFootnotes, FootnotePipeline());

        var group = Assert.IsType<StackPanel>(root.Children[^1]);
        Assert.Equal(4, group.Spacing);
        var item = Assert.IsType<Grid>(group.Children[0]);
        Assert.Equal(2, item.ColumnDefinitions.Count);
        Assert.True(item.ColumnDefinitions[0].Width.IsAuto);
        Assert.True(item.ColumnDefinitions[1].Width.IsStar);
        var label = item.Children.OfType<TextBlock>().Single();
        Assert.Equal(0, Grid.GetColumn(label));
        Assert.Equal(new Thickness(0, 0, 8, 0), label.Margin);
        var content = item.Children.OfType<StackPanel>().Single();
        Assert.Equal(1, Grid.GetColumn(content));
        Assert.Equal(2, content.Spacing);
        var text = Assert.IsType<MarkdownSelectableTextBlock>(content.Children[0]);
        Assert.Contains("first", string.Concat(text.Inlines!.OfType<Run>().Select(r => r.Text)));
    }

    [AvaloniaFact]
    public void Footnote_references_link_to_their_definition_anchor()
    {
        var root = Render(TwoFootnotes, FootnotePipeline());

        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(root.Children[0]);
        var refs = paragraph.Inlines!.OfType<MarkdownHyperlink>().ToArray();
        Assert.Equal(["#fn-1", "#fn-2"], refs.Select(r => r.NavigateUri!.OriginalString));
        Assert.Equal(["[1]", "[2]"], refs.Select(r => Assert.IsType<Run>(Assert.Single(r.Inlines)).Text));
        Assert.All(refs, r => Assert.Contains("markdown-footnote-ref", r.Classes));
    }
}
