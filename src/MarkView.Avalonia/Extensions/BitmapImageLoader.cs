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
        // Every image is decoded on the thread pool: a large one would otherwise stall the layout pass
        // that attaches it (data: and avares: images load synchronously up to here). Bitmap is not an
        // AvaloniaObject, so it can be created on any thread.
        // Stryker disable Boolean : ConfigureAwait(true) only resumes on the caller's thread, which changes no result a test can see
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

            using var stream = await resources.OpenAsync(uri, cancellationToken).ConfigureAwait(false);
            if (stream is null)
                return null;
            return await Task.Run(async () =>
            {
                if (stream.CanSeek)
                    return new Bitmap(stream);
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                buffer.Position = 0;
                return new Bitmap(buffer);
            }, cancellationToken).ConfigureAwait(false);
        }
        // Stryker restore Boolean
        catch (OperationCanceledException) { throw; }
        // Stryker disable once Block : removing the body leaves an empty catch, and Stryker substitutes the default return value (null) for the missing return, which is exactly what the body returns
        catch (Exception e) when (e is HttpRequestException or IOException) { return null; }
    }
}
