// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

using MermaidRenderOptions = Mermaider.Models.RenderOptions;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// Theme-dependent colours for Mermaid diagrams and the post-processing that bakes them into
/// the SVG produced by Mermaider.
/// </summary>
internal static partial class MermaidSvgTheme
{
    /// <summary>
    /// Builds render options matching the current Avalonia theme variant. Colours come from
    /// MermaidTheme.axaml's <c>MarkdownMermaid*</c> brush resources so users can override them;
    /// the literals here are only a fallback for apps that don't include that theme.
    /// </summary>
    internal static MermaidRenderOptions GetRenderOptions()
    {
        var app = Application.Current;
        var isDark = app?.ActualThemeVariant == ThemeVariant.Dark;
        var theme = app?.ActualThemeVariant ?? ThemeVariant.Light;

        return new MermaidRenderOptions
        {
            Bg = ResolveHex(app, theme, "MarkdownMermaidBackground", isDark ? "#18181B" : "#FFFFFF"),
            Fg = ResolveHex(app, theme, "MarkdownMermaidForeground", isDark ? "#FAFAFA" : "#27272A"),
            Accent = ResolveHex(app, theme, "MarkdownMermaidAccent", isDark ? "#60A5FA" : "#3B82F6"),
            Transparent = false,
        };
    }

    internal static string ResolveHex(Application? app, ThemeVariant theme, string resourceKey, string fallback)
    {
        if (app != null && app.TryGetResource(resourceKey, theme, out var resource) && resource is ISolidColorBrush brush)
        {
            var c = brush.Color;
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        return fallback;
    }

    private readonly record struct Rgba(double R, double G, double B, double A);

    /// <summary>
    /// Resolves the CSS custom properties (<c>var(--name)</c>) and <c>color-mix()</c> expressions in the
    /// SVG produced by Mermaider, so SkiaSharp can render the diagram.
    /// </summary>
    /// <remarks>
    /// SkiaSharp does not implement the CSS cascade: it ignores <c>var()</c> and <c>color-mix()</c>, which
    /// leaves a fill black or a font size at its default. The properties are read from the SVG itself (the
    /// root element's <c>style</c> attribute and the <c>svg { … }</c> rule of its <c>&lt;style&gt;</c> block),
    /// with the colours of <paramref name="opts"/> for any it does not declare, so the result follows the
    /// formulas of whichever Mermaider version produced it. <c>rem</c> lengths become pixels (16px per rem).
    /// An expression that cannot be resolved is left as it is.
    /// </remarks>
    internal static string InlineCssVariables(string svg, MermaidRenderOptions opts)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["--bg"] = opts.Bg ?? "#FFFFFF",
            ["--fg"] = opts.Fg ?? "#27272A",
        };
        AddIfSet(properties, "--accent", opts.Accent);
        AddIfSet(properties, "--muted", opts.Muted);
        AddIfSet(properties, "--line", opts.Line);
        AddIfSet(properties, "--surface", opts.Surface);
        AddIfSet(properties, "--border", opts.Border);

        if (RootStyle().Match(svg) is { Success: true } root)
            AddDeclarations(properties, root.Groups[1].Value);
        if (SvgRule().Match(svg) is { Success: true } rule)
            AddDeclarations(properties, rule.Groups[1].Value);

        return Resolve(svg, properties, 0);

        static void AddIfSet(Dictionary<string, string> properties, string name, string? value)
        {
            if (value is not null)
                properties[name] = value;
        }
    }

    [GeneratedRegex(@"<svg\b[^>]*?\sstyle=""([^""]*)""")]
    private static partial Regex RootStyle();

    [GeneratedRegex(@"(?<![\w-])svg\s*\{([^}]*)\}")]
    private static partial Regex SvgRule();

    private static void AddDeclarations(Dictionary<string, string> properties, string declarations)
    {
        foreach (var declaration in declarations.Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon < 0)
                continue;
            var name = declaration[..colon].Trim();
            if (name.StartsWith("--", StringComparison.Ordinal))
                properties[name] = declaration[(colon + 1)..].Trim();
        }
    }

    /// <summary>
    /// Replaces each <c>var()</c> and <c>color-mix()</c> expression in <paramref name="text"/> by its value.
    /// <paramref name="depth"/> bounds the recursion through properties that refer to each other.
    /// </summary>
    private static string Resolve(string text, IReadOnlyDictionary<string, string> properties, int depth)
    {
        const int MaxDepth = 16;
        StringBuilder? sb = null;
        var i = 0;
        while (true)
        {
            var start = NextFunction(text, i, out var isVar);
            var open = start < 0 ? -1 : text.IndexOf('(', start);
            var close = open < 0 ? -1 : MatchingParenthesis(text, open);
            if (close < 0)
                break;

            var arguments = text[(open + 1)..close];
            var value = depth >= MaxDepth ? null
                : isVar ? ResolveVar(arguments, properties, depth + 1)
                : MixColours(Resolve(arguments, properties, depth + 1));
            sb ??= new StringBuilder(text.Length);
            sb.Append(text, i, start - i).Append(value ?? text[start..(close + 1)]);
            i = close + 1;
        }
        return sb is null ? text : sb.Append(text, i, text.Length - i).ToString();
    }

    private static int NextFunction(string text, int from, out bool isVar)
    {
        var v = text.IndexOf("var(", from, StringComparison.Ordinal);
        var m = text.IndexOf("color-mix(", from, StringComparison.Ordinal);
        isVar = v >= 0 && (m < 0 || v < m);
        return isVar ? v : m;
    }

    private static int MatchingParenthesis(string text, int open)
    {
        var level = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(')
                level++;
            else if (text[i] == ')' && --level == 0)
                return i;
        }
        return -1;
    }

    /// <summary>The value of <c>var(--name)</c> or <c>var(--name, fallback)</c>, or <c>null</c> when it has none.</summary>
    private static string? ResolveVar(string arguments, IReadOnlyDictionary<string, string> properties, int depth)
    {
        var parts = SplitArguments(arguments, maxParts: 2);
        var raw = properties.TryGetValue(parts[0].Trim(), out var declared) ? declared
            : parts.Count > 1 ? parts[1].Trim()
            : null;
        if (raw is null)
            return null;

        var value = Resolve(raw, properties, depth);
        return RemLength().Match(value) is { Success: true } rem
            ? $"{double.Parse(rem.Groups[1].Value, CultureInfo.InvariantCulture) * 16:0.###}px"
            : value;
    }

    [GeneratedRegex(@"^\s*(\d*\.?\d+)rem\s*$")]
    private static partial Regex RemLength();

    /// <summary>
    /// The colour of <c>color-mix(in srgb, a p%, b q%)</c> (the arguments between the parentheses), mixed
    /// with premultiplied alpha as CSS does, or <c>null</c> when an argument is not a colour this understands.
    /// </summary>
    private static string? MixColours(string arguments)
    {
        var parts = SplitArguments(arguments, maxParts: 3);
        if (parts.Count != 3 || parts[0].Trim() != "in srgb"
            || ParseStop(parts[1]) is not var (a, pa) || ParseStop(parts[2]) is not var (b, pb))
            return null;

        // A missing percentage is what the other leaves; percentages that do not add up are scaled.
        var wa = pa ?? 100 - (pb ?? 50);
        var wb = pb ?? 100 - wa;
        var total = wa + wb;
        if (total <= 0)
            return null;
        wa /= total;
        wb /= total;

        var alpha = a.A * wa + b.A * wb;
        if (alpha <= 0)
            return "transparent";
        return Format(new Rgba(
            (a.R * a.A * wa + b.R * b.A * wb) / alpha,
            (a.G * a.A * wa + b.G * b.A * wb) / alpha,
            (a.B * a.A * wa + b.B * b.A * wb) / alpha,
            Math.Min(total, 100) / 100 * alpha));

        static (Rgba Colour, double? Percent)? ParseStop(string stop)
        {
            stop = stop.Trim();
            double? percent = null;
            var space = stop.LastIndexOf(' ');
            if (space > 0 && stop.EndsWith('%')
                && double.TryParse(stop.AsSpan(space + 1, stop.Length - space - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
            {
                percent = p;
                stop = stop[..space].TrimEnd();
            }
            return ParseColour(stop) is { } colour ? (colour, percent) : null;
        }
    }

    private static List<string> SplitArguments(string arguments, int maxParts)
    {
        var parts = new List<string>(maxParts);
        var level = 0;
        var start = 0;
        for (var i = 0; i < arguments.Length && parts.Count < maxParts - 1; i++)
        {
            switch (arguments[i])
            {
                case '(':
                    level++;
                    break;
                case ')':
                    level--;
                    break;
                case ',' when level == 0:
                    parts.Add(arguments[start..i]);
                    start = i + 1;
                    break;
            }
        }
        parts.Add(arguments[start..]);
        return parts;
    }

    private static Rgba? ParseColour(string text)
    {
        if (text == "transparent")
            return new Rgba(0, 0, 0, 0);
        if (!text.StartsWith('#'))
            return null;

        var hex = text[1..];
        if (hex.Length is 3 or 4)
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return null;
        if (hex.Length == 6)
            value = (value << 8) | 0xFF;
        return new Rgba(value >> 24, (value >> 16) & 0xFF, (value >> 8) & 0xFF, (value & 0xFF) / 255.0);
    }

    private static string Format(Rgba c)
    {
        static byte Channel(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);
        var rgb = $"#{Channel(c.R):X2}{Channel(c.G):X2}{Channel(c.B):X2}";
        return c.A >= 1 ? rgb : $"{rgb}{Channel(c.A * 255):X2}";
    }
}
