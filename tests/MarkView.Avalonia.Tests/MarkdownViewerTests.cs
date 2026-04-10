using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
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
    public void BaseUri_resolves_relative_image_url()
    {
        var viewer = new MarkdownViewer
        {
            BaseUri = new Uri("https://example.com/docs/"),
            Markdown = "![img](image.png)"
        };
        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var panel = Assert.IsType<StackPanel>(scrollViewer.Content);
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(panel.Children));
        var uiContainer = textBlock.Inlines!.OfType<InlineUIContainer>().Single();
        var image = Assert.IsType<Image>(uiContainer.Child);
        Assert.Equal("https://example.com/docs/image.png", image.Tag?.ToString());
    }

    [AvaloniaFact]
    public void Heading_is_registered_as_anchor_after_render()
    {
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var document = Markdig.Markdown.Parse("## Hello World", pipeline);
        var renderer = new MarkView.Avalonia.Rendering.AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(document);

        Assert.True(renderer.Anchors.ContainsKey("hello-world"));
    }

    [AvaloniaFact]
    public void Custom_pipeline_overrides_default_rendering()
    {
        // A pipeline without pipe-table support renders the table as plain text, not a Grid
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var viewer = new MarkdownViewer
        {
            Pipeline = pipeline,
            Markdown = "| A | B |\n|---|---|\n| 1 | 2 |"
        };
        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var panel = Assert.IsType<StackPanel>(scrollViewer.Content);
        Assert.DoesNotContain(panel.Children, c => c is Grid);
    }

    [AvaloniaFact]
    public void External_link_fires_LinkClicked_event()
    {
        var viewer = new MarkdownViewer { Markdown = "<https://example.com>" };

        string? clickedUrl = null;
        viewer.LinkClicked += (_, e) => clickedUrl = e.Url;

        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var panel = Assert.IsType<StackPanel>(scrollViewer.Content);
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(panel.Children));
        var uiContainer = textBlock.Inlines!.OfType<InlineUIContainer>().Single();
        var button = Assert.IsType<HyperlinkButton>(uiContainer.Child);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("https://example.com", clickedUrl);
    }
}
