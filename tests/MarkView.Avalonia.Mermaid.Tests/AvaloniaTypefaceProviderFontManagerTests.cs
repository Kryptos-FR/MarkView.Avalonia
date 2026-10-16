// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Fonts;

using SkiaSharp;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

/// <summary>
/// How the shared provider resolves the document font through the Avalonia <see cref="FontManager"/>.
/// </summary>
public class AvaloniaTypefaceProviderFontManagerTests
{
    // The shared provider caches what it resolved per family, so each test asks for a family of its own.
    private static FontFamily NewDocumentFont() => new($"Document Font {Guid.NewGuid():N}");

    // The family name of the font file the headless font manager hands over for the family.
    private static string ResolvedFamilyName(FontFamily family)
    {
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var glyphTypeface));
        return glyphTypeface.FamilyName;
    }

    private static SKTypeface? RequestShared(string family) =>
        AvaloniaTypefaceProvider.Shared.FromFamilyName(family, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

    [AvaloniaFact]
    public void Shared_provider_hands_over_the_font_file_avalonia_resolves_for_the_document_font()
    {
        var documentFont = NewDocumentFont();
        var familyName = ResolvedFamilyName(documentFont);
        AvaloniaTypefaceProvider.Use(documentFont);

        using var typeface = RequestShared($"'{familyName}', sans-serif");

        Assert.NotNull(typeface);
        Assert.Equal(familyName, typeface.FamilyName);
    }

    [AvaloniaFact]
    public void Shared_provider_leaves_other_families_to_the_svg_renderer_where_skia_has_fonts()
    {
        AvaloniaTypefaceProvider.Use(NewDocumentFont());

        using var typeface = RequestShared("'Segoe UI', sans-serif");

        // Only where SkiaSharp has no fonts of its own (as in the browser) does the document font
        // stand in for every family.
        Assert.Equal(SKFontManager.Default.FontFamilyCount == 0, typeface is not null);
    }

    [AvaloniaFact]
    public void Shared_provider_provides_nothing_when_avalonia_has_no_font_for_the_document_font()
    {
        // A font collection that was never added to the font manager.
        var documentFont = new FontFamily($"fonts:Missing{Guid.NewGuid():N}#Missing");
        Assert.False(FontManager.Current.TryGetGlyphTypeface(new Typeface(documentFont), out _));
        AvaloniaTypefaceProvider.Use(documentFont);

        Assert.Null(RequestShared("Missing"));
    }

    [AvaloniaFact]
    public void Shared_provider_provides_nothing_when_avalonia_cannot_hand_over_the_font_file()
    {
        var familyName = ResolvedFamilyName(NewDocumentFont());

        // The collection's fonts have readable glyph tables but no font file, as a platform
        // typeface that cannot open a stream reports.
        using var fonts = new FontCollectionScope(name =>
            FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily(name)), out var resolved)
                ? new GlyphTypeface(StreamlessTypeface.Wrap(resolved.PlatformTypeface))
                : null);
        var documentFont = new FontFamily($"{fonts.Key}#{familyName}");
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(documentFont), out _));
        AvaloniaTypefaceProvider.Use(documentFont);

        Assert.Null(RequestShared(familyName));
    }

    [AvaloniaFact]
    public void Shared_provider_provides_nothing_when_the_font_manager_fails()
    {
        var familyName = ResolvedFamilyName(NewDocumentFont());
        using var fonts = new FontCollectionScope(_ => throw new InvalidOperationException("The font manager failed."));
        var documentFont = new FontFamily($"{fonts.Key}#{familyName}");
        AvaloniaTypefaceProvider.Use(documentFont);

        SKTypeface? typeface = null;
        var exception = Record.Exception(() => typeface = RequestShared(familyName));

        Assert.Null(exception);
        Assert.Null(typeface);
    }

    /// <summary>
    /// Adds a font collection to the font manager until disposed; <c>resolve</c> provides (or not, or
    /// throws for) each family the collection is asked for.
    /// </summary>
    private sealed class FontCollectionScope : IDisposable
    {
        public FontCollectionScope(Func<string, GlyphTypeface?> resolve) =>
            FontManager.Current.AddFontCollection(new StubFontCollection(Key, resolve));

        public Uri Key { get; } = new($"fonts:Test{Guid.NewGuid():N}");

        public void Dispose() => FontManager.Current.RemoveFontCollection(Key);
    }

    private sealed class StubFontCollection(Uri key, Func<string, GlyphTypeface?> resolve) : IFontCollection
    {
        public Uri Key => key;

        public bool TryGetGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch,
            [NotNullWhen(true)] out GlyphTypeface? glyphTypeface) =>
            (glyphTypeface = resolve(familyName)) is not null;

        public bool TryMatchCharacter(int codepoint, FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch,
            string? familyName, CultureInfo? culture, out Typeface typeface)
        {
            typeface = default;
            return false;
        }

        public bool TryGetFamilyTypefaces(string familyName, [NotNullWhen(true)] out IReadOnlyList<Typeface>? familyTypefaces)
        {
            familyTypefaces = null;
            return false;
        }

        public bool TryCreateSyntheticGlyphTypeface(GlyphTypeface glyphTypeface, FontStyle style, FontWeight weight,
            FontStretch stretch, [NotNullWhen(true)] out GlyphTypeface? syntheticGlyphTypeface)
        {
            syntheticGlyphTypeface = null;
            return false;
        }

        public bool TryGetNearestMatch(string familyName, FontStyle style, FontWeight weight, FontStretch stretch,
            [NotNullWhen(true)] out GlyphTypeface? glyphTypeface) =>
            TryGetGlyphTypeface(familyName, style, weight, stretch, out glyphTypeface);

        public int Count => 0;
        public FontFamily this[int index] => throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<FontFamily> GetEnumerator() => Enumerable.Empty<FontFamily>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Dispose() { }
    }

    /// <summary>
    /// A platform typeface that forwards everything to another one except its font file, which it
    /// cannot open, and disposal, since the font manager still owns the other one.
    /// <see cref="IPlatformTypeface"/> is not implementable at compile time, so the implementation
    /// is generated at run time.
    /// </summary>
    public class StreamlessTypeface : DispatchProxy
    {
        private IPlatformTypeface _inner = null!;

        public static IPlatformTypeface Wrap(IPlatformTypeface inner)
        {
            var typeface = Create<IPlatformTypeface, StreamlessTypeface>();
            ((StreamlessTypeface)(object)typeface)._inner = inner;
            return typeface;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IPlatformTypeface.TryGetStream))
            {
                args![0] = null;
                return false;
            }
            return targetMethod.Name == nameof(IDisposable.Dispose) ? null : targetMethod.Invoke(_inner, args);
        }
    }
}
