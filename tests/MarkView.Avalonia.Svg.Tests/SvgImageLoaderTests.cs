// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using Avalonia.Svg.Skia;
using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Svg.Tests;

public class SvgImageLoaderTests
{
    [Theory]
    [InlineData("https://example.com/icon.svg", true)]
    [InlineData("https://example.com/image.png", true)]  // accepted speculatively; LoadAsync returns null if not SVG
    [InlineData("https://shields.io/badge/build-passing-green", true)]  // no extension; may be SVG response
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+", true)]
    [InlineData("data:image/png;base64,abc", false)]
    [InlineData("https://example.com/icon.SVG", true)]   // case-insensitive extension
    [InlineData("relative/path/image.svg", true)]
    public void CanLoad_returns_expected_value(string url, bool expected)
    {
        var loader = new SvgImageLoader();
        Assert.Equal(expected, loader.CanLoad(url));
    }

    [AvaloniaFact]
    public void SvgExtension_Register_inserts_loader_at_index_0()
    {
        var renderer = new AvaloniaRenderer();
        var extension = new SvgExtension();
        extension.Register(renderer);
        // AvaloniaRenderer starts with BitmapImageLoader at index 0; SvgExtension inserts at 0, pushing it to index 1
        Assert.Equal(2, renderer.ImageLoaders.Count);
        Assert.IsType<SvgImageLoader>(renderer.ImageLoaders[0]);
    }

    [AvaloniaFact]
    public void SvgExtension_Register_inserts_before_existing_loaders()
    {
        var renderer = new AvaloniaRenderer();
        var existing = new DummyLoader();
        renderer.ImageLoaders.Add(existing);

        var extension = new SvgExtension();
        extension.Register(renderer);

        // AvaloniaRenderer starts with BitmapImageLoader at index 0; after Add(existing) it's at index 1;
        // SvgExtension inserts at 0, pushing both to indices 1 and 2
        Assert.Equal(3, renderer.ImageLoaders.Count);
        Assert.IsType<SvgImageLoader>(renderer.ImageLoaders[0]);
        Assert.Same(existing, renderer.ImageLoaders[2]);
    }

    [AvaloniaFact]
    public void UseSvg_adds_SvgExtension_to_viewer()
    {
        var viewer = new MarkdownViewer();
        viewer.UseSvg();
        Assert.Single(viewer.Extensions);
        Assert.IsType<SvgExtension>(viewer.Extensions[0]);
    }

    [AvaloniaFact]
    public async Task LoadAsync_with_base64_data_uri_returns_SvgImage()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="1" height="1"/>""";
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
        var dataUri = $"data:image/svg+xml;base64,{base64}";

        var loader = new SvgImageLoader();
        var result = await loader.LoadAsync(dataUri);

        Assert.NotNull(result);
        Assert.IsType<SvgImage>(result);
    }

    [AvaloniaFact]
    public async Task LoadAsync_with_url_encoded_data_uri_returns_SvgImage()
    {
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="1" height="1"/>""";
        var dataUri = $"data:image/svg+xml,{Uri.EscapeDataString(svg)}";

        var loader = new SvgImageLoader();
        var result = await loader.LoadAsync(dataUri);

        Assert.NotNull(result);
        Assert.IsType<SvgImage>(result);
    }

    [AvaloniaFact]
    public async Task LoadAsync_with_invalid_svg_data_uri_returns_null()
    {
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("not valid svg"));
        var dataUri = $"data:image/svg+xml;base64,{base64}";

        var loader = new SvgImageLoader();
        var result = await loader.LoadAsync(dataUri);

        Assert.Null(result);
    }

    private sealed class DummyLoader : IImageLoader
    {
        public bool CanLoad(string url) => false;
        public Task<global::Avalonia.Media.IImage?> LoadAsync(string url, CancellationToken ct = default)
            => Task.FromResult<global::Avalonia.Media.IImage?>(null);
    }
}
