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
        // Every image is decoded on the thread pool: a large one would otherwise stall the layout pass
        // that attaches it (data: and avares: images load synchronously up to here). Bitmap is not an
        // AvaloniaObject, so it can be created on any thread.
        try
        {
            if (url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return await Task.Run(() =>
                {
                    using var ms = new MemoryStream(DataUri.Decode(url));
                    return new Bitmap(ms);
                }, cancellationToken).ConfigureAwait(false);
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            if (uri.Scheme == "avares")
            {
                return await Task.Run(() =>
                {
                    using var stream = AssetLoader.Open(uri);
                    return new Bitmap(stream);
                }, cancellationToken).ConfigureAwait(false);
            }

            using var responseStream = await SharedHttpClient.Instance.GetStreamAsync(uri, cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            await responseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            return await Task.Run(() => new Bitmap(buffer), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        // Stryker disable once Block : removing the body leaves an empty catch, and Stryker substitutes the default return value (null) for the missing return, which is exactly what the body returns
        catch (Exception e) when (e is HttpRequestException or IOException) { return null; }
    }
}
