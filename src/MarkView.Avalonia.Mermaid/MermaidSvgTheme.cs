// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

using MermaidRenderOptions = Mermaider.Models.RenderOptions;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// Theme-dependent colours for Mermaid diagrams and the post-processing that bakes them into
/// the SVG produced by Mermaider.
/// </summary>
internal static class MermaidSvgTheme
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

    private readonly record struct Rgb(byte R, byte G, byte B);

    /// <summary>
    /// Replaces Mermaider's CSS custom property references (<c>var(--_xxx)</c>) with
    /// computed hex values so SkiaSharp can render the diagram.
    /// </summary>
    /// <remarks>
    /// SkiaSharp does not implement the CSS cascade and silently ignores <c>var()</c> expressions.
    /// </remarks>
    internal static string InlineCssVariables(string svg, MermaidRenderOptions opts)
    {
        var bg = Parse(opts.Bg ?? "#FFFFFF");
        var fg = Parse(opts.Fg ?? "#27272A");
        var acc = Parse(opts.Accent ?? "#3b82f6");
        var mut = opts.Muted is { } m ? Parse(m) : (Rgb?)null;
        var lin = opts.Line is { } l ? Parse(l) : (Rgb?)null;
        var sur = opts.Surface is { } s ? Parse(s) : (Rgb?)null;
        var brd = opts.Border is { } b ? Parse(b) : (Rgb?)null;

        // Mirror the CSS variable formulas from Mermaider's <style> block
        var vars = new (string Token, Rgb Color)[]
        {
            ("var(--_text)",          fg),
            ("var(--_text-sec)",      mut  ?? Mix(fg, 55, bg)),
            ("var(--_text-muted)",    mut  ?? Mix(fg, 35, bg)),
            ("var(--_text-faint)",    Mix(fg, 20, bg)),
            ("var(--_line)",          lin  ?? Mix(fg, 32, bg)),
            ("var(--_arrow)",         acc),
            ("var(--_node-fill)",     sur  ?? Mix(fg,  4, bg)),
            ("var(--_node-stroke)",   brd  ?? Mix(fg, 22, bg)),
            ("var(--_group-fill)",    bg),
            ("var(--_group-hdr)",     Mix(fg,  4, bg)),
            ("var(--_group-stroke)",  Mix(fg, 10, bg)),
            ("var(--_inner-stroke)",  Mix(fg, 10, bg)),
            ("var(--_key-badge)",     Mix(fg,  8, bg)),
            ("var(--_accent-fill)",   Mix(acc,  8, bg)),
            ("var(--_accent-stroke)", Mix(acc, 20, bg)),
            ("var(--_accent-text)",   Mix(acc, 65, bg)),
        };

        var sb = new StringBuilder(svg);
        foreach (var (token, color) in vars)
            sb.Replace(token, Hex(color));

        // Root background: replace the inline style var reference
        sb.Replace("background:var(--bg)", $"background:{Hex(bg)}");

        return sb.ToString();


        static Rgb Parse(string hex)
        {
            hex = hex.TrimStart('#');
            return new Rgb(Convert.ToByte(hex[..2], 16),
                           Convert.ToByte(hex[2..4], 16),
                           Convert.ToByte(hex[4..6], 16));
        }

        // color-mix(in srgb, a N%, b) — linear interpolation in sRGB space
        static Rgb Mix(Rgb a, int aPercent, Rgb b)
        {
            int bp = 100 - aPercent;
            return new Rgb((byte)(a.R * aPercent / 100 + b.R * bp / 100),
                           (byte)(a.G * aPercent / 100 + b.G * bp / 100),
                           (byte)(a.B * aPercent / 100 + b.B * bp / 100));
        }

        static string Hex(Rgb c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
