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
/// The image cache shared by every viewer (<see cref="MarkdownViewerDefaults.ImageCacheSize"/>). Each
/// test loads its own URLs, so that what other tests cache does not matter.
/// </summary>
public class MarkdownViewerImageCacheTests
{
    private sealed class FakeImage : IImage
    {
        public Size Size => new(20, 10);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
    }

    private sealed class CountingLoader : IImageLoader
    {
        public int Loads;
        public TaskCompletionSource<IImage?>? Gate { get; init; }

        public bool CanLoad(string url) => url.StartsWith("fake:", StringComparison.Ordinal);

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Loads++;
            return Gate?.Task ?? Task.FromResult<IImage?>(new FakeImage());
        }
    }

    private sealed class LoaderExtension(IImageLoader loader) : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, loader);
    }

    private static string Document() => $"![picture](fake://{Guid.NewGuid()}.png)";

    private static (Window Window, MarkdownViewer Viewer) Show(string markdown, IImageLoader loader)
    {
        var viewer = new MarkdownViewer();
        viewer.Extensions.Add(new LoaderExtension(loader));
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        return (window, viewer);
    }

    private static Image[] Images(MarkdownViewer viewer) => viewer.GetVisualDescendants().OfType<Image>().ToArray();

    [AvaloniaFact]
    public async Task Re_rendering_a_document_shows_its_images_without_loading_them_again()
    {
        var loader = new CountingLoader();
        var markdown = Document();
        var (window, viewer) = Show(markdown, loader);
        var first = Images(viewer).Single();
        await AsyncTestHelpers.WaitUntilAsync(() => first.Source is not null);

        viewer.Markdown = markdown + "\n\nEdited";
        window.UpdateLayout();

        var second = Images(viewer).Single();
        Assert.NotSame(first, second);
        Assert.Same(first.Source, second.Source);
        Assert.Equal(1, loader.Loads);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_new_viewer_of_the_same_document_does_not_load_its_images_again()
    {
        var loader = new CountingLoader();
        var markdown = Document();
        var (window, viewer) = Show(markdown, loader);
        var first = Images(viewer).Single();
        await AsyncTestHelpers.WaitUntilAsync(() => first.Source is not null);
        window.Close();

        (window, viewer) = Show(markdown, new CountingLoader());

        Assert.Same(first.Source, Images(viewer).Single().Source);
        Assert.Equal(1, loader.Loads);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Copies_of_an_image_in_a_document_share_one_load()
    {
        var loader = new CountingLoader { Gate = new TaskCompletionSource<IImage?>() };
        var image = Document();
        var (window, viewer) = Show($"{image}\n\n{image}", loader);

        var picture = new FakeImage();
        loader.Gate.SetResult(picture);
        var images = Images(viewer);
        await AsyncTestHelpers.WaitUntilAsync(() => images.All(i => i.Source is not null));

        Assert.Equal(2, images.Length);
        Assert.All(images, i => Assert.Same(picture, i.Source));
        Assert.Equal(1, loader.Loads);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_failed_image_is_loaded_again_on_the_next_render()
    {
        var loader = new CountingLoader { Gate = new TaskCompletionSource<IImage?>() };
        loader.Gate.SetResult(null);
        var markdown = Document();
        var (window, viewer) = Show(markdown, loader);
        await AsyncTestHelpers.WaitUntilAsync(() => viewer.GetVisualDescendants().OfType<Border>().Any(b => b.Classes.Contains("markdown-image-failed")));

        viewer.Markdown = markdown + "\n\nEdited";
        await AsyncTestHelpers.WaitUntilAsync(() => loader.Loads == 2);
        window.Close();
    }

    [AvaloniaFact]
    public async Task With_a_cache_size_of_zero_every_render_loads_its_images()
    {
        var saved = MarkdownViewerDefaults.ImageCacheSize;
        try
        {
            MarkdownViewerDefaults.ImageCacheSize = 0;
            Assert.Equal(0, ImageCache.Shared.Capacity);
            var loader = new CountingLoader();
            var markdown = Document();
            var (window, viewer) = Show(markdown, loader);
            await AsyncTestHelpers.WaitUntilAsync(() => Images(viewer).Single().Source is not null);

            viewer.Markdown = markdown + "\n\nEdited";
            await AsyncTestHelpers.WaitUntilAsync(() => Images(viewer).Single().Source is not null);

            Assert.Equal(2, loader.Loads);
            window.Close();
        }
        finally
        {
            MarkdownViewerDefaults.ImageCacheSize = saved;
        }
    }

    [AvaloniaFact]
    public async Task Clearing_the_cache_makes_the_next_render_load_its_images_again()
    {
        var loader = new CountingLoader();
        var markdown = Document();
        var (window, viewer) = Show(markdown, loader);
        await AsyncTestHelpers.WaitUntilAsync(() => Images(viewer).Single().Source is not null);

        MarkdownViewerDefaults.ClearImageCache();
        viewer.Markdown = markdown + "\n\nEdited";
        await AsyncTestHelpers.WaitUntilAsync(() => Images(viewer).Single().Source is not null);

        Assert.Equal(2, loader.Loads);
        window.Close();
    }

    [AvaloniaFact]
    public void The_cache_holds_a_hundred_images_by_default_and_rejects_a_negative_size()
    {
        Assert.Equal(100, MarkdownViewerDefaults.ImageCacheSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkdownViewerDefaults.ImageCacheSize = -1);
        Assert.Equal(100, MarkdownViewerDefaults.ImageCacheSize);
    }
}
