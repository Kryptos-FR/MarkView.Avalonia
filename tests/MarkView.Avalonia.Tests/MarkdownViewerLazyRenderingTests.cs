// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// <see cref="MarkdownViewer.IsLazyRenderingEnabled"/> for images. Each test loads its own URLs, so
/// that what other tests put in the shared image cache does not matter.
/// </summary>
public class MarkdownViewerLazyRenderingTests
{
    private sealed class FakeImage : IImage
    {
        public Size Size => new(20, 10);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
    }

    private sealed class RecordingLoader : IImageLoader
    {
        public List<string> Loaded { get; } = [];

        public bool CanLoad(string url) => true;

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Loaded.Add(url);
            return Task.FromResult<IImage?>(new FakeImage());
        }
    }

    /// <summary>Records each load; the first one never completes, the next ones do at once.</summary>
    private sealed class FirstLoadHangsLoader : IImageLoader
    {
        public List<CancellationToken> Tokens { get; } = [];

        public bool CanLoad(string url) => true;

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Tokens.Count == 1 ? new TaskCompletionSource<IImage?>().Task : Task.FromResult<IImage?>(new FakeImage());
        }
    }

    private sealed class LoaderExtension(IImageLoader loader) : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, loader);
    }

    // Enough paragraphs to push what follows well over a screen below a 300 px viewport.
    private static readonly string Filler = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"Paragraph {i}"));

    private static string NewImageUrl() => $"fake://{Guid.NewGuid()}.png";

    private sealed class Fixture : IDisposable
    {
        public RecordingLoader Loader { get; } = new();
        public MarkdownViewer Viewer { get; }
        public Window Window { get; }

        public Fixture(string markdown, bool? lazy = null, IImageLoader? loader = null)
        {
            Viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create() };
            if (lazy is { } value)
                Viewer.IsLazyRenderingEnabled = value;
            Viewer.Extensions.Add(new LoaderExtension(loader ?? Loader));
            Viewer.Markdown = markdown;
            Window = new Window { Width = 400, Height = 300, Content = Viewer };
            Window.Show();
        }

        public ScrollViewer ScrollViewer => Viewer.GetVisualDescendants().OfType<ScrollViewer>().First();

        public Image[] Images => Viewer.GetVisualDescendants().OfType<Image>().ToArray();

        public void ScrollToEnd()
        {
            ScrollViewer.ScrollToEnd();
            Window.UpdateLayout();
        }

        public void ScrollToHome()
        {
            ScrollViewer.ScrollToHome();
            Window.UpdateLayout();
        }

        public void Dispose() => Window.Close();
    }

    [AvaloniaFact]
    public void Lazy_rendering_is_on_by_default()
    {
        Assert.True(new MarkdownViewer().IsLazyRenderingEnabled);
    }

    [AvaloniaFact]
    public async Task Image_far_below_the_visible_area_loads_once_scrolled_near()
    {
        var url = NewImageUrl();
        using var f = new Fixture($"{Filler}\n\n![picture]({url})");
        await AsyncTestHelpers.PumpAsync();
        Assert.Empty(f.Loader.Loaded);

        f.ScrollToEnd();

        var image = Assert.Single(f.Images);
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        Assert.Equal([url], f.Loader.Loaded);
    }

    [AvaloniaFact]
    public async Task Image_in_view_loads_without_scrolling()
    {
        var url = NewImageUrl();
        using var f = new Fixture($"![picture]({url})\n\n{Filler}");

        var image = Assert.Single(f.Images);
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        Assert.Equal([url], f.Loader.Loaded);
    }

    [AvaloniaFact]
    public async Task Cached_image_far_below_the_visible_area_shows_at_once()
    {
        var url = NewImageUrl();
        using var f = new Fixture($"![picture]({url})");
        var first = Assert.Single(f.Images);
        await AsyncTestHelpers.WaitUntilAsync(() => first.Source is not null);

        f.Viewer.Markdown = $"{Filler}\n\n![picture]({url})";
        f.Window.UpdateLayout();

        var second = Assert.Single(f.Images);
        Assert.Same(first.Source, second.Source);
        Assert.Single(f.Loader.Loaded);
    }

    [AvaloniaFact]
    public async Task Without_lazy_rendering_an_image_far_below_the_visible_area_loads_at_once()
    {
        var url = NewImageUrl();
        using var f = new Fixture($"{Filler}\n\n![picture]({url})", lazy: false);

        var image = Assert.Single(f.Images);
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        Assert.Equal([url], f.Loader.Loaded);
    }

    [AvaloniaFact]
    public async Task YouTube_thumbnail_far_below_the_visible_area_loads_once_scrolled_near()
    {
        // A unique video id per run keeps the thumbnail out of the shared cache.
        var id = Guid.NewGuid().ToString("N")[..11];
        using var f = new Fixture($"{Filler}\n\n![video](https://www.youtube.com/watch?v={id})");
        await AsyncTestHelpers.PumpAsync();
        Assert.Empty(f.Loader.Loaded);

        f.ScrollToEnd();

        var thumbnail = Assert.Single(f.Images);
        await AsyncTestHelpers.WaitUntilAsync(() => thumbnail.Source is not null);
        Assert.Equal([$"https://img.youtube.com/vi/{id}/hqdefault.jpg"], f.Loader.Loaded);
    }

    [AvaloniaFact]
    public async Task Turning_lazy_rendering_off_renders_again_and_loads_every_image()
    {
        var url = NewImageUrl();
        using var f = new Fixture($"{Filler}\n\n![picture]({url})");
        var before = f.Viewer.Content;

        f.Viewer.IsLazyRenderingEnabled = false;

        Assert.NotSame(before, f.Viewer.Content);
        var image = Assert.Single(f.Images);
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        Assert.Equal([url], f.Loader.Loaded);
    }

    [AvaloniaFact]
    public async Task Image_replaced_before_it_came_near_is_never_loaded()
    {
        var url = NewImageUrl();
        using var f = new Fixture($"{Filler}\n\n![picture]({url})");

        f.Viewer.Markdown = "Replaced";
        f.ScrollToEnd();
        await AsyncTestHelpers.PumpAsync();

        Assert.Empty(f.Loader.Loaded);
    }

    [AvaloniaFact]
    public async Task Image_put_back_after_its_load_was_cancelled_loads_again_once_scrolled_near()
    {
        var loader = new FirstLoadHangsLoader();
        using var f = new Fixture($"{Filler}\n\n![picture]({NewImageUrl()})", loader: loader);
        f.ScrollToEnd();
        Assert.Single(loader.Tokens);
        f.ScrollToHome();

        f.Window.Content = null;
        f.Window.Content = f.Viewer;
        f.Window.UpdateLayout();
        await AsyncTestHelpers.PumpAsync();
        Assert.True(Assert.Single(loader.Tokens).IsCancellationRequested);

        f.ScrollToEnd();

        var image = Assert.Single(f.Images);
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        Assert.Equal(2, loader.Tokens.Count);
        Assert.False(loader.Tokens[1].IsCancellationRequested);
    }
}
