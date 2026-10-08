// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Net;
using System.Text;

using Avalonia.Headless.XUnit;
using Avalonia.Svg.Skia;

using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Svg.Tests;

public sealed class SvgImageLoaderRemoteTests : IDisposable
{
    private static readonly byte[] SvgBytes =
        Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"4\" height=\"4\"><rect width=\"4\" height=\"4\"/></svg>");

    private readonly LoopbackHttpServer _server = new(new Dictionary<string, Action<HttpListenerResponse>>
    {
        ["/icon.svg"] = LoopbackHttpServer.Content("image/svg+xml", SvgBytes),
        ["/not-svg"] = LoopbackHttpServer.Content("text/plain", Encoding.UTF8.GetBytes("hello world")),
        ["/truncated.svg"] = LoopbackHttpServer.Truncated(SvgBytes[..10]),
    });

    private readonly SvgImageLoader _loader = new();

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task Server_serves_routes_and_404s_unknown_paths()
    {
        using var client = new HttpClient();
        Assert.Equal(SvgBytes, await client.GetByteArrayAsync(_server.Url("icon.svg"), TestContext.Current.CancellationToken));
        var missing = await client.GetAsync(_server.Url("missing"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [AvaloniaFact]
    public async Task Remote_svg_is_loaded_into_an_SvgImage()
    {
        var image = await _loader.LoadAsync(_server.Url("icon.svg"), TestContext.Current.CancellationToken);

        var svg = Assert.IsType<SvgImage>(image);
        Assert.NotNull(svg.Source);
    }

    [AvaloniaTheory]
    [InlineData("missing.svg")]
    [InlineData("not-svg")]
    [InlineData("truncated.svg")]
    public async Task Remote_failures_return_null_so_the_bitmap_loader_can_try(string path)
    {
        Assert.Null(await _loader.LoadAsync(_server.Url(path), TestContext.Current.CancellationToken));
    }
}
