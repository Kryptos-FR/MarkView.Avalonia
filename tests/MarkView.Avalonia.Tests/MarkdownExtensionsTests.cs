using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownExtensionsTests : RenderTestBase
{
    [AvaloniaFact]
    public void UseSupportedExtensions_enables_task_lists()
    {
        var pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
        var result = Render("- [x] done\n- [ ] todo", pipeline);
        var listPanel = Assert.IsType<StackPanel>(Assert.Single(result.Children));
        Assert.Contains("markdown-list", listPanel.Classes);
        Assert.Equal(2, listPanel.Children.Count);
    }

    [AvaloniaFact]
    public void UseSupportedExtensions_enables_pipe_tables()
    {
        var pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
        var result = Render("| A | B |\n|---|---|\n| 1 | 2 |", pipeline);
        var grid = Assert.IsType<Grid>(Assert.Single(result.Children));
        Assert.Contains("markdown-table", grid.Classes);
    }

    [AvaloniaFact]
    public void UseSupportedExtensions_enables_autolinks()
    {
        var pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
        // UseAutoLinks turns bare URLs into MarkdownHyperlink spans
        var result = Render("Visit https://example.com today", pipeline);
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var inlines = textBlock.Inlines!.ToList();
        Assert.Contains(inlines, i => i is MarkdownHyperlink);
    }
}
