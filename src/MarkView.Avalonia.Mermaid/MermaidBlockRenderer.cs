// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Svg.Skia;
using Avalonia.VisualTree;
using Markdig.Syntax;
using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using Mermaider;

using MermaidRenderOptions = Mermaider.Models.RenderOptions;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// Handles all <see cref="FencedCodeBlock"/> nodes.  Mermaid blocks are rendered
/// to SVG via <see cref="MermaidRenderer"/> (pure .NET, no browser required);
/// non-mermaid fenced blocks are rendered as styled code blocks and respect any
/// <see cref="AvaloniaRenderer.CodeHighlighter"/> registered by the SyntaxHighlighting extension.
/// </summary>
public sealed class MermaidBlockRenderer : AvaloniaObjectRenderer<FencedCodeBlock>
{
    protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        if (string.Equals(obj.Info, "mermaid", StringComparison.OrdinalIgnoreCase))
            WriteMermaid(renderer, obj);
        else
            WriteStandardCodeBlock(renderer, obj);
    }

    private static void WriteMermaid(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        var source = ExtractSource(obj);

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var border = new Border { Child = image };
        border.Classes.Add("markdown-mermaid");

        CancellationTokenSource? cts = null;

        // Re-render when the user switches light/dark theme.
        // Standard Avalonia controls update automatically via DynamicResource, but
        // the Mermaid SVG has colours baked in at render time so it must be rebuilt.
        void OnThemeChanged(object? s, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name != nameof(Application.ActualThemeVariant)) return;
            _ = ApplyThemeAsync();
        }
        Application.Current!.PropertyChanged += OnThemeChanged;

        // A ScrollViewer passes infinite available width to its children.
        // Constrain MaxWidth to the viewport width, updating on resize.
        image.AttachedToVisualTree += (_, _) =>
        {
            var sv = image.FindAncestorOfType<ScrollViewer>();
            if (sv is null) return;

            sv.SizeChanged += OnSizeChanged;
            image.DetachedFromLogicalTree += (_, _) =>
            {
                sv.SizeChanged -= OnSizeChanged;
                Application.Current?.PropertyChanged -= OnThemeChanged;
                cts?.Cancel();
                cts?.Dispose();
            };
            Update();

            void Update()
            {
                var w = sv.Viewport.Width;
                if (w > 0) image.MaxWidth = Math.Min(w, 800);
            }

            void OnSizeChanged(object? s, SizeChangedEventArgs e) => Update();
        };

        renderer.WriteBlock(border);
        _ = ApplyThemeAsync();

        // Heavy work (MermaidRenderer.RenderSvg + SvgSource.LoadFromStream via SkiaSharp)
        // is offloaded to a background thread so the UI thread is never blocked.
        // A CancellationTokenSource allows rapid theme switches to cancel in-flight renders.
        async Task ApplyThemeAsync()
        {
            cts?.Cancel();
            cts?.Dispose();
            var localCts = cts = new CancellationTokenSource();
            var token = localCts.Token;

            var opts = GetRenderOptions();

            try
            {
                var svgSource = await Task.Run(() =>
                {
                    var prevCulture = Thread.CurrentThread.CurrentCulture;
                    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                    string svg;
                    try { svg = MermaidRenderer.RenderSvg(source, opts); }
                    finally { Thread.CurrentThread.CurrentCulture = prevCulture; }

                    svg = InlineCssVariables(svg, opts);
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
                    return SvgSource.LoadFromStream(stream);
                }, token);

                if (!token.IsCancellationRequested)
                    image.Source = new SvgImage { Source = svgSource };
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    var panel = new StackPanel { Spacing = 4 };
                    panel.Children.Add(new TextBlock { Text = $"Mermaid render error: {ex.Message}" });
                    panel.Children.Add(new TextBlock { Text = source });
                    border.Child = panel;
                    border.Classes.Clear();
                    border.Classes.Add("markdown-mermaid-fallback");
                }
            }
        }
    }

    /// <summary>
    /// Builds render options matching the current Avalonia theme variant.
    /// </summary>
    private static MermaidRenderOptions GetRenderOptions()
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        return isDark
            ? new MermaidRenderOptions { Bg = "#18181B", Fg = "#FAFAFA", Accent = "#60a5fa", Transparent = false }
            : new MermaidRenderOptions { Bg = "#FFFFFF", Fg = "#27272A", Accent = "#3b82f6", Transparent = false };
    }

    private readonly record struct Rgb(byte R, byte G, byte B);

    /// <summary>
    /// Replaces Mermaider's CSS custom property references (<c>var(--_xxx)</c>) with
    /// computed hex values so SkiaSharp can render the diagram.
    /// </summary>
    /// <remarks>
    /// SkiaSharp does not implement the CSS cascade and silently ignores <c>var()</c> expressions.
    /// </remarks>
    private static string InlineCssVariables(string svg, MermaidRenderOptions opts)
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

    private static string ExtractSource(FencedCodeBlock block)
    {
        if (block.Lines.Lines == null)
            return string.Empty;

        var lines = block.Lines;
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(lines.Lines[i].Slice.AsSpan());
        }
        return sb.ToString();
    }

    private static void WriteStandardCodeBlock(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        var language = obj.Info;
        var textBlock = new TextBlock { TextWrapping = TextWrapping.NoWrap };
        var border = new Border { Child = textBlock };
        border.Classes.Add("markdown-code-block");

        if (!string.IsNullOrEmpty(language))
            border.Classes.Add($"language-{language}");

        if (obj.Lines.Lines == null)
        {
            renderer.WriteBlock(border);
            return;
        }

        // Materialise source lines once so they can be reused on theme change.
        // AsMemory() references the Markdig source buffer directly — no per-line allocation.
        var lineTexts = new List<ReadOnlyMemory<char>>(obj.Lines.Count);
        for (int i = 0; i < obj.Lines.Count; i++)
        {
            var slice = obj.Lines.Lines[i].Slice;
            lineTexts.Add(slice.Text.AsMemory(slice.Start, slice.Length));
        }

        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        BuildInlines(textBlock, renderer.CodeHighlighter, language, isDark, lineTexts);

        // If the highlighter is theme-aware, rebuild only TextBlock.Inlines when the
        // theme changes — the Border and its position in the document stay untouched.
        if (renderer.CodeHighlighter is IThemeAwareCodeHighlighter themeAware)
        {
            void OnThemeChanged(object? s, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property.Name != nameof(Application.ActualThemeVariant)) return;
                var newIsDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                textBlock.Inlines!.Clear();
                BuildInlines(textBlock, themeAware, language, newIsDark, lineTexts);
            }
            Application.Current!.PropertyChanged += OnThemeChanged;
            border.DetachedFromLogicalTree += (_, _) => Application.Current?.PropertyChanged -= OnThemeChanged;
        }

        renderer.WriteBlock(border);
    }

    private static void BuildInlines(
        TextBlock textBlock,
        ICodeHighlighter? highlighter,
        string? language,
        bool isDark,
        IReadOnlyList<ReadOnlyMemory<char>> lineTexts)
    {
        for (int i = 0; i < lineTexts.Count; i++)
        {
            if (i > 0) textBlock.Inlines!.Add(new LineBreak());

            var lineText = lineTexts[i];
            var tokens = highlighter is IThemeAwareCodeHighlighter th
                ? th.HighlightVariant(lineText, language, isDark)
                : highlighter?.Highlight(lineText, language);

            if (tokens != null)
            {
                foreach (var (text, foreground) in tokens)
                {
                    var run = new Run(text);
                    if (foreground != null)
                        run.Foreground = foreground;
                    textBlock.Inlines!.Add(run);
                }
            }
            else
            {
                textBlock.Inlines!.Add(new Run(lineText.ToString()));
            }
        }
    }
}
