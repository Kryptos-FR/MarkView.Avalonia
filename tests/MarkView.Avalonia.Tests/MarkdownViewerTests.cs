using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerTests
{
    [AvaloniaFact]
    public void Setting_Markdown_property_renders_content()
    {
        var viewer = new MarkdownViewer
        {
            Markdown = "# Hello\n\nWorld"
        };
        Assert.NotNull(viewer.Content);
        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var panel = Assert.IsType<StackPanel>(scrollViewer.Content);
        Assert.Equal(2, panel.Children.Count);
    }

    [AvaloniaFact]
    public void Changing_Markdown_rerenders()
    {
        var viewer = new MarkdownViewer { Markdown = "First" };
        viewer.Markdown = "# Second";
        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var panel = Assert.IsType<StackPanel>(scrollViewer.Content);
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(panel.Children));
        Assert.Contains("markdown-h1", textBlock.Classes);
    }

    [AvaloniaFact]
    public void Null_Markdown_clears_content()
    {
        var viewer = new MarkdownViewer { Markdown = "Hello" };
        viewer.Markdown = null;
        Assert.Null(viewer.Content);
    }

    [AvaloniaFact]
    public void BaseUri_is_passed_to_renderer()
    {
        var viewer = new MarkdownViewer
        {
            BaseUri = new Uri("https://example.com/docs/"),
            Markdown = "![img](image.png)"
        };
        Assert.NotNull(viewer.Content);
    }
}
