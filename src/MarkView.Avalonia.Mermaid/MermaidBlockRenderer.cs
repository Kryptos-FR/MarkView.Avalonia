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
/// A diagram renders as soon as it comes within a screen of the viewport. Where threads are available (not in the
/// browser), every diagram also starts rendering in the background once it is in a window, so that scrolling
/// to it later costs nothing.
/// With <see cref="MarkdownViewer.IsImageZoomEnabled"/>, clicking a rendered diagram opens it in the viewer's
/// lightbox, as an image, where it is fitted to the view and can be zoomed and panned.
/// </summary>
/// <param name="style">The visual style diagrams are painted in.</param>
public sealed class MermaidBlockRenderer(MermaidStyle style = MermaidStyle.Quiet) : AvaloniaObjectRenderer<FencedCodeBlock>
{
    /// <summary>The visual style diagrams are painted in.</summary>
    public MermaidStyle Style { get; } = style;

    /// <summary>
    /// Whether diagrams start rendering in the background once they are in a window, rather than only when they come
    /// near the viewport. On by default except in the browser, where background work shares the UI thread.
    /// </summary>
    internal bool RendersInBackground { get; init; } = DefaultRendersInBackground;

    internal static bool DefaultRendersInBackground => !OperatingSystem.IsBrowser();

    protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        if (string.Equals(obj.Info, "mermaid", StringComparison.OrdinalIgnoreCase) && !renderer.IsProvisional(obj))
            WriteMermaid(renderer, obj, Style, RendersInBackground);
        else
            WriteStandardCodeBlock(renderer, obj);
    }

    private static void WriteMermaid(AvaloniaRenderer renderer, FencedCodeBlock obj, MermaidStyle style, bool inBackground)
    {
        var source = ExtractSource(obj);

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        // The diagram's source is its only text equivalent.
        AutomationProperties.SetName(image, source);
        // Can be saved, as SVG or PNG.
        ImageExport.SetFileName(image, "diagram");

        var border = new Border { Child = image };
        border.Classes.Add("markdown-mermaid");
        renderer.ApplyTheme(border, "MarkdownMermaidTheme");
        MarkdownSelection.SetCopyText(border, $"```mermaid\n{source}\n```");

        // A rendered diagram opens in the viewer's lightbox, where it can be zoomed and panned. It is
        // drawn as vectors, so the lightbox may enlarge it to fit.
        if (renderer.IsImageZoomEnabled)
            ImageZoom.MakeZoomable(renderer, image, enlargeToFit: true);

        CancellationTokenSource? cts = null;
        var started = false;
        var abandoned = false;

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

        // A diagram removed from the document (a new render, a streaming update that rebuilds it) gives up
        // the render it waits for, which still runs if another diagram waits for it too; put back, it asks again.
        border.DetachedFromLogicalTree += (_, _) =>
        {
            if (cts is null) return;
            abandoned = true;
            cts.Cancel();
        };
        border.AttachedToLogicalTree += (_, _) =>
        {
            if (!abandoned) return;
            abandoned = false;
            _ = ApplyThemeAsync();
        };

        // A diagram that appears after the document has been laid out moves everything below it, which costs a
        // relayout of the document each. Rendered in the background from the moment the document is attached,
        // the diagrams are mostly ready by the time the document is first laid out. In the browser, where
        // Task.Run below shares the UI thread, lazy rendering waits instead until the diagram first comes
        // within a screen of the viewport, so diagrams further down a long document cost nothing while it loads.
        if (inBackground || !renderer.IsLazyRenderingEnabled)
            border.AttachedToVisualTree += (_, _) => Start();
        else
            ViewportTracking.RunWhenNearViewport(border, Start);

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

        void Start()
        {
            // Attached again after a theme switch, the diagram is re-rendered by the theme tracking; starting
            // a diagram already rendering would cancel that render and render it again.
            // Stryker disable once all : see above
            if (started) return;
            started = true;
            _ = ApplyThemeAsync();
        }

        // Heavy work (MermaidRenderer.RenderSvg + SvgSource.LoadFromStream via SkiaSharp)
        // is offloaded to a background thread so the UI thread is not blocked on platforms
        // with threads; single-threaded WebAssembly still runs it on the UI thread. Diagrams
        // rendered before, by any document, come from the shared cache.
        // A CancellationTokenSource lets a theme switch supersede an in-flight render.
        async Task ApplyThemeAsync()
        {
            cts?.Cancel();
            var localCts = cts = new CancellationTokenSource();
            var token = localCts.Token;

            var opts = MermaidSvgTheme.GetRenderOptions(style);
            var colours = (opts.Bg, opts.Fg, opts.Accent);
            if (rendered.TryGetValue(colours, out var cached))
            {
                cts = null;
                ShowDiagram(cached);
                return;
            }

            // Diagram text is drawn in the document's font, resolved by Avalonia, rather than with
            // whatever SkiaSharp finds on its own (only a monospace font in the browser).
            var font = border.GetValue(TextElement.FontFamilyProperty);

            try
            {
                var result = await MermaidDiagramCache.Shared.GetOrRenderAsync(
                    new MermaidDiagramCache.Key(source, opts.Bg, opts.Fg, opts.Accent, style, font),
                    () =>
                    {
                        var svg = MermaidRenderer.RenderSvg(source, opts);
                        svg = MermaidSvgTheme.InlineCssVariables(svg, opts);
                        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
                        // A saved diagram is written on its background colour, which the image shown leaves
                        // to the document.
                        return new RenderedDiagram(AvaloniaTypefaceProvider.WithDocumentFont(font, () => SvgSource.LoadFromStream(stream)),
                            new Lazy<byte[]>(() => Encoding.UTF8.GetBytes(MermaidSvgTheme.AddBackground(svg, opts.Bg!))));
                    },
                    token);

                if (!token.IsCancellationRequested)
                {
                    var diagram = new SvgImage { Source = result.Source };
                    ImageExport.SetSvg(diagram, result.File);
                    ShowDiagram(rendered[colours] = diagram);
                }
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
                    border.Theme = null;
                }
            }
            finally
            {
                // Stryker disable once Statement : a CancellationTokenSource without timers or linked tokens holds no resources
                localCts.Dispose();
                if (cts == localCts)
                    cts = null;
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

        HighlightedCode.Write(renderer, border, textBlock, obj, language);

        CodeBlockToolbar.Write(renderer, border, obj, language);
    }
}
