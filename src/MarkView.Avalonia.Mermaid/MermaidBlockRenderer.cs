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

        CancellationTokenSource? cts = null;

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
            cts?.Dispose();
            var localCts = cts = new CancellationTokenSource();
            var token = localCts.Token;

            var opts = MermaidSvgTheme.GetRenderOptions();

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
            ThemeTracking.ReapplyOnThemeChange(border, () =>
            {
                var newIsDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                textBlock.Inlines!.Clear();
                BuildInlines(textBlock, themeAware, language, newIsDark, lineTexts);
            });
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
