// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia.Extensions;

/// <summary>Decodes the payload of a <c>data:</c> URI, for the image loaders.</summary>
internal static class DataUri
{
    /// <summary>
    /// The bytes of <paramref name="dataUri"/> (<c>data:[&lt;mediatype&gt;][;base64],&lt;data&gt;</c>): its
    /// base64 payload decoded, or its URL-encoded text as UTF-8. Empty without a comma.
    /// </summary>
    /// <exception cref="FormatException">The base64 payload is not valid base64.</exception>
    public static byte[] Decode(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',');
        if (commaIndex < 0)
            return [];

        var payload = dataUri.AsSpan(commaIndex + 1);
        if (dataUri.AsSpan(0, commaIndex).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            // Decoded straight from the URI, which can be megabytes long, rather than from a copy of the
            // payload. Whitespace makes the exact length unknown: the string overload handles that case.
            if (payload.Length % 4 == 0)
            {
                var padding = payload.EndsWith("==") ? 2 : payload.EndsWith("=") ? 1 : 0;
                var bytes = new byte[payload.Length / 4 * 3 - padding];
                if (Convert.TryFromBase64Chars(payload, bytes, out var written) && written == bytes.Length)
                    return bytes;
            }
            return Convert.FromBase64String(payload.ToString());
        }

        return System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
    }
}
