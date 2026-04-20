// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Xml;

using Avalonia.Media;
using Avalonia.Svg.Skia;

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia.Svg;

/// <summary>
/// Loads SVG images from remote URLs (ending in <c>.svg</c>) and
/// from <c>data:image/svg+xml</c> data URIs.
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

    public async Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            SvgSource source;

            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = DecodeDataUri(url);
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
                using var responseStream = await SharedHttpClient.Instance.GetStreamAsync(uri, cancellationToken);
                using var buffer = new MemoryStream();
                await responseStream.CopyToAsync(buffer, cancellationToken);
                buffer.Position = 0;
                source = await Task.Run(() => SvgSource.LoadFromStream(buffer), cancellationToken);
            }

            // SvgImage is an AvaloniaObject — must be created on the UI thread.
            // LoadAsync is always awaited from LoadImageAsync which starts on the Avalonia UI
            // thread, so the SynchronizationContext is captured and continuations resume there.
            return new SvgImage { Source = source };
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

        // Use AsSpan to test the header without allocating a substring
        if (dataUri.AsSpan(0, commaIndex).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            return Convert.FromBase64String(dataUri[(commaIndex + 1)..]);

        // URL-encoded text
        var decoded = Uri.UnescapeDataString(dataUri[(commaIndex + 1)..]);
        return System.Text.Encoding.UTF8.GetBytes(decoded);
    }
}
