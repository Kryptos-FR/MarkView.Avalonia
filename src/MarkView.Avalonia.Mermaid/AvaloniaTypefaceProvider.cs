// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;

using Avalonia.Media;
using Avalonia.Svg.Skia;

using SkiaSharp;

using Svg.Skia.TypefaceProviders;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// Lets the SVG renderer draw diagram text with the font the document uses, as Avalonia resolves it.
/// The SVG renderer otherwise only sees SkiaSharp's own fonts, which never include fonts embedded in
/// the application (e.g. <c>WithInterFont()</c>), and in the browser are a single monospace font that
/// draws every label as boxes.
/// </summary>
internal sealed class AvaloniaTypefaceProvider(
    Func<FontFamily, FontWeight, FontStyle, (string FamilyName, SKData Data)?> resolve,
    bool useDocumentFontForAnyFamily) : ITypefaceProvider
{
    private static readonly AvaloniaTypefaceProvider s_shared = new(ResolveWithFontManager,
        NeedsDocumentFontForAnyFamily(OperatingSystem.IsBrowser(), SKFontManager.Default.FontFamilyCount));
    private static readonly Lock s_registrationLock = new();
    private static bool s_registered;

    // Font files by the weight and slant they were resolved for, kept as SKData so each request gets
    // its own SKTypeface: the SVG renderer owns, and may dispose, what it is given.
    private readonly ConcurrentDictionary<(FontFamily Family, SKFontStyleWeight Weight, SKFontStyleSlant Slant), (string FamilyName, SKData Data)?> _fonts = new();

    /// <summary>
    /// Makes diagram text use <paramref name="documentFont"/> (the font of the document the diagram
    /// is in) wherever the SVG asks for that font by name, and wherever SkiaSharp has no usable fonts
    /// of its own. The provider is added in front of the typeface providers of every SVG loaded
    /// through <see cref="SvgSource"/> on the first call.
    /// </summary>
    public static void Use(FontFamily documentFont)
    {
        lock (s_registrationLock)
        {
            if (!s_registered)
            {
                var settings = SvgSource.s_skiaModel.Settings;
                var providers = settings.TypefaceProviders ?? [];
                providers.Insert(0, s_shared);
                settings.TypefaceProviders = providers;
                s_registered = true;
            }
        }
        s_shared.DocumentFont = documentFont;
    }

    /// <summary>
    /// Whether every font the SVG asks for should be drawn with the document font: in the browser
    /// SkiaSharp's single font draws every label as boxes, and without any SkiaSharp font nothing is drawn.
    /// </summary>
    internal static bool NeedsDocumentFontForAnyFamily(bool isBrowser, int skiaFontFamilyCount) => isBrowser || skiaFontFamilyCount == 0;

    /// <summary>The provider <see cref="Use"/> registers.</summary>
    internal static AvaloniaTypefaceProvider Shared => s_shared;

    /// <summary>The font of the document diagrams are drawn for, or <c>null</c> to provide none.</summary>
    public FontFamily? DocumentFont { get; set; }

    /// <summary>
    /// Returns the document font when <paramref name="fontFamily"/> (a single family or a CSS list)
    /// names it, or for any family when SkiaSharp has no usable fonts. Otherwise returns <c>null</c>,
    /// leaving the family to the SVG renderer's own resolution.
    /// </summary>
    public SKTypeface? FromFamilyName(string fontFamily, SKFontStyleWeight fontWeight, SKFontStyleWidth fontWidth, SKFontStyleSlant fontStyle)
    {
        if (DocumentFont is not { } documentFont)
            return null;

        var font = _fonts.GetOrAdd((documentFont, fontWeight, fontStyle), key => Resolve(key.Family, key.Weight, key.Slant));
        if (font is not var (familyName, data))
            return null;

        return useDocumentFontForAnyFamily || Names(fontFamily, familyName)
            ? SKTypeface.FromData(data)
            : null;
    }

    private (string FamilyName, SKData Data)? Resolve(FontFamily family, SKFontStyleWeight weight, SKFontStyleSlant slant)
    {
        var style = slant switch
        {
            SKFontStyleSlant.Italic => FontStyle.Italic,
            SKFontStyleSlant.Oblique => FontStyle.Oblique,
            _ => FontStyle.Normal,
        };
        return resolve(family, (FontWeight)(int)weight, style);
    }

    // Whether a CSS font-family list ("'Inter', system-ui, sans-serif") contains the family name.
    private static bool Names(string fontFamilyList, string familyName)
    {
        foreach (var name in fontFamilyList.Split(','))
        {
            if (string.Equals(name.Trim().Trim('\'', '"'), familyName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static (string FamilyName, SKData Data)? ResolveWithFontManager(FontFamily family, FontWeight weight, FontStyle style)
    {
        try
        {
            if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(family, style, weight), out var glyphTypeface)
                || !glyphTypeface.PlatformTypeface.TryGetStream(out var stream))
                return null;

            using (stream)
                return (glyphTypeface.FamilyName, SKData.Create(stream));
        }
        // Stryker disable once Block : removing the body leaves an empty catch, and Stryker substitutes the default return value (null) for the missing return, which is exactly what the body returns
        catch (Exception)
        {
            // No usable font manager: leave every family to the SVG renderer.
            return null;
        }
    }
}
