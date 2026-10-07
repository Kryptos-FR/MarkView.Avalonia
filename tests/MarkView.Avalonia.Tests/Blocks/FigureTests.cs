// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Markdig;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class FigureTests : RenderTestBase
{
    private static MarkdownPipeline FigurePipeline() =>
        new MarkdownPipelineBuilder().UseFigures().Build();

    [AvaloniaFact]
    public void Figure_renders_as_Border_with_figure_class()
    {
        // ^^^ opens a figure, ^^^ Caption closes it with a caption line
        var result = Render("^^^\nContent\n\n^^^ My Caption", FigurePipeline());
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-figure", border.Classes);
    }

    [AvaloniaFact]
    public void Figure_contains_StackPanel_with_content_and_caption()
    {
        var result = Render("^^^\nContent\n\n^^^ My Caption", FigurePipeline());
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        var panel = Assert.IsType<StackPanel>(border.Child);
        // Panel should have at least one content child and one caption
        Assert.True(panel.Children.Count >= 2,
            $"Expected at least 2 children (content + caption), got {panel.Children.Count}");
    }

    [AvaloniaFact]
    public void Figure_caption_has_caption_class()
    {
        var result = Render("^^^\nContent\n\n^^^ My Caption", FigurePipeline());
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        var panel = Assert.IsType<StackPanel>(border.Child);
        // Last child is the caption TextBlock
        var caption = panel.Children[^1];
        Assert.IsType<MarkdownSelectableTextBlock>(caption);
        Assert.Contains("markdown-figure-caption", caption.Classes);
    }

    [AvaloniaFact]
    public void Figure_without_caption_renders_single_border()
    {
        // A figure with no caption line still renders
        var result = Render("^^^\nContent\n^^^", FigurePipeline());
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-figure", border.Classes);
    }

    [AvaloniaFact]
    public void Figure_caption_text_is_rendered_below_the_content()
    {
        var root = Render("""
            ^^^
            Body text
            ^^^ The caption
            """, FigurePipeline());

        var texts = root.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>()
            .Select(t => MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!))
            .ToArray();
        Assert.Equal(["Body text", "The caption"], texts);
    }

    [AvaloniaFact]
    public void Figure_panel_is_spaced_and_caption_wraps_and_is_bound_to_the_renderer()
    {
        var pipeline = FigurePipeline();
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse("""
            ^^^
            Body text
            ^^^ The caption
            """, pipeline));

        var border = Assert.IsType<Border>(Assert.Single(renderer.RootPanel.Children));
        var panel = Assert.IsType<StackPanel>(border.Child);
        Assert.Equal(4, panel.Spacing);
        var caption = Assert.IsType<MarkdownSelectableTextBlock>(panel.Children[^1]);
        Assert.Equal(TextWrapping.Wrap, caption.TextWrapping);
        Assert.Same(renderer, caption.Renderer);
    }
}
