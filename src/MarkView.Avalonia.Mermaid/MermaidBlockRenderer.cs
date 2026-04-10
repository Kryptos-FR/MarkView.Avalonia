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

using Mermaider;

using MermaidRenderOptions = Mermaider.Models.RenderOptions;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// Handles all <see cref="FencedCodeBlock"/> nodes.  Mermaid blocks are rendered
/// to SVG via <see cref="MermaidRenderer"/> (pure .NET, no browser required);
/// non-mermaid fenced blocks are rendered as styled code blocks and respect any
/// <see cref="AvaloniaRenderer.CodeHighlighter"/> registered by the SyntaxHighlighting extension.
/// </summary>
public class MermaidBlockRenderer : AvaloniaObjectRenderer<FencedCodeBlock>
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
        try
        {
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            ApplyTheme();

            // Re-render when the user switches light/dark theme.
            // Standard Avalonia controls update automatically via DynamicResource, but
            // the Mermaid SVG has colours baked in at render time so it must be rebuilt.
            void OnThemeChanged(object? s, AvaloniaPropertyChangedEventArgs e) => ApplyTheme();
            Application.Current!.PropertyChanged += OnThemeChanged;

            // A ScrollViewer passes infinite available width to its children.
            // Constrain MaxWidth to the viewport width, updating on resize.
            image.AttachedToVisualTree += (_, _) =>
            {
                var sv = image.FindAncestorOfType<ScrollViewer>();
                if (sv is null) return;

                sv.SizeChanged += OnSizeChanged;
                image.DetachedFromVisualTree += (_, _) =>
                {
                    sv.SizeChanged -= OnSizeChanged;
                    Application.Current?.PropertyChanged -= OnThemeChanged;
                };
                Update();

                void Update()
                {
                    var w = sv.Viewport.Width;
                    if (w > 0) image.MaxWidth = w;
                }

                void OnSizeChanged(object? s, SizeChangedEventArgs e) => Update();
            };

            var border = new Border { Child = image };
            border.Classes.Add("markdown-mermaid");
            renderer.WriteBlock(border);

            void ApplyTheme()
            {
                var opts = GetRenderOptions();
                var prevCulture = Thread.CurrentThread.CurrentCulture;
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                string svg;
                try { svg = MermaidRenderer.RenderSvg(source, opts); }
                finally { Thread.CurrentThread.CurrentCulture = prevCulture; }

                svg = InlineCssVariables(svg, opts);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
                image.Source = new SvgImage { Source = SvgSource.LoadFromStream(stream) };
            }
        }
        catch (Exception ex)
        {
            WriteFallback(renderer, source, ex.Message);
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
        return string.Join("\n",
            Enumerable.Range(0, lines.Count)
                      .Select(i => lines.Lines[i].Slice.ToString()));
    }

    private static void WriteFallback(AvaloniaRenderer renderer, string source, string? errorMessage = null)
    {
        var panel = new StackPanel { Spacing = 4 };
        if (errorMessage != null)
            panel.Children.Add(new TextBlock { Text = $"Mermaid render error: {errorMessage}" });
        panel.Children.Add(new TextBlock { Text = source });

        var border = new Border { Child = panel };
        border.Classes.Add("markdown-mermaid-fallback");

        renderer.WriteBlock(border);
    }

    private static void WriteStandardCodeBlock(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        var language = obj.Info;
        var textBlock = new TextBlock { TextWrapping = TextWrapping.NoWrap };
        var border = new Border { Child = textBlock };
        border.Classes.Add("markdown-code-block");

        if (!string.IsNullOrEmpty(language))
            border.Classes.Add($"language-{language}");

        if (obj.Lines.Lines != null)
        {
            var lines = obj.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                    textBlock.Inlines!.Add(new LineBreak());

                var lineText = lines.Lines[i].Slice.ToString();
                var tokens = renderer.CodeHighlighter?.Highlight(lineText, language);

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
                    textBlock.Inlines!.Add(new Run(lineText));
                }
            }
        }

        renderer.WriteBlock(border);
    }
}
