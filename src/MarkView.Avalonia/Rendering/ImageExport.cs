// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Saves a rendered image or diagram to a file (see <see cref="MarkdownViewer.IsImageSaveEnabled"/>): as PNG,
/// and as SVG when the picture's SVG document is at hand.
/// </summary>
internal static class ImageExport
{
    /// <summary>How much larger than its size a picture drawn as vectors is rendered as PNG.</summary>
    internal const double VectorScale = 2;

    /// <summary>The most pixels a picture drawn as vectors is rendered with along its longer side.</summary>
    internal const int MaxPixelSize = 8192;

    /// <summary>The name a saved file is suggested when an image's address names none.</summary>
    internal const string DefaultFileName = "image";

    private static readonly char[] InvalidFileNameChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    // The SVG documents of pictures, kept as long as the picture itself, which the image cache may share
    // between documents.
    private static readonly ConditionalWeakTable<IImage, byte[]> SvgDocuments = new();

    /// <summary>
    /// The name, without an extension, a saved copy of the image is suggested; <c>null</c> (the default)
    /// for an image that cannot be saved.
    /// </summary>
    internal static readonly AttachedProperty<string?> FileNameProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("FileName", typeof(ImageExport));

    internal static string? GetFileName(Image image) => image.GetValue(FileNameProperty);

    internal static void SetFileName(Image image, string? value) => image.SetValue(FileNameProperty, value);

    /// <summary>Notes <paramref name="svg"/> as the SVG document <paramref name="picture"/> was drawn from.</summary>
    internal static void SetSvg(IImage picture, byte[] svg) => SvgDocuments.AddOrUpdate(picture, svg);

    /// <summary>The SVG document <paramref name="picture"/> was drawn from, if one was noted.</summary>
    internal static byte[]? GetSvg(IImage picture) => SvgDocuments.TryGetValue(picture, out var svg) ? svg : null;

    /// <summary>Whether <paramref name="image"/> shows a picture that can be saved.</summary>
    internal static bool CanSave(Image image) => image.Source is not null && GetFileName(image) is not null;

    /// <summary>The formats <paramref name="picture"/> can be saved in, the best first.</summary>
    internal static IReadOnlyList<MarkdownImageFormat> GetFormats(IImage picture) =>
        GetSvg(picture) is null ? [MarkdownImageFormat.Png] : [MarkdownImageFormat.Svg, MarkdownImageFormat.Png];

    /// <summary>The usual extension of a file in <paramref name="format"/>, without the dot.</summary>
    internal static string GetExtension(MarkdownImageFormat format) =>
        format == MarkdownImageFormat.Svg ? "svg" : "png";

    /// <summary>The media type of a file in <paramref name="format"/>.</summary>
    internal static string GetMimeType(MarkdownImageFormat format) =>
        format == MarkdownImageFormat.Svg ? "image/svg+xml" : "image/png";

    /// <summary>
    /// Writes <paramref name="picture"/> to <paramref name="stream"/> in <paramref name="format"/>: its SVG
    /// document, a bitmap's own pixels as PNG, or any other picture rendered as PNG at
    /// <see cref="VectorScale"/> times its size on <paramref name="background"/>. The file is made in memory
    /// first, then copied asynchronously, as a browser's file can only be written that way.
    /// </summary>
    internal static async Task WriteAsync(IImage picture, IBrush? background, MarkdownImageFormat format, Stream stream)
    {
        if (format == MarkdownImageFormat.Svg)
        {
            var svg = GetSvg(picture) ?? throw new InvalidOperationException("The picture has no SVG document.");
            await stream.WriteAsync(svg);
            return;
        }

        using var buffer = new MemoryStream();
        if (picture is Bitmap bitmap)
        {
            bitmap.Save(buffer, PngBitmapEncoderOptions.Default);
        }
        else
        {
            using var rendered = Render(picture, background);
            rendered.Save(buffer, PngBitmapEncoderOptions.Default);
        }
        buffer.Position = 0;
        await buffer.CopyToAsync(stream);
    }

    /// <summary>
    /// Renders <paramref name="picture"/> on <paramref name="background"/> at <see cref="VectorScale"/> times its
    /// size, or less where its longer side would pass <see cref="MaxPixelSize"/> pixels.
    /// </summary>
    internal static RenderTargetBitmap Render(IImage picture, IBrush? background)
    {
        var size = picture.Size;
        var longer = Math.Max(size.Width, size.Height);
        if (!(longer > 0))
            throw new InvalidOperationException("The picture has no size.");
        var scale = Math.Min(VectorScale, MaxPixelSize / longer);
        var pixels = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(size.Width * scale)),
            Math.Max(1, (int)Math.Ceiling(size.Height * scale)));
        var bitmap = new RenderTargetBitmap(pixels, new Vector(96 * scale, 96 * scale));
        using (var context = bitmap.CreateDrawingContext())
        {
            var bounds = new Rect(size);
            if (background is not null)
                context.FillRectangle(background, bounds);
            context.DrawImage(picture, bounds);
        }
        return bitmap;
    }

    /// <summary>
    /// The name a saved copy of the image at <paramref name="url"/> is suggested: the last segment of its path,
    /// without its extension or the characters a file name cannot hold, or <see cref="DefaultFileName"/>.
    /// </summary>
    internal static string FileNameFromUrl(string url)
    {
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return DefaultFileName;
        var path = url.Split('?', '#')[0];
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !uri.IsFile)
            path = uri.AbsolutePath;
        var segment = Uri.UnescapeDataString(path.TrimEnd('/', '\\'));
        segment = segment[(segment.LastIndexOfAny(['/', '\\']) + 1)..];
        var dot = segment.LastIndexOf('.');
        if (dot > 0)
            segment = segment[..dot];
        var name = new string(segment.Where(c => !char.IsControl(c) && Array.IndexOf(InvalidFileNameChars, c) < 0).ToArray()).Trim();
        return name.Length > 0 ? name : DefaultFileName;
    }
}
