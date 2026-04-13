// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Default image loader for bitmap formats (PNG, JPEG, GIF, BMP, WebP, …).
/// Handles <c>avares://</c> embedded resources, <c>http/https</c> URLs, and
/// <c>data:image/…;base64,…</c> data URIs.
/// Registered last in <see cref="Rendering.AvaloniaRenderer.ImageLoaders"/> so that
/// specialised loaders (e.g. SVG) take priority.
/// </summary>
internal sealed class BitmapImageLoader : IImageLoader
{
    private static readonly HttpClient HttpClient = new();

    public bool CanLoad(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;

        if (url.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)) return true;

        if (url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return true;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

        return false;
    }

    public async Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            if (url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = DecodeDataUri(url);
                using var ms = new MemoryStream(bytes);
                return new Bitmap(ms);
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            if (uri.Scheme == "avares")
            {
                using var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }

            using var responseStream = await HttpClient.GetStreamAsync(uri, cancellationToken);
            using var buffer = new MemoryStream();
            await responseStream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return new Bitmap(buffer);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException) { return null; }
        catch (IOException) { return null; }
    }

    private static byte[] DecodeDataUri(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',');
        if (commaIndex < 0) return [];

        if (dataUri.AsSpan(0, commaIndex).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            return Convert.FromBase64String(dataUri[(commaIndex + 1)..]);

        var decoded = Uri.UnescapeDataString(dataUri[(commaIndex + 1)..]);
        return System.Text.Encoding.UTF8.GetBytes(decoded);
    }
}
