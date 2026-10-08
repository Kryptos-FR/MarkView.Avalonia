// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Net;

using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Extensions;

public sealed class BitmapImageLoaderRemoteTests : IDisposable
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly LoopbackHttpServer _server = new(new Dictionary<string, Action<HttpListenerResponse>>
    {
        ["/pixel.png"] = LoopbackHttpServer.Content("image/png", Png),
        ["/truncated.png"] = LoopbackHttpServer.Truncated(Png[..8]),
    });

    public void Dispose() => _server.Dispose();

    [AvaloniaFact]
    public async Task Remote_image_is_downloaded_into_a_Bitmap()
    {
        Assert.IsType<Bitmap>(await new BitmapImageLoader().LoadAsync(_server.Url("pixel.png"), TestContext.Current.CancellationToken));
    }

    [AvaloniaTheory]
    [InlineData("missing.png")]
    [InlineData("truncated.png")]
    public async Task Remote_failures_return_null(string path)
    {
        Assert.Null(await new BitmapImageLoader().LoadAsync(_server.Url(path), TestContext.Current.CancellationToken));
    }
}
