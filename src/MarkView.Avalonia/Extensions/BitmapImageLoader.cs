// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Default image loader for bitmap formats (PNG, JPEG, GIF, BMP, WebP, …).
/// Handles <c>data:image/…;base64,…</c> data URIs, and any absolute URL whose content the
/// <see cref="IResourceResolver"/> can open (<c>avares://</c>, <c>file://</c> and <c>http/https</c> by default).
/// Registered last in <see cref="Rendering.AvaloniaRenderer.ImageLoaders"/> so that
/// specialised loaders (e.g. SVG) take priority.
/// </summary>
internal sealed class BitmapImageLoader : IImageLoader
{

    public bool CanLoad(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;

        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase);

        // Whatever the scheme, the resource resolver may open it.
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }

    public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) =>
        LoadAsync(url, DefaultResourceResolver.Instance, cancellationToken);

    public async Task<IImage?> LoadAsync(string url, IResourceResolver resources, CancellationToken cancellationToken = default)
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

            using var stream = await resources.OpenAsync(uri, cancellationToken);
            if (stream is null)
                return null;
            if (stream.CanSeek)
                return new Bitmap(stream);

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return new Bitmap(buffer);
        }
        catch (OperationCanceledException) { throw; }
        // Stryker disable once Block : removing the body leaves an empty catch, and Stryker substitutes the default return value (null) for the missing return, which is exactly what the body returns
        catch (Exception e) when (e is HttpRequestException or IOException) { return null; }
    }

    internal static byte[] DecodeDataUri(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',');
        if (commaIndex < 0) return [];

        if (dataUri.AsSpan(0, commaIndex).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            return Convert.FromBase64String(dataUri[(commaIndex + 1)..]);

        var decoded = Uri.UnescapeDataString(dataUri[(commaIndex + 1)..]);
        return System.Text.Encoding.UTF8.GetBytes(decoded);
    }
}
