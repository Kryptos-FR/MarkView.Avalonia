// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia.Rendering;
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
}
