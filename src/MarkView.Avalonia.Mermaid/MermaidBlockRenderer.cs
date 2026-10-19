// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
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
/// While <see cref="AvaloniaRenderer.IsProvisional"/> holds for a block (it contains the end of the text
/// being streamed), a Mermaid fence is rendered as a <c>mermaid</c> code block and no diagram render is
/// scheduled; it renders as a diagram once a blank line or another block follows it.
/// With <see cref="MarkdownViewer.IsImageZoomEnabled"/>, clicking a rendered diagram opens it in the viewer's
/// lightbox, as an image, where it is fitted to the view and can be zoomed and panned.
/// </summary>
/// <param name="style">The visual style diagrams are painted in.</param>
public sealed class MermaidBlockRenderer(MermaidStyle style = MermaidStyle.Quiet) : AvaloniaObjectRenderer<FencedCodeBlock>
{
    /// <summary>The visual style diagrams are painted in.</summary>
    public MermaidStyle Style { get; } = style;

    protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        if (string.Equals(obj.Info, "mermaid", StringComparison.OrdinalIgnoreCase) && !renderer.IsProvisional(obj))
            WriteMermaid(renderer, obj, Style);
        else
            WriteStandardCodeBlock(renderer, obj);
    }

    private static void WriteMermaid(AvaloniaRenderer renderer, FencedCodeBlock obj, MermaidStyle style)
    {
        var source = ExtractSource(obj);

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        // The diagram's source is its only text equivalent.
        AutomationProperties.SetName(image, source);

        var border = new Border { Child = image };
        border.Classes.Add("markdown-mermaid");
        MarkdownSelection.SetCopyText(border, $"```mermaid\n{source}\n```");

        // A rendered diagram opens in the viewer's lightbox, where it can be zoomed and panned. It is
        // drawn as vectors, so the lightbox may enlarge it to fit.
        if (renderer.IsImageZoomEnabled)
            ImageZoom.MakeZoomable(renderer, image, enlargeToFit: true);

        CancellationTokenSource? cts = null;
        var started = false;

        // Rendered images by the colours baked into them, so switching back to a theme
        // already shown reuses its image instead of rendering and parsing the SVG again.
        var rendered = new Dictionary<(string? Bg, string? Fg, string? Accent), SvgImage>();

        // The Mermaid SVG has colours baked in at render time, so it must be rebuilt
        // when the user switches light/dark theme. A diagram not rendered yet picks up
        // the theme in effect when it first renders.
        ThemeTracking.ReapplyOnThemeChange(border, () =>
        {
            if (started) _ = ApplyThemeAsync();
        });

        // Rendering waits until the diagram first comes within a screen of the viewport, so
        // diagrams further down a long document cost nothing while it loads. This matters
        // most in the browser, where Task.Run below shares the UI thread.
        border.EffectiveViewportChanged += OnEffectiveViewportChanged;

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

        void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            if (!IsNearViewport(border.Bounds.Size, e.EffectiveViewport)) return;
            border.EffectiveViewportChanged -= OnEffectiveViewportChanged;
            started = true;
            _ = ApplyThemeAsync();
        }

        // Heavy work (MermaidRenderer.RenderSvg + SvgSource.LoadFromStream via SkiaSharp)
        // is offloaded to a background thread so the UI thread is not blocked on platforms
        // with threads; single-threaded WebAssembly still runs it on the UI thread.
        // A CancellationTokenSource lets a theme switch supersede an in-flight render.
        async Task ApplyThemeAsync()
        {
            cts?.Cancel();
            // Stryker disable once Statement : a CancellationTokenSource without timers or linked tokens holds no resources
            cts?.Dispose();
            var localCts = cts = new CancellationTokenSource();
            var token = localCts.Token;

            var opts = MermaidSvgTheme.GetRenderOptions(style);
            var colours = (opts.Bg, opts.Fg, opts.Accent);
            if (rendered.TryGetValue(colours, out var cached))
            {
                ShowDiagram(cached);
                return;
            }

            // Diagram text is drawn in the document's font, resolved by Avalonia, rather than with
            // whatever SkiaSharp finds on its own (only a monospace font in the browser).
            AvaloniaTypefaceProvider.Use(border.GetValue(TextElement.FontFamilyProperty));

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
                    ShowDiagram(rendered[colours] = new SvgImage { Source = svgSource });
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

            // The diagram is transparent, so the lightbox paints it on its background colour, set
            // before the source the lightbox follows.
            void ShowDiagram(SvgImage diagram)
            {
                ImageZoom.SetBackground(image, new ImmutableSolidColorBrush(Color.Parse(opts.Bg!)));
                image.Source = diagram;
            }
        }
    }

    // The viewport is in the element's own coordinates; it is widened by its own height so a
    // diagram starts rendering a screen before it scrolls into view. Edges count as touching,
    // since a diagram not rendered yet may have no height at all.
    private static bool IsNearViewport(Size size, Rect viewport)
    {
        var area = viewport.Inflate(new Thickness(0, viewport.Height));
        return size.Width >= area.Left && area.Right >= 0
            && size.Height >= area.Top && area.Bottom >= 0;
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

        CodeBlockToolbar.Write(renderer, border, obj, language);
    }
}
