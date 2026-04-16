// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Markdig;
using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerExtensionsTests
{
    [AvaloniaFact]
    public void UseFootnotes_sets_pipeline_on_viewer()
    {
        var viewer = new MarkdownViewer();
        var result = viewer.UseFootnotes();
        Assert.Same(viewer, result);
        Assert.NotNull(viewer.Pipeline);
    }

    [AvaloniaFact]
    public void UseAlertBlocks_sets_pipeline_on_viewer()
    {
        var viewer = new MarkdownViewer();
        var result = viewer.UseAlertBlocks();
        Assert.Same(viewer, result);
        Assert.NotNull(viewer.Pipeline);
    }

    [AvaloniaFact]
    public void UseAbbreviations_sets_pipeline_on_viewer()
    {
        var viewer = new MarkdownViewer();
        var result = viewer.UseAbbreviations();
        Assert.Same(viewer, result);
        Assert.NotNull(viewer.Pipeline);
    }

    [AvaloniaFact]
    public void UseFigures_sets_pipeline_on_viewer()
    {
        var viewer = new MarkdownViewer();
        var result = viewer.UseFigures();
        Assert.Same(viewer, result);
        Assert.NotNull(viewer.Pipeline);
    }

    [AvaloniaFact]
    public void UseMediaLinks_sets_pipeline_on_viewer()
    {
        var viewer = new MarkdownViewer();
        var result = viewer.UseMediaLinks();
        Assert.Same(viewer, result);
        Assert.NotNull(viewer.Pipeline);
    }

    [AvaloniaFact]
    public void UseFootnotes_renders_footnote_when_markdown_set()
    {
        var viewer = new MarkdownViewer();
        viewer.UseFootnotes();
        viewer.Markdown = "Text[^1]\n\n[^1]: Definition";

        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var contentGrid = Assert.IsType<Grid>(scrollViewer.Content);
        var panel = Assert.IsType<StackPanel>(contentGrid.Children[0]);
        // Footnote group should be the last child
        Assert.True(panel.Children.Count >= 2, "Expected content + footnote group");
    }

    [AvaloniaFact]
    public void UseAlertBlocks_renders_alert_border_when_markdown_set()
    {
        var viewer = new MarkdownViewer();
        viewer.UseAlertBlocks();
        viewer.Markdown = "> [!NOTE]\n> Hello";

        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var contentGrid = Assert.IsType<Grid>(scrollViewer.Content);
        var panel = Assert.IsType<StackPanel>(contentGrid.Children[0]);
        var border = Assert.IsType<Border>(Assert.Single(panel.Children));
        Assert.Contains("markdown-alert", border.Classes);
    }
}
