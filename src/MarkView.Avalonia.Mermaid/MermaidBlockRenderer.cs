// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

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
using MarkView.Avalonia.Rendering.Blocks;
using Mermaider;

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
        MarkdownSelection.SetCopyText(border, $"```mermaid\n{source}\n```");

        CancellationTokenSource? cts = null;

        // Rendered images by the colours baked into them, so switching back to a theme
        // already shown reuses its image instead of rendering and parsing the SVG again.
        var rendered = new Dictionary<(string? Bg, string? Fg, string? Accent), SvgImage>();

        // The Mermaid SVG has colours baked in at render time, so it must be rebuilt
        // when the user switches light/dark theme.
        ThemeTracking.ReapplyOnThemeChange(border, () => _ = ApplyThemeAsync());

        // A ScrollViewer passes infinite available width to its children.
        // Constrain MaxWidth to the viewport width, updating on resize.
        image.AttachedToVisualTree += (_, _) =>
        {
            var sv = image.FindAncestorOfType<ScrollViewer>();
            if (sv is null) return;

            sv.SizeChanged += OnSizeChanged;
            image.DetachedFromLogicalTree += (_, _) => sv.SizeChanged -= OnSizeChanged;
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
        // A CancellationTokenSource lets a theme switch supersede an in-flight render.
        async Task ApplyThemeAsync()
        {
            cts?.Cancel();
            // Stryker disable once Statement : a CancellationTokenSource without timers or linked tokens holds no resources
            cts?.Dispose();
            var localCts = cts = new CancellationTokenSource();
            var token = localCts.Token;

            var opts = MermaidSvgTheme.GetRenderOptions();
            var colours = (opts.Bg, opts.Fg, opts.Accent);
            if (rendered.TryGetValue(colours, out var cached))
            {
                image.Source = cached;
                return;
            }

            try
            {
                var svgSource = await Task.Run(() =>
                {
                    var svg = MermaidRenderer.RenderSvg(source, opts);
                    svg = MermaidSvgTheme.InlineCssVariables(svg, opts);
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
                    return SvgSource.LoadFromStream(stream);
                }, token);

                if (!token.IsCancellationRequested)
                    image.Source = rendered[colours] = new SvgImage { Source = svgSource };
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

        HighlightedCode.Write(border, textBlock, obj, language, renderer.CodeHighlighter);

        renderer.WriteBlock(border);
    }
}
