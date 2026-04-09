using Avalonia.Media;
using Avalonia.Svg.Skia;
using MarkView.Avalonia.Extensions;
using System.Xml;

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
        return path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
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

            using var stream = new MemoryStream(bytes);
            var source = SvgSource.LoadFromStream(stream);
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

        var header = dataUri[..commaIndex];
        var data = dataUri[(commaIndex + 1)..];

        if (header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            return Convert.FromBase64String(data);

        // URL-encoded text
        var decoded = Uri.UnescapeDataString(data);
        return System.Text.Encoding.UTF8.GetBytes(decoded);
    }
}
