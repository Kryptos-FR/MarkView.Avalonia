// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using MarkView.Avalonia;
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
        // Insert at index 0 so the spy takes priority over the default BitmapImageLoader
        // and its CanLoad is called synchronously (before the first await in the loader chain).
        renderer.ImageLoaders.Insert(0, loader);
        pipeline.Setup(renderer);
        renderer.Render(document);

        // Loading is deferred to AttachedToVisualTree; attach to a headless window so
        // the event fires and CanLoad is called (synchronously, before the first await).
        var window = new Window { Content = renderer.RootPanel };
        window.Show();

        Assert.True(loader.CanLoadCalled);
    }

    [AvaloniaFact]
    public void Image_size_hint_sets_Width_and_Height()
    {
        var viewer = new MarkdownViewer();
        viewer.Markdown = "![logo](https://example.com/logo.png =200x100)";

        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var contentGrid = Assert.IsType<Grid>(scrollViewer.Content);
        var panel = Assert.IsType<StackPanel>(contentGrid.Children[0]);
        var image = FindFirst<Image>(panel);
        Assert.NotNull(image);
        Assert.Equal(200.0, image.Width);
        Assert.Equal(100.0, image.Height);
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

    private sealed class FakeBitmapLoader(string url, IImage image) : IImageLoader
    {
        public bool CanLoad(string checkUrl) => checkUrl == url;
        public Task<IImage?> LoadAsync(string checkUrl, CancellationToken cancellationToken = default)
            => Task.FromResult<IImage?>(image);
    }

    private static async Task PumpUntilSettledAsync()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Parses and renders markdown with the given mode/loader, returning the renderer so
    /// callers can either inspect renderer.RootPanel directly or host it under a Window
    /// for layout (mirrors the existing Custom_image_loader_is_invoked_for_matching_url
    /// pattern, which already hosts renderer.RootPanel directly under a Window).
    /// </summary>
    private static AvaloniaRenderer RenderDocument(string markdown, ImageResizeMode mode, IImageLoader? loader = null)
    {
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var document = Markdig.Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer { ImageResizeMode = mode };
        if (loader is not null)
            renderer.ImageLoaders.Insert(0, loader);
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer;
    }

    [AvaloniaFact]
    public void ScaleDownToFit_mode_sets_Uniform_and_DownOnly()
    {
        var renderer = RenderDocument("![img](https://example.com/image.png)", ImageResizeMode.ScaleDownToFit);
        var image = FindFirst<Image>(renderer.RootPanel);
        Assert.NotNull(image);
        Assert.Equal(Stretch.Uniform, image!.Stretch);
        Assert.Equal(StretchDirection.DownOnly, image.StretchDirection);
    }

    [AvaloniaFact]
    public void Natural_mode_sets_Stretch_None()
    {
        var renderer = RenderDocument("![img](https://example.com/image.png)", ImageResizeMode.Natural);
        var image = FindFirst<Image>(renderer.RootPanel);
        Assert.NotNull(image);
        Assert.Equal(Stretch.None, image!.Stretch);
    }

    [AvaloniaFact]
    public void Fill_mode_sets_Uniform_and_Both()
    {
        var renderer = RenderDocument("![img](https://example.com/image.png)", ImageResizeMode.Fill);
        var image = FindFirst<Image>(renderer.RootPanel);
        Assert.NotNull(image);
        Assert.Equal(Stretch.Uniform, image!.Stretch);
        Assert.Equal(StretchDirection.Both, image.StretchDirection);
    }

    [AvaloniaFact]
    public async Task ScaleDownToFit_large_image_scales_down_to_container_width()
    {
        var bigImage = new RenderTargetBitmap(new PixelSize(1600, 1200));
        var loader = new FakeBitmapLoader("fake://big.png", bigImage);
        var renderer = RenderDocument("![big](fake://big.png)", ImageResizeMode.ScaleDownToFit, loader);

        var window = new Window { Width = 600, Height = 400, Content = renderer.RootPanel };
        window.Show();
        await PumpUntilSettledAsync();

        var image = FindFirst<Image>(renderer.RootPanel);
        Assert.NotNull(image);
        Assert.Equal(600, image!.Bounds.Width, 0);
        Assert.Equal(450, image.Bounds.Height, 0);
    }

    [AvaloniaFact]
    public async Task ScaleDownToFit_small_image_does_not_upscale()
    {
        var smallImage = new RenderTargetBitmap(new PixelSize(200, 150));
        var loader = new FakeBitmapLoader("fake://small.png", smallImage);
        var renderer = RenderDocument("![small](fake://small.png)", ImageResizeMode.ScaleDownToFit, loader);

        var window = new Window { Width = 1200, Height = 400, Content = renderer.RootPanel };
        window.Show();
        await PumpUntilSettledAsync();

        var image = FindFirst<Image>(renderer.RootPanel);
        Assert.NotNull(image);
        Assert.Equal(200, image!.Bounds.Width, 0);
        Assert.Equal(150, image.Bounds.Height, 0);
    }

    [AvaloniaFact]
    public async Task Fill_mode_upscales_small_image_to_container_width()
    {
        var smallImage = new RenderTargetBitmap(new PixelSize(200, 150));
        var loader = new FakeBitmapLoader("fake://small.png", smallImage);
        var renderer = RenderDocument("![small](fake://small.png)", ImageResizeMode.Fill, loader);

        var window = new Window { Width = 600, Height = 400, Content = renderer.RootPanel };
        window.Show();
        await PumpUntilSettledAsync();

        var image = FindFirst<Image>(renderer.RootPanel);
        Assert.NotNull(image);
        Assert.Equal(600, image!.Bounds.Width, 0);
        Assert.Equal(450, image.Bounds.Height, 0);
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
