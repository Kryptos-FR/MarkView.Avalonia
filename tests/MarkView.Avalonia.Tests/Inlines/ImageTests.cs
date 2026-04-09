using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class ImageTests : RenderTestBase
{
    [AvaloniaFact]
    public void Image_renders_in_control_tree()
    {
        var result = Render("![alt text](https://example.com/image.png)");
        Assert.NotEmpty(result.Children);
    }

    [AvaloniaFact]
    public void Image_has_url_in_Tag()
    {
        var result = Render("![alt text](https://example.com/image.png)");
        var image = FindFirst<Image>(result);
        Assert.NotNull(image);
        Assert.Equal("https://example.com/image.png", image.Tag?.ToString());
    }

    [AvaloniaFact]
    public void Image_has_alt_text_tooltip()
    {
        var result = Render("![alt text](https://example.com/image.png)");
        var image = FindFirst<Image>(result);
        Assert.NotNull(image);
        Assert.Equal("alt text", ToolTip.GetTip(image)?.ToString());
    }

    [AvaloniaFact]
    public void Image_relative_url_resolved_against_BaseUri()
    {
        var markdown = "![img](media/screenshot.png)";
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var document = Markdig.Markdown.Parse(markdown, pipeline);
        var renderer = new MarkView.Avalonia.Rendering.AvaloniaRenderer
        {
            BaseUri = new Uri("https://doc.stride3d.net/4.2/ReleaseNotes/")
        };
        pipeline.Setup(renderer);
        renderer.Render(document);
        var image = FindFirst<Image>(renderer.RootPanel);
        Assert.NotNull(image);
        Assert.Equal("https://doc.stride3d.net/4.2/ReleaseNotes/media/screenshot.png", image.Tag?.ToString());
    }

    [AvaloniaFact]
    public void Custom_image_loader_is_invoked_for_matching_url()
    {
        var loader = new SpyImageLoader("https://example.com/image.png");
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var document = Markdig.Markdown.Parse("![alt](https://example.com/image.png)", pipeline);
        var renderer = new AvaloniaRenderer();
        renderer.ImageLoaders.Add(loader);
        pipeline.Setup(renderer);
        renderer.Render(document);

        // Loader was consulted — CanLoad must have been called
        Assert.True(loader.CanLoadCalled);
    }

    [AvaloniaFact]
    public void Image_with_no_matching_loader_falls_back_to_http_path()
    {
        // A loader that never matches — should not throw, HTTP fallback is used
        var loader = new SpyImageLoader("does-not-match");
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var document = Markdig.Markdown.Parse("![alt](https://example.com/image.png)", pipeline);
        var renderer = new AvaloniaRenderer();
        renderer.ImageLoaders.Add(loader);
        pipeline.Setup(renderer);
        renderer.Render(document);
        // No assertion beyond "doesn't throw"
    }

    private static T? FindFirst<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                var found = FindFirst<T>(child);
                if (found != null) return found;
            }
        }
        if (root is ContentControl cc && cc.Content is Control content)
            return FindFirst<T>(content);
        if (root is Decorator dec && dec.Child is Control decChild)
            return FindFirst<T>(decChild);
        if (root is TextBlock tb && tb.Inlines != null)
        {
            foreach (var inline in tb.Inlines)
            {
                if (inline is global::Avalonia.Controls.Documents.InlineUIContainer iuc && iuc.Child is Control inlineChild)
                {
                    var found = FindFirst<T>(inlineChild);
                    if (found != null) return found;
                }
            }
        }
        return null;
    }

    private sealed class SpyImageLoader(string matchUrl) : IImageLoader
    {
        public bool CanLoadCalled { get; private set; }

        public bool CanLoad(string url)
        {
            CanLoadCalled = true;
            return url == matchUrl;
        }

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult<IImage?>(null);
    }
}
