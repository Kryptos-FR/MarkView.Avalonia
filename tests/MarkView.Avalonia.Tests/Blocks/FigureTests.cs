// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
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
}
