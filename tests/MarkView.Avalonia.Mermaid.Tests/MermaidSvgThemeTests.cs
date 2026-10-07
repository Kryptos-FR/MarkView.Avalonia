// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

using MarkView.Avalonia.Tests.Shared;

using Xunit;

using MermaidRenderOptions = Mermaider.Models.RenderOptions;

namespace MarkView.Avalonia.Mermaid.Tests;

public class MermaidSvgThemeTests
{
    private static MermaidRenderOptions Options(string? muted = null) => new()
    {
        Bg = "#C8AF96",
        Fg = "#644B32",
        Accent = "#3B82F6",
        Muted = muted,
    };

    [AvaloniaTheory]
    [InlineData(false, "#FFFFFF", "#27272A", "#3B82F6")]
    [InlineData(true, "#18181B", "#FAFAFA", "#60A5FA")]
    public void GetRenderOptions_without_theme_resources_uses_variant_fallbacks(bool dark, string bg, string fg, string accent)
    {
        using var theme = new ThemeScope(dark ? ThemeVariant.Dark : ThemeVariant.Light);

        var options = MermaidSvgTheme.GetRenderOptions();

        Assert.Equal(bg, options.Bg);
        Assert.Equal(fg, options.Fg);
        Assert.Equal(accent, options.Accent);
        Assert.False(options.Transparent);
    }

    [AvaloniaFact]
    public void GetRenderOptions_prefers_solid_brush_theme_resources_over_fallbacks()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var resources = Application.Current!.Resources;
        resources["MarkdownMermaidBackground"] = new SolidColorBrush(Color.Parse("#102030"));
        resources["MarkdownMermaidForeground"] = new SolidColorBrush(Color.Parse("#405060"));
        resources["MarkdownMermaidAccent"] = new SolidColorBrush(Color.Parse("#708090"));
        try
        {
            var options = MermaidSvgTheme.GetRenderOptions();

            Assert.Equal("#102030", options.Bg);
            Assert.Equal("#405060", options.Fg);
            Assert.Equal("#708090", options.Accent);
        }
        finally
        {
            resources.Remove("MarkdownMermaidBackground");
            resources.Remove("MarkdownMermaidForeground");
            resources.Remove("MarkdownMermaidAccent");
        }
    }

    [AvaloniaFact]
    public void GetRenderOptions_resolves_theme_resources_for_the_active_variant()
    {
        using var theme = new ThemeScope(ThemeVariant.Dark);
        var resources = Application.Current!.Resources;
        resources.ThemeDictionaries[ThemeVariant.Dark] = new ResourceDictionary
        {
            ["MarkdownMermaidBackground"] = new SolidColorBrush(Color.Parse("#0A141E")),
        };
        resources.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary
        {
            ["MarkdownMermaidBackground"] = new SolidColorBrush(Color.Parse("#F0E6DC")),
        };
        try
        {
            var options = MermaidSvgTheme.GetRenderOptions();

            Assert.Equal("#0A141E", options.Bg);
        }
        finally
        {
            resources.ThemeDictionaries.Remove(ThemeVariant.Dark);
            resources.ThemeDictionaries.Remove(ThemeVariant.Light);
        }
    }

    [AvaloniaFact]
    public void ResolveHex_ignores_resources_that_are_not_solid_colour_brushes()
    {
        var resources = Application.Current!.Resources;
        resources["TestGradient"] = new LinearGradientBrush();
        try
        {
            Assert.Equal("#ABCDEF", MermaidSvgTheme.ResolveHex(Application.Current, ThemeVariant.Light, "TestGradient", "#ABCDEF"));
        }
        finally
        {
            resources.Remove("TestGradient");
        }
    }

    [Fact]
    public void ResolveHex_without_application_returns_fallback() =>
        Assert.Equal("#ABCDEF", MermaidSvgTheme.ResolveHex(null, ThemeVariant.Light, "MarkdownMermaidBackground", "#ABCDEF"));

    [Theory]
    [InlineData("var(--_text)", "#644B32")]
    [InlineData("var(--_text-sec)", "#91775E")]
    [InlineData("var(--_node-fill)", "#C4AB92")]
    [InlineData("var(--_arrow)", "#3B82F6")]
    [InlineData("var(--_group-fill)", "#C8AF96")]
    [InlineData("var(--_accent-text)", "#6C91D3")]
    public void InlineCssVariables_replaces_token_with_computed_colour(string token, string expected)
    {
        var result = MermaidSvgTheme.InlineCssVariables($"<rect fill=\"{token}\"/>", Options());

        Assert.Equal($"<rect fill=\"{expected}\"/>", result);
    }

    [Fact]
    public void InlineCssVariables_uses_supplied_accent_colour()
    {
        var options = new MermaidRenderOptions { Bg = "#FFFFFF", Fg = "#000000", Accent = "#112233" };

        var result = MermaidSvgTheme.InlineCssVariables("var(--_arrow)", options);

        Assert.Equal("#112233", result);
    }

    [Fact]
    public void InlineCssVariables_replaces_root_background_reference()
    {
        var result = MermaidSvgTheme.InlineCssVariables("<svg style=\"background:var(--bg)\"/>", Options());

        Assert.Equal("<svg style=\"background:#C8AF96\"/>", result);
    }

    [Fact]
    public void InlineCssVariables_uses_explicit_muted_line_surface_and_border_colours()
    {
        var options = new MermaidRenderOptions
        {
            Bg = "#C8AF96",
            Fg = "#644B32",
            Accent = "#3B82F6",
            Muted = "#010203",
            Line = "#0A0B0C",
            Surface = "#0D0E0F",
            Border = "#101112",
        };
        const string svg = "var(--_text-sec)|var(--_text-muted)|var(--_line)|var(--_node-fill)|var(--_node-stroke)";

        var result = MermaidSvgTheme.InlineCssVariables(svg, options);

        Assert.Equal("#010203|#010203|#0A0B0C|#0D0E0F|#101112", result);
    }

    [Fact]
    public void InlineCssVariables_without_colours_falls_back_to_light_defaults()
    {
        var result = MermaidSvgTheme.InlineCssVariables(
            "var(--_text)|var(--_group-fill)|var(--_arrow)",
            new MermaidRenderOptions { Bg = null, Fg = null, Accent = null });

        Assert.Equal("#27272A|#FFFFFF|#3B82F6", result);
    }
}
