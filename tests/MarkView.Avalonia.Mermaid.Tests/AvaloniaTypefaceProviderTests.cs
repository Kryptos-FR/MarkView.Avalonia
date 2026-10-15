// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Svg.Skia;

using SkiaSharp;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

public class AvaloniaTypefaceProviderTests
{
    private static readonly FontFamily DocumentFont = new("Document Font");

    // A real font file, so the provider's SKTypeface.FromData has something to parse.
    private static SKData FontData()
    {
        using var stream = SKTypeface.Default.OpenStream();
        return SKData.Create(stream);
    }

    private static string FontName => SKTypeface.Default.FamilyName;

    private sealed class Resolver
    {
        public List<(FontFamily Family, FontWeight Weight, FontStyle Style)> Calls { get; } = [];
        public string FamilyName { get; init; } = FontName;
        public bool Found { get; init; } = true;

        public (string FamilyName, SKData Data)? Resolve(FontFamily family, FontWeight weight, FontStyle style)
        {
            Calls.Add((family, weight, style));
            return Found ? (FamilyName, FontData()) : null;
        }
    }

    private static AvaloniaTypefaceProvider Provider(Resolver resolver, bool anyFamily = false) =>
        new(resolver.Resolve, anyFamily) { DocumentFont = DocumentFont };

    private static SKTypeface? Request(AvaloniaTypefaceProvider provider, string family,
        SKFontStyleWeight weight = SKFontStyleWeight.Normal, SKFontStyleSlant slant = SKFontStyleSlant.Upright) =>
        provider.FromFamilyName(family, weight, SKFontStyleWidth.Normal, slant);

    [AvaloniaFact]
    public void Provides_the_document_font_when_the_css_list_names_it()
    {
        var provider = Provider(new Resolver { FamilyName = "Inter" });

        using var typeface = Request(provider, "'inter', system-ui, sans-serif");

        Assert.NotNull(typeface);
        Assert.Equal(FontName, typeface.FamilyName);
    }

    [AvaloniaTheory]
    [InlineData("Inter")]
    [InlineData("\"Inter\"")]
    [InlineData("system-ui, Inter")]
    public void Matches_single_double_quoted_and_later_names(string family)
    {
        var provider = Provider(new Resolver { FamilyName = "Inter" });

        Assert.NotNull(Request(provider, family));
    }

    [AvaloniaFact]
    public void Leaves_other_families_to_the_svg_renderer()
    {
        var provider = Provider(new Resolver { FamilyName = "Inter" });

        Assert.Null(Request(provider, "'Segoe UI', sans-serif"));
    }

    [AvaloniaFact]
    public void Provides_the_document_font_for_any_family_where_skia_has_no_usable_fonts()
    {
        var provider = Provider(new Resolver { FamilyName = "Inter" }, anyFamily: true);

        using var typeface = Request(provider, "'Segoe UI', sans-serif");

        Assert.NotNull(typeface);
    }

    [AvaloniaFact]
    public void Provides_nothing_without_a_document_font()
    {
        var resolver = new Resolver();
        var provider = new AvaloniaTypefaceProvider(resolver.Resolve, useDocumentFontForAnyFamily: true);

        Assert.Null(Request(provider, FontName));
        Assert.Empty(resolver.Calls);
    }

    [AvaloniaFact]
    public void Provides_nothing_when_avalonia_cannot_resolve_the_document_font()
    {
        var provider = Provider(new Resolver { Found = false }, anyFamily: true);

        Assert.Null(Request(provider, FontName));
    }

    [AvaloniaFact]
    public void Resolves_each_weight_and_slant_once_and_returns_a_new_typeface_each_time()
    {
        var resolver = new Resolver();
        var provider = Provider(resolver);

        using var first = Request(provider, FontName, SKFontStyleWeight.SemiBold, SKFontStyleSlant.Italic);
        using var second = Request(provider, FontName, SKFontStyleWeight.SemiBold, SKFontStyleSlant.Italic);
        using var oblique = Request(provider, FontName, SKFontStyleWeight.Normal, SKFontStyleSlant.Oblique);
        using var upright = Request(provider, FontName);

        Assert.NotNull(first);
        Assert.NotSame(first, second);
        Assert.Equal(
            [
                (DocumentFont, FontWeight.SemiBold, FontStyle.Italic),
                (DocumentFont, FontWeight.Normal, FontStyle.Oblique),
                (DocumentFont, FontWeight.Normal, FontStyle.Normal),
            ],
            resolver.Calls);
    }

    [AvaloniaFact]
    public void Resolves_again_when_the_document_font_changes()
    {
        var resolver = new Resolver();
        var provider = Provider(resolver);
        var other = new FontFamily("Other Font");

        Request(provider, FontName)?.Dispose();
        provider.DocumentFont = other;
        Request(provider, FontName)?.Dispose();

        Assert.Equal([DocumentFont, other], resolver.Calls.Select(c => c.Family));
    }

    [AvaloniaFact]
    public void Use_registers_the_shared_provider_once_in_front_of_the_svg_typeface_providers()
    {
        var font = new FontFamily("Some Font");

        AvaloniaTypefaceProvider.Use(font);
        AvaloniaTypefaceProvider.Use(font);

        var providers = SvgSource.s_skiaModel.Settings.TypefaceProviders!;
        Assert.Same(AvaloniaTypefaceProvider.Shared, providers[0]);
        Assert.Single(providers, p => ReferenceEquals(p, AvaloniaTypefaceProvider.Shared));
        Assert.Equal(font, AvaloniaTypefaceProvider.Shared.DocumentFont);
    }

    [AvaloniaFact]
    public void Shared_provider_reads_fonts_from_the_avalonia_font_manager()
    {
        AvaloniaTypefaceProvider.Use(FontFamily.Default);

        // The headless platform's font manager has no font files to hand over, so nothing is
        // provided; the point is that resolving goes through it without throwing.
        var exception = Record.Exception(() => Request(AvaloniaTypefaceProvider.Shared, "sans-serif")?.Dispose());

        Assert.Null(exception);
    }
}
