// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using System.Xml.Linq;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// Checks the colour pairs of every theme preset against the WCAG 2.2 AA contrast minimums: 4.5:1
/// for text (1.4.3) and 3:1 for the borders and bars that carry meaning (1.4.11), and its
/// high-contrast palette against 7:1. Colours are read from the preset's <c>.axaml</c> file, falling
/// back to <c>MarkdownTheme.axaml</c> for a key it does not redefine, so editing a brush in either
/// file re-runs the check.
/// </summary>
/// <remarks>
/// Decorative lines are left out: the rule under or above headings, the line above the footnotes,
/// the outline of code blocks and of the copy button. A preset that sets its own page colours on
/// <c>MarkdownViewer</c> (<c>MarkdownBackground</c>, <c>MarkdownForeground</c>) is checked on them;
/// one that leaves the page to the host, as the Chat preset does, is checked on each of
/// <see cref="HostSurfaces"/>, with Fluent's text colour as the body text.
/// </remarks>
public class ThemePresetContrastTests
{
    private const double Text = 4.5;
    private const double NonText = 3.0;
    // High contrast: WCAG 2.2 AAA enhanced contrast (1.4.6), which Windows' contrast themes meet.
    private const double HighContrast = 7.0;

    private static readonly string[] Presets = ["GitHubTheme", "ChatTheme"];

    // Pages and chat bubbles a preset without its own background is drawn on: Fluent's window, and
    // the neutral bubbles of chat applications. Body text is Fluent's.
    private static readonly Dictionary<string, (string Page, string Body)[]> HostSurfaces = new()
    {
        ["Light"] = [("#FFFFFF", "#000000"), ("#F4F4F5", "#000000")],
        ["Dark"] = [("#000000", "#FFFFFF"), ("#18181B", "#FFFFFF"), ("#2F2F2F", "#FFFFFF")],
    };

    // Keys only some presets define: a pair naming one is checked where the preset defines it.
    private static readonly HashSet<string> OptionalKeys =
    [
        "MarkdownMutedForeground",
        "MarkdownTableRowAlternateBackground",
        "MarkdownTableHeaderBackground",
        "MarkdownThematicBreakBackground",
    ];

    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static TheoryData<string, string, string, string, double> Pairs()
    {
        // (preset, variant, foreground key, background, minimum ratio). A background is a key, or
        // several joined by " over ": translucent brushes composited from the last one up, over the page.
        var data = new TheoryData<string, string, string, string, double>();
        foreach (var preset in Presets)
        {
            foreach (var variant in new[] { "Light", "Dark" })
            {
                var keys = LoadBrushes(preset, variant).Keys.ToHashSet();
                foreach (var (foreground, background, minimum) in PairsOf())
                {
                    var named = background.Split(" over ").Append(foreground);
                    if (named.All(key => !OptionalKeys.Contains(key) || keys.Contains(key)))
                        data.Add(preset, variant, foreground, background, minimum);
                }
            }
        }
        return data;
    }

    private static IEnumerable<(string Foreground, string Background, double Minimum)> PairsOf()
    {
        foreach (var text in new[] { "Body", "MarkdownMutedForeground", "MarkdownLinkForeground" })
        {
            yield return (text, "Page", Text);
            // Striped table rows and the header row hold body text and links
            yield return (text, "MarkdownTableRowAlternateBackground", Text);
            yield return (text, "MarkdownTableHeaderBackground", Text);
        }
        yield return ("Body", "MarkdownMarkedBackground", Text);
        yield return ("MarkdownCodeBlockForeground", "MarkdownCodeBlockBackground", Text);
        yield return ("MarkdownCodeInlineForeground", "MarkdownCodeInlineBackground", Text);
        yield return ("MarkdownCodeInlineForeground", "MarkdownCodeInlineBackground over MarkdownTableRowAlternateBackground", Text);
        yield return ("MarkdownCodeInlineForeground", "MarkdownCodeInlineBackground over MarkdownTableHeaderBackground", Text);
        yield return ("MarkdownCodeHeaderForeground", "MarkdownCodeHeaderBackground", Text);
        yield return ("MarkdownCodeLineNumberForeground", "MarkdownCodeBlockBackground", Text);
        // The copy icon under the pointer, drawn in the code's colour by the default theme
        yield return ("MarkdownCodeBlockForeground", "MarkdownCodeCopyButtonHoverBackground over MarkdownCodeHeaderBackground", NonText);
        yield return ("MarkdownBlockquoteBorder", "Page", NonText);
        yield return ("MarkdownTableBorder", "Page", NonText);
        yield return ("MarkdownTableBorder", "MarkdownTableRowAlternateBackground", NonText);
        yield return ("MarkdownTableBorder", "MarkdownTableHeaderBackground", NonText);
        yield return ("MarkdownThematicBreakBackground", "Page", NonText);
        yield return ("MarkdownKbdForeground", "MarkdownKbdBackground", Text);
        yield return ("MarkdownKbdBorder", "Page", NonText);
        yield return ("MarkdownKbdBorder", "MarkdownKbdBackground", NonText);
        foreach (var match in new[] { "MarkdownFindMatchBackground", "MarkdownFindCurrentMatchBackground" })
        {
            // Matched text keeps its own colour
            yield return ("Body", match, Text);
            yield return ("MarkdownCodeBlockForeground", match, Text);
            yield return ("MarkdownCodeInlineForeground", match, Text);
            yield return ("MarkdownCodeInlineForeground", $"MarkdownCodeInlineBackground over {match}", Text);
        }
        yield return ("MarkdownImageFailedForeground", "Page", Text);
        yield return ("MarkdownImageFailedForeground", "MarkdownImageFailedBackground", Text);
        yield return ("MarkdownImageFailedBorder", "Page", NonText);
        yield return ("MarkdownImageLightboxCloseForeground", "MarkdownImageLightboxCloseBackground over MarkdownImageLightboxBackdrop", NonText);
        foreach (var kind in new[] { "Default", "Note", "Tip", "Important", "Warning", "Caution" })
        {
            // The alert title is drawn in the bar's colour on the alert background.
            yield return ($"MarkdownAlert{kind}Border", $"MarkdownAlert{kind}Background", Text);
            yield return ("Body", $"MarkdownAlert{kind}Background", Text);
        }
        // The high-contrast palette (MarkdownViewer:high-contrast) replaces the page colours.
        yield return ("MarkdownHighContrastForeground", "MarkdownHighContrastBackground", HighContrast);
        yield return ("MarkdownHighContrastLink", "MarkdownHighContrastBackground", HighContrast);
        yield return ("MarkdownHighContrastBorder", "MarkdownHighContrastBackground", HighContrast);
        yield return ("MarkdownHighContrastHighlightForeground", "MarkdownHighContrastHighlightBackground", HighContrast);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Theme_colour_pair_meets_WCAG_AA(string preset, string variant, string foreground, string background, double minimum)
    {
        var brushes = LoadBrushes(preset, variant);
        var failures = new List<string>();
        foreach (var surface in Surfaces(brushes, variant))
        {
            var back = background.Split(" over ").Reverse()
                .Aggregate(surface.Page, (under, key) => Composite(Resolve(brushes, surface, key), under));
            var fore = Composite(Resolve(brushes, surface, foreground), back);

            var ratio = ContrastRatio(fore, back);
            if (ratio < minimum)
                failures.Add($"{foreground} ({fore}) on {background} ({back}) over page {surface.Page} is {ratio:F2}:1");
        }

        Assert.True(failures.Count == 0, $"{preset} {variant}, below {minimum}:1: " + string.Join("; ", failures));
    }

    [Theory]
    [MemberData(nameof(PresetVariants))]
    public void Every_colour_of_the_default_theme_is_redefined(string preset, string variant)
    {
        var defaults = LoadDictionary("MarkdownTheme", variant);
        var redefined = LoadDictionary(preset, variant);

        Assert.NotEmpty(defaults);
        Assert.Empty(defaults.Keys.Except(redefined.Keys));
    }

    public static TheoryData<string, string> PresetVariants()
    {
        var data = new TheoryData<string, string>();
        foreach (var preset in Presets)
        {
            data.Add(preset, "Light");
            data.Add(preset, "Dark");
        }
        return data;
    }

    [Theory]
    [InlineData("#000000", "#FFFFFF", 21.0)]
    [InlineData("#FFFFFF", "#FFFFFF", 1.0)]
    [InlineData("#0078D7", "#FFFFFF", 4.50)]
    public void ContrastRatio_matches_the_WCAG_formula(string a, string b, double expected)
    {
        Assert.Equal(expected, ContrastRatio(a, b), 2);
        Assert.Equal(expected, ContrastRatio(b, a), 2);
    }

    [Fact]
    public void Composite_blends_a_translucent_colour_over_its_background()
    {
        Assert.Equal("#808080", Composite("#80FFFFFF", "#000000"));
        Assert.Equal("#123456", Composite("#123456", "#FFFFFF"));
        Assert.Equal("#FFFFFF", Composite("#00000000", "#FFFFFF"));
    }

    /// <summary>The preset's own page colours, or the host surfaces when it has none.</summary>
    private static IEnumerable<(string Page, string Body)> Surfaces(Dictionary<string, string> brushes, string variant) =>
        brushes.TryGetValue("MarkdownBackground", out var page)
            ? [(page, brushes["MarkdownForeground"])]
            : HostSurfaces[variant];

    private static string Resolve(Dictionary<string, string> brushes, (string Page, string Body) surface, string key) => key switch
    {
        "Page" => surface.Page,
        "Body" => surface.Body,
        _ => brushes.TryGetValue(key, out var colour)
            ? colour
            : throw new KeyNotFoundException($"{key} has no literal colour in the theme dictionaries."),
    };

    /// <summary>The preset's brushes for <paramref name="variant"/>, over the default theme's.</summary>
    private static Dictionary<string, string> LoadBrushes(string preset, string variant)
    {
        var brushes = LoadDictionary("MarkdownTheme", variant);
        foreach (var (key, colour) in LoadDictionary(preset, variant))
            brushes[key] = colour;
        return brushes;
    }

    private static Dictionary<string, string> LoadDictionary(string theme, string variant)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "MarkView.Avalonia", "Themes", theme + ".axaml");
        var dictionary = XDocument.Load(path)
            .Descendants(Avalonia + "ResourceDictionary")
            .Single(d => (string?)d.Attribute(X + "Key") == variant);
        return dictionary.Elements(Avalonia + "SolidColorBrush")
            .Where(b => b.Value.StartsWith('#'))
            .ToDictionary(b => (string)b.Attribute(X + "Key")!, b => b.Value.Trim());
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }

    /// <summary>Blends <paramref name="colour"/> (#RRGGBB or #AARRGGBB) over an opaque background.</summary>
    private static string Composite(string colour, string background)
    {
        var (alpha, fore) = Parse(colour);
        var (_, back) = Parse(background);
        var mixed = fore.Zip(back, (f, b) => (int)Math.Round(alpha * f + (1 - alpha) * b));
        return "#" + string.Concat(mixed.Select(c => c.ToString("X2", CultureInfo.InvariantCulture)));
    }

    /// <summary>The WCAG 2.x contrast ratio between two opaque colours.</summary>
    private static double ContrastRatio(string a, string b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double RelativeLuminance(string colour)
    {
        var channels = Parse(colour).Rgb.Select(c =>
        {
            var v = c / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }).ToArray();
        return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
    }

    private static (double Alpha, int[] Rgb) Parse(string colour)
    {
        var hex = colour.TrimStart('#');
        if (hex.Length == 6)
            hex = "FF" + hex;
        int Byte(int i) => int.Parse(hex.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (Byte(0) / 255.0, [Byte(2), Byte(4), Byte(6)]);
    }
}
