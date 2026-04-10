// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Svg.Tests;

public class SvgImageLoaderTests
{
    [Theory]
    [InlineData("https://example.com/icon.svg", true)]
    [InlineData("https://example.com/image.png", false)]
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
        Assert.Single(renderer.ImageLoaders);
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

        Assert.Equal(2, renderer.ImageLoaders.Count);
        Assert.IsType<SvgImageLoader>(renderer.ImageLoaders[0]);
        Assert.Same(existing, renderer.ImageLoaders[1]);
    }

    [AvaloniaFact]
    public void UseSvg_adds_SvgExtension_to_viewer()
    {
        var viewer = new MarkdownViewer();
        viewer.UseSvg();
        Assert.Single(viewer.Extensions);
        Assert.IsType<SvgExtension>(viewer.Extensions[0]);
    }

    private sealed class DummyLoader : IImageLoader
    {
        public bool CanLoad(string url) => false;
        public Task<global::Avalonia.Media.IImage?> LoadAsync(string url, CancellationToken ct = default)
            => Task.FromResult<global::Avalonia.Media.IImage?>(null);
    }
}
