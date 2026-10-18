// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using System.Xml.Linq;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// Checks the GitHub theme's colour pairs against the WCAG 2.2 AA contrast minimums: 4.5:1 for
/// text (1.4.3) and 3:1 for the borders and bars that carry meaning (1.4.11). Colours are read
/// from <c>GitHubTheme.axaml</c>, falling back to <c>MarkdownTheme.axaml</c> for a key it does not
/// redefine, so editing a brush in either file re-runs the check.
/// </summary>
/// <remarks>
/// Decorative lines are left out, as GitHub draws them: the rule under h1 and h2, the line above
/// the footnotes and the copy button's outline. The theme sets its own page colours on
/// <c>MarkdownViewer</c>, so "Page" and "Body" are its <c>MarkdownBackground</c> and
/// <c>MarkdownForeground</c>.
/// </remarks>
public class GitHubThemeContrastTests
{
    private const double Text = 4.5;
    private const double NonText = 3.0;

    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static TheoryData<string, string, string, double> Pairs()
    {
        // (variant, foreground key, background, minimum ratio). A background is a key, or several
        // joined by " over ": translucent brushes composited from the last one up, over the page.
        var data = new TheoryData<string, string, string, double>();
        foreach (var variant in new[] { "Light", "Dark" })
        {
            foreach (var text in new[] { "Body", "MarkdownMutedForeground", "MarkdownLinkForeground" })
            {
                data.Add(variant, text, "Page", Text);
                // Striped table rows hold body text and links
                data.Add(variant, text, "MarkdownTableRowAlternateBackground", Text);
            }
            data.Add(variant, "Body", "MarkdownMarkedBackground", Text);
            data.Add(variant, "MarkdownCodeBlockForeground", "MarkdownCodeBlockBackground", Text);
            data.Add(variant, "MarkdownCodeInlineForeground", "MarkdownCodeInlineBackground", Text);
            data.Add(variant, "MarkdownCodeInlineForeground", "MarkdownCodeInlineBackground over MarkdownTableRowAlternateBackground", Text);
            data.Add(variant, "MarkdownCodeHeaderForeground", "MarkdownCodeHeaderBackground", Text);
            data.Add(variant, "MarkdownCodeLineNumberForeground", "MarkdownCodeBlockBackground", Text);
            // The copy icon under the pointer, drawn in the code's colour by the default theme
            data.Add(variant, "MarkdownCodeBlockForeground", "MarkdownCodeCopyButtonHoverBackground over MarkdownCodeHeaderBackground", NonText);
            data.Add(variant, "MarkdownBlockquoteBorder", "Page", NonText);
            data.Add(variant, "MarkdownTableBorder", "Page", NonText);
            data.Add(variant, "MarkdownTableBorder", "MarkdownTableRowAlternateBackground", NonText);
            data.Add(variant, "MarkdownThematicBreakBackground", "Page", NonText);
            data.Add(variant, "MarkdownKbdForeground", "MarkdownKbdBackground", Text);
            data.Add(variant, "MarkdownKbdBorder", "Page", NonText);
            data.Add(variant, "MarkdownKbdBorder", "MarkdownKbdBackground", NonText);
            foreach (var match in new[] { "MarkdownFindMatchBackground", "MarkdownFindCurrentMatchBackground" })
            {
                // Matched text keeps its own colour
                data.Add(variant, "Body", match, Text);
                data.Add(variant, "MarkdownCodeBlockForeground", match, Text);
                data.Add(variant, "MarkdownCodeInlineForeground", match, Text);
            }
            data.Add(variant, "MarkdownImageFailedForeground", "Page", Text);
            data.Add(variant, "MarkdownImageFailedForeground", "MarkdownImageFailedBackground", Text);
            data.Add(variant, "MarkdownImageFailedBorder", "Page", NonText);
            data.Add(variant, "MarkdownImageLightboxCloseForeground", "MarkdownImageLightboxCloseBackground over MarkdownImageLightboxBackdrop", NonText);
            foreach (var kind in new[] { "Default", "Note", "Tip", "Important", "Warning", "Caution" })
            {
                // The alert title is drawn in the bar's colour on the alert background.
                data.Add(variant, $"MarkdownAlert{kind}Border", $"MarkdownAlert{kind}Background", Text);
                data.Add(variant, "Body", $"MarkdownAlert{kind}Background", Text);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Theme_colour_pair_meets_WCAG_AA(string variant, string foreground, string background, double minimum)
    {
        var brushes = LoadBrushes(variant);
        var page = Resolve(brushes, variant, "Page");
        var back = background.Split(" over ").Reverse()
            .Aggregate(page, (under, key) => Composite(Resolve(brushes, variant, key), under));
        var fore = Composite(Resolve(brushes, variant, foreground), back);

        var ratio = ContrastRatio(fore, back);

        Assert.True(ratio >= minimum,
            $"{variant}: {foreground} ({fore}) on {background} ({back}) is {ratio:F2}:1, below {minimum}:1.");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Every_colour_of_the_default_theme_is_redefined(string variant)
    {
        var defaults = LoadDictionary("MarkdownTheme", variant);
        var gitHub = LoadDictionary("GitHubTheme", variant);

        Assert.NotEmpty(defaults);
        Assert.Empty(defaults.Keys.Except(gitHub.Keys));
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

    private static string Resolve(Dictionary<string, string> brushes, string variant, string key) => key switch
    {
        "Page" => Resolve(brushes, variant, "MarkdownBackground"),
        "Body" => Resolve(brushes, variant, "MarkdownForeground"),
        _ => brushes.TryGetValue(key, out var colour)
            ? colour
            : throw new KeyNotFoundException($"{key} has no literal colour in the {variant} theme dictionaries."),
    };

    /// <summary>The GitHub theme's brushes for <paramref name="variant"/>, over the default theme's.</summary>
    private static Dictionary<string, string> LoadBrushes(string variant)
    {
        var brushes = LoadDictionary("MarkdownTheme", variant);
        foreach (var (key, colour) in LoadDictionary("GitHubTheme", variant))
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
