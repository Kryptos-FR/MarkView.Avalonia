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

    /// <summary>An SVG shaped like Mermaider's: colours on the root element, derived properties in its style block.</summary>
    private static string Svg(string body, string rootStyle = "--bg:#C8AF96;--fg:#644B32;--accent:#3B82F6;background:#C8AF96", string rule = "") =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" style=\"{rootStyle}\">\n<style>\n  text {{ font-family: 'Inter'; }}\n  svg {{\n{rule}\n  }}\n</style>\n{body}</svg>";

    private static string Resolve(string body, string rule = "", MermaidRenderOptions? options = null) =>
        Body(MermaidSvgTheme.InlineCssVariables(Svg(body, rule: rule), options ?? Options()));

    private static string Body(string svg) => svg[(svg.IndexOf("</style>\n", StringComparison.Ordinal) + 9)..^6];

    [Theory]
    [InlineData("var(--fg)", "#644B32")]
    [InlineData("var(--bg)", "#C8AF96")]
    [InlineData("var(--accent)", "#3B82F6")]
    [InlineData("var(--_text)", "#644B32")]
    [InlineData("var(--_soft)", "#7D644B")]
    [InlineData("var(--_tint)", "#BAAAA0")]
    public void InlineCssVariables_resolves_properties_declared_by_the_svg(string expression, string expected)
    {
        const string rule = """
                --_text:  var(--fg);
                --_soft:  color-mix(in srgb, var(--fg) 75%, var(--bg));
                --_tint:  color-mix(in srgb, var(--accent, var(--fg)) 10%, var(--bg));
            """;

        Assert.Equal($"<rect fill=\"{expected}\"/>", Resolve($"<rect fill=\"{expression}\"/>", rule));
    }

    [Fact]
    public void InlineCssVariables_resolves_expressions_inside_the_style_block_and_root_style()
    {
        var result = MermaidSvgTheme.InlineCssVariables(
            Svg("", rootStyle: "--bg:#FFFFFF;--fg:#000000;--line:color-mix(in srgb, #FF0000 50%, var(--fg));background:var(--bg)", rule: "--_line: var(--line);"),
            Options());

        Assert.Contains("--line:#800000;background:#FFFFFF\"", result);
        Assert.Contains("--_line: #800000;", result);
    }

    [Fact]
    public void InlineCssVariables_uses_the_fallback_of_an_undeclared_property()
    {
        const string rule = "--_text-sec: var(--muted, color-mix(in srgb, var(--fg) 50%, var(--bg)));";

        Assert.Equal("#967D64", Resolve("var(--_text-sec)", rule));
    }

    [Fact]
    public void InlineCssVariables_prefers_the_colours_of_the_options_over_fallbacks()
    {
        const string rule = "--_text-sec: var(--muted, #000000); --_line: var(--line, #000000); --_fill: var(--surface, #000000); --_stroke: var(--border, #000000);";
        var options = new MermaidRenderOptions
        {
            Bg = "#C8AF96",
            Fg = "#644B32",
            Muted = "#010203",
            Line = "#0A0B0C",
            Surface = "#0D0E0F",
            Border = "#101112",
        };

        var result = Resolve("var(--_text-sec)|var(--_line)|var(--_fill)|var(--_stroke)", rule, options);

        Assert.Equal("#010203|#0A0B0C|#0D0E0F|#101112", result);
    }

    [Fact]
    public void InlineCssVariables_prefers_the_colours_declared_by_the_svg_over_the_options()
    {
        var result = MermaidSvgTheme.InlineCssVariables(Svg("var(--fg)|var(--accent)", rootStyle: "--fg:#123456;--accent:#ABCDEF"), Options());

        Assert.Equal("#123456|#ABCDEF", Body(result));
    }

    [Fact]
    public void InlineCssVariables_without_declarations_uses_the_options_then_light_defaults()
    {
        Assert.Equal("#C8AF96|#644B32|#3B82F6", MermaidSvgTheme.InlineCssVariables("var(--bg)|var(--fg)|var(--accent)", Options()));
        Assert.Equal(
            "#FFFFFF|#27272A|var(--accent)",
            MermaidSvgTheme.InlineCssVariables("var(--bg)|var(--fg)|var(--accent)", new MermaidRenderOptions { Bg = null, Fg = null, Accent = null }));
    }

    [Theory]
    [InlineData("color-mix(in srgb, #000000 50%, #FFFFFF)", "#808080")]
    [InlineData("color-mix(in srgb, #000000 25%, #FFFFFF 75%)", "#BFBFBF")]
    [InlineData("color-mix(in srgb, #000000 25%, #FFFFFF)", "#BFBFBF")]
    [InlineData("color-mix(in srgb, #000000, #FFFFFF 75%)", "#BFBFBF")]
    [InlineData("color-mix(in srgb, #000000, #FFFFFF)", "#808080")]
    [InlineData("color-mix(in srgb, #000000 50%, #FFFFFF 150%)", "#BFBFBF")]
    [InlineData("color-mix(in srgb, #000 100%, #FFF)", "#000000")]
    [InlineData("color-mix(in srgb, #000000 0%, #FFFFFF)", "#FFFFFF")]
    [InlineData("color-mix(in srgb,#102030 50%,#304050)", "#203040")]
    [InlineData("color-mix(in srgb, #FF0000 12%, transparent)", "#FF00001F")]
    [InlineData("color-mix(in srgb, #FF000080 50%, #0000FF)", "#5500AAC0")]
    [InlineData("color-mix(in srgb, #00F8 50%, #F00)", "#A60059C3")]
    [InlineData("color-mix(in srgb, #000000 20%, #FFFFFF 30%)", "#99999980")]
    [InlineData("color-mix(in srgb, #80402080 50%, #20408040)", "#60404060")]
    [InlineData("color-mix(in srgb, transparent 40%, transparent)", "transparent")]
    public void InlineCssVariables_mixes_colours_in_srgb(string expression, string expected) =>
        Assert.Equal(expected, MermaidSvgTheme.InlineCssVariables(expression, Options()));

    [Theory]
    [InlineData("var(--unknown)")]
    [InlineData("var(--bg")]
    [InlineData("color-mix(in oklch, #000000 50%, #FFFFFF)")]
    [InlineData("color-mix(in srgb, red 50%, #FFFFFF)")]
    [InlineData("color-mix(in srgb, #000000 50%, #FFFFF)")]
    [InlineData("color-mix(in srgb, #000000 50%, #GGGGGG)")]
    [InlineData("color-mix(in srgb, #000000 50%)")]
    [InlineData("color-mix(in srgb, #000000 0%, #FFFFFF 0%)")]
    [InlineData("color-mix(in srgb, #000000 50%, #FFFFFF 50%, #808080)")]
    public void InlineCssVariables_leaves_expressions_it_cannot_resolve(string expression) =>
        Assert.Equal($"<rect fill=\"{expression}\"/>", Resolve($"<rect fill=\"{expression}\"/>"));

    [Fact]
    public void InlineCssVariables_leaves_properties_that_refer_to_each_other()
    {
        const string rule = "--_a: var(--_b); --_b: var(--_a); --_c: color-mix(in srgb, var(--_c) 50%, #FFFFFF);";

        Assert.Equal("var(--_a)", Resolve("var(--_a)", rule)[..9]);
        Assert.EndsWith("#FFFFFF)", Resolve("var(--_c)", rule));
    }

    [Fact]
    public void InlineCssVariables_keeps_every_comma_of_a_fallback() =>
        Assert.Equal("a, b", Resolve("var(--nope, a, b)"));

    [Fact]
    public void InlineCssVariables_ignores_declarations_that_are_not_custom_properties() =>
        Assert.Equal("var(background)", Body(MermaidSvgTheme.InlineCssVariables(Svg("var(background)", rootStyle: "background:#123456"), Options())));

    [Fact]
    public void InlineCssVariables_converts_rem_font_sizes_to_pixels()
    {
        const string rule = "--fs-xs: 0.75rem; --fs-s: 0.875rem; --fs-m:  1rem; --fs-l: 1.125rem; --gap: 2em;";

        var result = Resolve("""<text font-size="var(--fs-xs)"/><text font-size="var(--fs-s)"/><text font-size="var(--fs-m)"/><text font-size="var(--fs-l)"/><g x="var(--gap)"/>""", rule);

        Assert.Equal("""<text font-size="12px"/><text font-size="14px"/><text font-size="16px"/><text font-size="18px"/><g x="2em"/>""", result);
    }

    [Fact]
    public void InlineCssVariables_reads_only_the_svg_rule_and_the_root_style()
    {
        var svg = """<svg style="--fg:#111111"><style>  .tsvg { --_x: #222222; } svg { --_y: #333333; }</style><g style="--_z:#444444"/>var(--_x)|var(--_y)|var(--_z)|var(--fg)</svg>""";

        var result = MermaidSvgTheme.InlineCssVariables(svg, Options());

        Assert.EndsWith("var(--_x)|#333333|var(--_z)|#111111</svg>", result);
    }

    [Theory]
    [InlineData("graph TD\n  A[Start] --> B{Ok?}\n  B -->|yes| C[Done]\n  subgraph S [Group]\n    C\n  end")]
    [InlineData("sequenceDiagram\n  Alice->>Bob: Hi\n  Note over Alice,Bob: note")]
    [InlineData("classDiagram\n  Animal <|-- Dog")]
    [InlineData("erDiagram\n  CUSTOMER ||--o{ ORDER : places")]
    [InlineData("stateDiagram-v2\n  [*] --> Still\n  Still --> [*]")]
    [InlineData("pie title Pets\n  \"Dogs\" : 386\n  \"Cats\" : 85")]
    [InlineData("graph LR\n  A:::success --> B:::failure --> C:::warning --> D:::info")]
    public void InlineCssVariables_leaves_no_css_function_in_mermaider_output(string source)
    {
        foreach (var options in new[] { Options(), new MermaidRenderOptions { Bg = "#18181B", Fg = "#FAFAFA", Accent = "#60A5FA" } })
        {
            var svg = MermaidSvgTheme.InlineCssVariables(Mermaider.MermaidRenderer.RenderSvg(source, options), options);
            var drawing = svg.Remove(svg.IndexOf("<style>", StringComparison.Ordinal), svg.IndexOf("</style>", StringComparison.Ordinal) - svg.IndexOf("<style>", StringComparison.Ordinal));

            Assert.DoesNotContain("var(", drawing);
            Assert.DoesNotContain("color-mix(", drawing);
        }
    }
}
