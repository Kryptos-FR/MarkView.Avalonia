using System.Globalization;
using System.IO;
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
            // Mermaider formats SVG numbers using the thread's current culture.
            // On locales with ',' as decimal separator this corrupts the SVG dimensions
            // (e.g. width="908,16" → invalid SVG, viewBox height becomes 16 trillion px).
            // Force InvariantCulture for the duration of the call and restore afterwards.
            var prevCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            string svg;
            try { svg = MermaidRenderer.RenderSvg(source, GetRenderOptions()); }
            finally { Thread.CurrentThread.CurrentCulture = prevCulture; }
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
            var svgSource = SvgSource.LoadFromStream(stream);

            var image = new Image
            {
                Source = new SvgImage { Source = svgSource },
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            // A ScrollViewer passes infinite available width to its children.
            // Once attached to the visual tree, find the host ScrollViewer and
            // constrain MaxWidth to its Viewport.Width, updating on resize.
            image.AttachedToVisualTree += (_, _) =>
            {
                var sv = image.FindAncestorOfType<ScrollViewer>();
                if (sv is null) return;

                void Update()
                {
                    var w = sv.Viewport.Width;
                    if (w > 0) image.MaxWidth = w;
                }

                void OnSizeChanged(object? s, SizeChangedEventArgs e) => Update();
                sv.SizeChanged += OnSizeChanged;
                image.DetachedFromVisualTree += (_, _) => sv.SizeChanged -= OnSizeChanged;
                Update();
            };

            var border = new Border { Child = image };
            border.Classes.Add("markdown-mermaid");
            renderer.WriteBlock(border);
        }
        catch (Exception ex)
        {
            WriteFallback(renderer, source, ex.Message);
        }
    }

    /// <summary>
    /// Builds <see cref="MermaidRenderOptions"/> matching the current Avalonia theme variant
    /// so the diagram colours look correct on both light and dark backgrounds.
    /// </summary>
    private static MermaidRenderOptions GetRenderOptions()
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        return isDark
            ? new MermaidRenderOptions { Bg = "#18181B", Fg = "#FAFAFA", Accent = "#60a5fa", Transparent = false }
            : new MermaidRenderOptions { Bg = "#FFFFFF", Fg = "#27272A", Accent = "#3b82f6", Transparent = false };
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
