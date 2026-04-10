// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Xml;

using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia.Threading;

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia.Svg;

/// <summary>
/// Loads SVG images from remote URLs (ending in <c>.svg</c>) and
/// from <c>data:image/svg+xml</c> data URIs.
/// </summary>
public sealed class SvgImageLoader : IImageLoader
{
    private static readonly HttpClient HttpClient = new();

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

    public async Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            byte[] bytes;

            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                bytes = DecodeDataUri(url);
            }
            else
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    return null;
                bytes = await HttpClient.GetByteArrayAsync(uri, cancellationToken);
            }

            SvgSource source;
            using (var stream = new MemoryStream(bytes))
                source = SvgSource.LoadFromStream(stream);

            // SvgImage is an AvaloniaObject — must be created on the UI thread.
            return await Dispatcher.UIThread.InvokeAsync(() => new SvgImage { Source = source });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException) { return null; }
        catch (IOException) { return null; }
        catch (XmlException) { return null; }
    }

    private static byte[] DecodeDataUri(string dataUri)
    {
        // Format: data:[<mediatype>][;base64],<data>
        var commaIndex = dataUri.IndexOf(',');
        if (commaIndex < 0)
            return [];

        var header = dataUri[..commaIndex];
        var data = dataUri[(commaIndex + 1)..];

        if (header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            return Convert.FromBase64String(data);

        // URL-encoded text
        var decoded = Uri.UnescapeDataString(data);
        return System.Text.Encoding.UTF8.GetBytes(decoded);
    }
}
