// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Xml;

using Avalonia.Media;
using Avalonia.Svg.Skia;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Svg;

/// <summary>
/// Loads SVG images from URLs ending in <c>.svg</c>, from any <c>http/https</c> URL (falling back to the
/// bitmap loader when the response is not SVG), and from <c>data:image/svg+xml</c> data URIs. Their content
/// is read through the viewer's <see cref="IResourceResolver"/>, so <c>avares://</c>, <c>file://</c> and the
/// application's own schemes work as well.
/// </summary>
public sealed class SvgImageLoader : IImageLoader
{

    public bool CanLoad(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        if (url.StartsWith("data:image/svg+xml", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check extension — ignore query string
        var path = url.Split('?')[0];
        if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            return true;

        // Accept any absolute HTTP/HTTPS URL speculatively — LoadAsync will return null
        // if the response is not valid SVG, letting the bitmap fallback take over.
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return true;

        return false;
    }

    public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) =>
        LoadAsync(url, DefaultResourceResolver.Instance, cancellationToken);

    public async Task<IImage?> LoadAsync(string url, IResourceResolver resources, CancellationToken cancellationToken = default)
    {
        try
        {
            SvgSource source;
            byte[] bytes;

            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                bytes = DataUri.Decode(url);
                source = await Task.Run(() =>
                {
                    using var ms = new MemoryStream(bytes);
                    return SvgSource.LoadFromStream(ms);
                }, cancellationToken);
            }
            else
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    return null;
                using var responseStream = await resources.OpenAsync(uri, cancellationToken);
                if (responseStream is null)
                    return null;
                using var buffer = new MemoryStream();
                await responseStream.CopyToAsync(buffer, cancellationToken);
                bytes = buffer.ToArray();
                buffer.Position = 0;
                source = await Task.Run(() => SvgSource.LoadFromStream(buffer), cancellationToken);
            }

            // SvgImage is an AvaloniaObject — must be created on the UI thread.
            // LoadAsync is always awaited from LoadImageAsync which starts on the Avalonia UI
            // thread, so the SynchronizationContext is captured and continuations resume there.
            var image = new SvgImage { Source = source };
            // A saved copy of the image is its own file.
            ImageExport.SetSvg(image, bytes);
            return image;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        // Stryker disable once Block : removing the body leaves an empty catch, and Stryker substitutes the default return value (null) for the missing return, which is exactly what the body returns
        catch (Exception e) when (e is HttpRequestException or IOException or XmlException) { return null; }
    }
}
