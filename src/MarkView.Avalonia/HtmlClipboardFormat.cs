// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using System.Text;

using Avalonia.Input;

namespace MarkView.Avalonia;

/// <summary>
/// Writes HTML to the clipboard next to plain text. Avalonia has no cross-platform HTML format, so each
/// desktop platform gets its own: <c>HTML Format</c> with its CF_HTML header on Windows, <c>public.html</c>
/// on macOS, <c>text/html</c> on Linux. Other platforms (the browser, mobile) get none.
/// </summary>
internal static class HtmlClipboardFormat
{
    private static readonly DataFormat<byte[]> WindowsFormat = DataFormat.CreateBytesPlatformFormat("HTML Format");
    private static readonly DataFormat<string> MacOSFormat = DataFormat.CreateStringPlatformFormat("public.html");
    private static readonly DataFormat<string> LinuxFormat = DataFormat.CreateStringPlatformFormat("text/html");

    /// <summary>Whether this platform has an HTML format.</summary>
    public static bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || IsUnix;

    private static bool IsUnix => OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD();

    /// <summary>Adds <paramref name="html"/>, a fragment, to <paramref name="item"/> in this platform's HTML format.</summary>
    public static void Set(DataTransferItem item, string html)
    {
        if (OperatingSystem.IsWindows())
            item.Set(WindowsFormat, ToWindowsFormat(html));
        else if (OperatingSystem.IsMacOS())
            item.Set(MacOSFormat, html);
        else if (IsUnix)
            item.Set(LinuxFormat, html);
    }

    /// <summary>Reads the HTML fragment this platform's HTML format holds in <paramref name="transfer"/>, if any.</summary>
    public static async Task<string?> TryGetAsync(IAsyncDataTransfer transfer)
    {
        if (OperatingSystem.IsWindows())
            return await transfer.TryGetValueAsync(WindowsFormat) is { } bytes ? FragmentOf(bytes) : null;
        if (OperatingSystem.IsMacOS())
            return await transfer.TryGetValueAsync(MacOSFormat);
        return IsUnix ? await transfer.TryGetValueAsync(LinuxFormat) : null;
    }

    private const string HeaderFormat =
        "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
    private const string Prefix = "<html><body>\r\n<!--StartFragment-->";
    private const string Suffix = "<!--EndFragment-->\r\n</body></html>";

    /// <summary>
    /// The CF_HTML clipboard format of <paramref name="fragment"/>: a header giving the UTF-8 byte offsets of
    /// the document and of the fragment in it, then the document.
    /// </summary>
    public static byte[] ToWindowsFormat(string fragment)
    {
        var headerLength = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, HeaderFormat, 0, 0, 0, 0));
        var startFragment = headerLength + Encoding.UTF8.GetByteCount(Prefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(Suffix);
        var header = string.Format(CultureInfo.InvariantCulture, HeaderFormat, headerLength, endHtml, startFragment, endFragment);
        return Encoding.UTF8.GetBytes(header + Prefix + fragment + Suffix);
    }

    /// <summary>The fragment of a CF_HTML clipboard format, between the offsets its header gives.</summary>
    public static string? FragmentOf(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        if (Offset(text, "StartFragment:") is not { } start || Offset(text, "EndFragment:") is not { } end
            || start > end || end > data.Length)
            return null;
        return Encoding.UTF8.GetString(data, start, end - start);
    }

    private static int? Offset(string header, string name)
    {
        var index = header.IndexOf(name, StringComparison.Ordinal);
        if (index < 0) return null;
        var value = header.AsSpan(index + name.Length);
        var lineEnd = value.IndexOfAny('\r', '\n');
        return int.TryParse(lineEnd < 0 ? value : value[..lineEnd], NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
            ? offset : null;
    }
}
