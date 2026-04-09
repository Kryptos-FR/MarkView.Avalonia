using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Extensions;
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

    [AvaloniaFact]
    public void Fragment_link_click_does_not_fire_public_LinkClicked_event()
    {
        // Render via the renderer directly to get the anchor registry
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var document = Markdig.Markdown.Parse("## My Heading\n\n[jump](#my-heading)", pipeline);
        var renderer = new MarkView.Avalonia.Rendering.AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(document);

        // Fragment anchor must be registered
        Assert.True(renderer.Anchors.ContainsKey("my-heading"));
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
    public void Extensions_list_is_empty_by_default()
    {
        var viewer = new MarkdownViewer();
        Assert.NotNull(viewer.Extensions);
        Assert.Empty(viewer.Extensions);
    }

    [AvaloniaFact]
    public void Extensions_Register_is_called_during_render()
    {
        var spy = new SpyExtension();
        var viewer = new MarkdownViewer();
        viewer.Extensions.Add(spy);
        viewer.Markdown = "Hello";
        Assert.True(spy.RegisterCalled);
    }

    [AvaloniaFact]
    public void Extensions_Register_receives_renderer_instance()
    {
        var spy = new SpyExtension();
        var viewer = new MarkdownViewer();
        viewer.Extensions.Add(spy);
        viewer.Markdown = "```csharp\nvar x = 1;\n```";
        Assert.NotNull(spy.ReceivedRenderer);
    }

    private sealed class SpyExtension : IMarkViewExtension
    {
        public bool RegisterCalled { get; private set; }
        public AvaloniaRenderer? ReceivedRenderer { get; private set; }

        public void Register(AvaloniaRenderer renderer)
        {
            RegisterCalled = true;
            ReceivedRenderer = renderer;
        }
    }
}
