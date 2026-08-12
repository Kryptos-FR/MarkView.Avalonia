// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using MarkView.Avalonia.Extensions;

using Xunit;

namespace MarkView.Avalonia.Tests.Extensions;

public class BitmapImageLoaderTests
{
    private readonly BitmapImageLoader _loader = new();

    // CanLoad — synchronous, no Avalonia context needed

    [Fact]
    public void CanLoad_empty_string_returns_false() =>
        Assert.False(_loader.CanLoad(""));

    [Fact]
    public void CanLoad_avares_url_returns_true() =>
        Assert.True(_loader.CanLoad("avares://MyAssembly/Assets/logo.png"));

    [Fact]
    public void CanLoad_data_image_url_returns_true() =>
        Assert.True(_loader.CanLoad("data:image/png;base64,abc123"));

    [Fact]
    public void CanLoad_http_url_returns_true() =>
        Assert.True(_loader.CanLoad("http://example.com/image.png"));

    [Fact]
    public void CanLoad_https_url_returns_true() =>
        Assert.True(_loader.CanLoad("https://example.com/image.png"));

    [Fact]
    public void CanLoad_file_url_returns_false() =>
        Assert.False(_loader.CanLoad("file:///C:/images/photo.png"));

    [Fact]
    public void CanLoad_ftp_url_returns_false() =>
        Assert.False(_loader.CanLoad("ftp://example.com/image.png"));

    [Fact]
    public void CanLoad_relative_path_returns_false() =>
        Assert.False(_loader.CanLoad("images/photo.png"));

    // LoadAsync — data URI path (Avalonia Bitmap needs platform)

    [AvaloniaFact]
    public async Task LoadAsync_base64_data_uri_returns_Bitmap()
    {
        var dataUri = CreatePngDataUri();
        var result = await _loader.LoadAsync(dataUri);
        Assert.NotNull(result);
        Assert.IsType<Bitmap>(result);
    }

    // LoadAsync — non-URI string returns null without any network call

    [Fact]
    public async Task LoadAsync_non_absolute_url_returns_null()
    {
        var result = await _loader.LoadAsync("relative/path.png", TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    // LoadAsync — cancelled token propagates as OperationCanceledException

    [Fact]
    public async Task LoadAsync_cancelled_token_throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _loader.LoadAsync("http://localhost/image.png", cts.Token));
    }

    // Helpers

    private static string CreatePngDataUri()
    {
        var wb = new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var ms = new MemoryStream();
        wb.Save(ms, PngBitmapEncoderOptions.Default);
        return $"data:image/png;base64,{Convert.ToBase64String(ms.ToArray())}";
    }
}
