// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia;

/// <summary>
/// A control that renders Markdown text into Avalonia visual elements.
/// </summary>
public partial class MarkdownViewer : ContentControl
{
    // Matches a backtick code span (group 1) or the non-standard "![alt](url =WxH)" size hint
    // (groups 2-5). Code spans are passed through unchanged so literal syntax examples in
    // documentation are not mangled.
    [GeneratedRegex(@"(`+)[\s\S]*?\1|(\!\[[^\]]*\]\()([^\s\)]+)\s+=(\d+x\d+)(\))")]
    private static partial Regex ImageSizePreprocessorRegex();

    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private Dictionary<string, Control> _anchors = new(StringComparer.OrdinalIgnoreCase);
    private List<(int Level, string Text, string Slug)> _headingEntries = [];
    private DocumentSelectionLayer? _selectionLayer;
    private ScrollViewer? _scrollViewer;
    private bool _isDragging;
    private Point _dragStart;
    private const double DragThreshold = 3.0;

    private string? _sourceMarkdown;
    private CancellationTokenSource? _sourceLoadCts;

    private static readonly MarkdownPipeline DefaultPipeline =
        new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

    /// <summary>
    /// Defines the <see cref="Markdown"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));

    /// <summary>
    /// Defines the <see cref="Pipeline"/> property.
    /// </summary>
    public static readonly StyledProperty<MarkdownPipeline?> PipelineProperty =
        AvaloniaProperty.Register<MarkdownViewer, MarkdownPipeline?>(nameof(Pipeline));

    /// <summary>
    /// Defines the <see cref="BaseUri"/> property.
    /// </summary>
    public static readonly StyledProperty<Uri?> BaseUriProperty =
        AvaloniaProperty.Register<MarkdownViewer, Uri?>(nameof(BaseUri));

    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<Uri?> SourceProperty =
        AvaloniaProperty.Register<MarkdownViewer, Uri?>(nameof(Source));

    /// <summary>
    /// Defines the <see cref="ImageResizeMode"/> property.
    /// </summary>
    public static readonly StyledProperty<ImageResizeMode> ImageResizeModeProperty =
        AvaloniaProperty.Register<MarkdownViewer, ImageResizeMode>(nameof(ImageResizeMode), ImageResizeMode.ScaleDownToFit);

    /// <summary>
    /// Defines the <see cref="TableOfContentsMaxDepth"/> property.
    /// </summary>
    public static readonly StyledProperty<int> TableOfContentsMaxDepthProperty =
        AvaloniaProperty.Register<MarkdownViewer, int>(nameof(TableOfContentsMaxDepth), 6);

    /// <summary>
    /// Defines the <see cref="TableOfContents"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, IReadOnlyList<TocEntry>> TableOfContentsProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, IReadOnlyList<TocEntry>>(
            nameof(TableOfContents), o => o.TableOfContents);

    /// <summary>
    /// Gets or sets the Markdown text to render.
    /// </summary>
    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>
    /// Gets or sets the Markdig pipeline. If null, a default pipeline with supported extensions is used.
    /// </summary>
    public MarkdownPipeline? Pipeline
    {
        get => GetValue(PipelineProperty);
        set => SetValue(PipelineProperty, value);
    }

    /// <summary>
    /// Gets or sets the base URI for resolving relative URLs in images and links.
    /// </summary>
    public Uri? BaseUri
    {
        get => GetValue(BaseUriProperty);
        set => SetValue(BaseUriProperty, value);
    }

    /// <summary>
    /// Gets or sets a URI that points to a Markdown document to load and render.
    /// Supports <c>avares://</c> embedded resources, <c>file://</c>, and <c>http/https</c> schemes.
    /// When both <see cref="Source"/> and <see cref="Markdown"/> are set,
    /// <see cref="Source"/> takes precedence.
    /// </summary>
    public Uri? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Gets or sets how images without an explicit "=WxH" size hint are scaled
    /// relative to their container. Defaults to <see cref="ImageResizeMode.ScaleDownToFit"/>.
    /// </summary>
    public ImageResizeMode ImageResizeMode
    {
        get => GetValue(ImageResizeModeProperty);
        set => SetValue(ImageResizeModeProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum heading level included in <see cref="TableOfContents"/>.
    /// Headings deeper than this level, and their descendants, are excluded. Defaults to 6 (no limit).
    /// </summary>
    public int TableOfContentsMaxDepth
    {
        get => GetValue(TableOfContentsMaxDepthProperty);
        set => SetValue(TableOfContentsMaxDepthProperty, value);
    }

    /// <summary>
    /// Gets the nested heading tree for the current document, filtered by
    /// <see cref="TableOfContentsMaxDepth"/>. Recomputed after every render. Pass an
    /// entry's <see cref="TocEntry.Slug"/> to <see cref="ScrollToAnchor"/> to navigate to it.
    /// </summary>
    public IReadOnlyList<TocEntry> TableOfContents
    {
        get;
        private set => SetAndRaise(TableOfContentsProperty, ref field, value);
    } = [];

    /// <summary>
    /// Extensions that customise the renderer before each render pass.
    /// Add entries before setting <see cref="Markdown"/> or assigning
    /// a new <see cref="Pipeline"/>; each extension's
    /// <see cref="IMarkViewExtension.Register"/> is called once per render.
    /// </summary>
    public IList<IMarkViewExtension> Extensions { get; } = [];

    /// <summary>
    /// Identifies the <see cref="LinkClicked"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<LinkClickedEventArgs> LinkClickedEvent =
        RoutedEvent.Register<MarkdownViewer, LinkClickedEventArgs>(
            nameof(LinkClicked), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when a hyperlink in the rendered markdown is clicked.
    /// Bubbles up the visual tree; subscribe globally with
    /// <c>MarkdownViewer.LinkClickedEvent.AddClassHandler&lt;MarkdownViewer&gt;(...)</c>.
    /// </summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    static MarkdownViewer()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        PipelineProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        BaseUriProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        SourceProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.OnSourceChanged());
        ImageResizeModeProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RenderMarkdown());
        TableOfContentsMaxDepthProperty.Changed.AddClassHandler<MarkdownViewer>((x, _) => x.RebuildTableOfContents());
        FocusableProperty.OverrideDefaultValue<MarkdownViewer>(true);
    }

    // ── Source loading ────────────────────────────────────────────────────────

    private void OnSourceChanged()
    {
        _sourceLoadCts?.Cancel();
        _sourceLoadCts = null;
        _sourceMarkdown = null;

        var source = Source;
        if (source is null)
        {
            RenderMarkdown();
            return;
        }

        var fragment = source.Fragment.TrimStart('#');

        switch (source.Scheme)
        {
            // avares:// is a ManifestResourceStream — memory-mapped into the loaded assembly,
            // effectively zero I/O cost. No async API exists; synchronous read is appropriate.
            case "avares":
            {
                using var stream = AssetLoader.Open(source);
                using var reader = new StreamReader(stream);
                _sourceMarkdown = reader.ReadToEnd();
                RenderMarkdown();
                ScrollToAnchorAfterRender(fragment);
                break;
            }
            case "file":
            case "http":
            case "https":
                _ = LoadFromUriAsync(source, fragment);
                break;
        }
    }

    private async Task LoadFromUriAsync(Uri uri, string fragment)
    {
        var cts = new CancellationTokenSource();
        _sourceLoadCts = cts;
        try
        {
            string text;
            if (uri.Scheme == "file")
            {
                using var stream = File.OpenRead(uri.LocalPath);
                using var reader = new StreamReader(stream);
                text = await reader.ReadToEndAsync(cts.Token);
            }
            else
            {
                using var stream = await SharedHttpClient.Instance.GetStreamAsync(uri, cts.Token);
                using var reader = new StreamReader(stream);
                text = await reader.ReadToEndAsync(cts.Token);
            }

            if (!cts.IsCancellationRequested)
            {
                _sourceMarkdown = text;
                RenderMarkdown();
                ScrollToAnchorAfterRender(fragment);
            }
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { }
        catch (IOException) { }
    }

    // Anchor targets are only positioned after the next layout pass, so scrolling
    // must be deferred past the render that just set Content.
    private void ScrollToAnchorAfterRender(string fragment)
    {
        if (string.IsNullOrEmpty(fragment)) return;
        Dispatcher.UIThread.Post(() => ScrollToAnchor(fragment), DispatcherPriority.Loaded);
    }

    private static Uri? InferBaseUri(Uri? source)
    {
        if (source is null) return null;
        var abs = source.AbsoluteUri;
        var lastSlash = abs.LastIndexOf('/');
        return lastSlash > 0 ? new Uri(abs[..(lastSlash + 1)]) : null;
    }

    private void RenderMarkdown()
    {
        var markdown = _sourceMarkdown ?? Markdown;
        if (string.IsNullOrEmpty(markdown))
        {
            Content = null;
            _anchors = new(StringComparer.OrdinalIgnoreCase);
            _headingEntries = [];
            RebuildTableOfContents();
            _selectionLayer = null;
            return;
        }

        var pipeline = Pipeline ?? MarkdownViewerDefaults.Pipeline ?? DefaultPipeline;
        var markdownText = ImageSizePreprocessorRegex().Replace(markdown, static m =>
            m.Groups[1].Success
                ? m.Value
                : $"{m.Groups[2].Value}{m.Groups[3].Value} \"={m.Groups[4].Value}\"{m.Groups[5].Value}");
        var document = Markdig.Markdown.Parse(markdownText, pipeline);

        var renderer = new AvaloniaRenderer
        {
            BaseUri = BaseUri ?? InferBaseUri(Source),
            ImageResizeMode = ImageResizeMode,
        };
        renderer.LinkClicked += OnLinkClicked;

        // Extensions register before pipeline.Setup() so they can swap renderers.
        // Globals first, then per-instance; skip exact-reference duplicates.
        // Allocate the dedup HashSet only when at least one list is non-empty.
        if (MarkdownViewerDefaults.Extensions.Count > 0 || Extensions.Count > 0)
        {
            var seen = new HashSet<IMarkViewExtension>(ReferenceEqualityComparer.Instance);

            foreach (var ext in MarkdownViewerDefaults.Extensions)
            {
                if (seen.Add(ext))
                    ext.Register(renderer);
            }
            foreach (var ext in Extensions)
            {
                if (seen.Add(ext))
                    ext.Register(renderer);
            }
        }

        pipeline.Setup(renderer);
        renderer.Render(document);

        _anchors = new Dictionary<string, Control>(renderer.Anchors, StringComparer.OrdinalIgnoreCase);
        _headingEntries = [.. renderer.HeadingEntries];
        RebuildTableOfContents();

        // Set up document-wide selection layer
        var layer = new DocumentSelectionLayer();
        RegisterBlocks(layer, renderer.RootPanel);
        _selectionLayer = layer;

        // Stack panel + transparent selection layer in a single-cell Grid
        var contentGrid = new Grid();
        contentGrid.Children.Add(renderer.RootPanel);
        contentGrid.Children.Add(layer);

        // Tunnel handlers fire before any child receives the event
        contentGrid.AddHandler(InputElement.PointerPressedEvent,
            OnContentPointerPressed, RoutingStrategies.Tunnel);
        contentGrid.AddHandler(InputElement.PointerMovedEvent,
            OnContentPointerMoved, RoutingStrategies.Tunnel);
        contentGrid.AddHandler(InputElement.PointerReleasedEvent,
            OnContentPointerReleased, RoutingStrategies.Tunnel);

        Content = contentGrid;

        // Every new render is treated as a fresh document — reset scroll position to the
        // top, since PART_ScrollViewer now persists across renders (it lives in the
        // template, not rebuilt per-render) and would otherwise keep its old offset.
        if (_scrollViewer is not null)
            _scrollViewer.Offset = default;
    }

    private void RebuildTableOfContents() =>
        TableOfContents = TocEntry.BuildTree(_headingEntries, TableOfContentsMaxDepth);

    // ── Block registration ────────────────────────────────────────────────────

    private void RegisterBlocks(DocumentSelectionLayer layer, Panel panel)
    {
        foreach (var child in panel.Children)
        {
            switch (child)
            {
                case MarkdownSelectableTextBlock tb:
                    layer.Register(new IndexEntry(tb,
                        MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!), "\n"));
                    break;
                case Border { Child: TextBlock codeTb } border
                    when border.Classes.Contains("markdown-code-block"):
                    layer.Register(new IndexEntry(codeTb,
                        codeTb.Inlines != null
                            ? MarkdownSelectableTextBlock.ExtractPlainText(codeTb.Inlines)
                            : codeTb.Text ?? string.Empty, "\n"));
                    break;
                case Panel listPanel when listPanel.Classes.Contains("markdown-list"):
                    RegisterListItems(layer, listPanel);
                    break;
                case Grid tableGrid when tableGrid.Classes.Contains("markdown-table"):
                    RegisterTableRows(layer, tableGrid);
                    break;
                case Border { Child: Panel borderPanel }:
                    RegisterBlocks(layer, borderPanel);
                    break;
                case Panel nested:
                    RegisterBlocks(layer, nested);
                    break;
            }
        }
    }

    private void RegisterListItems(DocumentSelectionLayer layer, Panel listPanel)
    {
        foreach (var child in listPanel.Children)
        {
            if (child is not Grid itemGrid) continue;

            // Column 0 holds the marker TextBlock (bullet/ordered) or a CheckBox (task list)
            // Column 1 holds the content StackPanel
            TextBlock? markerTb = null;
            Panel? contentPanel = null;
            foreach (var gridChild in itemGrid.Children)
            {
                if (gridChild is TextBlock tb && Grid.GetColumn(tb) == 0)
                    markerTb = tb;
                else if (gridChild is Panel pnl && Grid.GetColumn(pnl) == 1)
                    contentPanel = pnl;
            }
            var markerText = markerTb?.Text ?? string.Empty;
            if (contentPanel is null) continue;

            bool markerApplied = false;
            RegisterListContent(layer, contentPanel, markerText, ref markerApplied);
        }
    }

    private void RegisterListContent(DocumentSelectionLayer layer, Panel panel,
        string marker, ref bool markerApplied)
    {
        foreach (var child in panel.Children)
        {
            switch (child)
            {
                case MarkdownSelectableTextBlock tb:
                    var text = MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!);
                    if (!markerApplied && !string.IsNullOrEmpty(marker))
                    {
                        text = marker + " " + text;
                        markerApplied = true;
                    }
                    layer.Register(new IndexEntry(tb, text, "\n"));
                    break;
                // Nested list — recurse with its own markers
                case Panel nestedList when nestedList.Classes.Contains("markdown-list"):
                    RegisterListItems(layer, nestedList);
                    break;
                case Border { Child: Panel borderPanel }:
                    RegisterListContent(layer, borderPanel, marker, ref markerApplied);
                    break;
                case Panel nested:
                    RegisterListContent(layer, nested, marker, ref markerApplied);
                    break;
            }
        }
    }

    private static void RegisterTableRows(DocumentSelectionLayer layer, Grid tableGrid)
    {
        // TableRenderer adds cells in row-major order (rowIndex / colIndex ascending),
        // so iterating Children directly avoids the O(N log N) SortedDictionary sort.
        // Collect borders first, then emit each with "\t" or "\n" based on row boundary.
        var cells = new List<Border>(tableGrid.Children.Count);
        foreach (var child in tableGrid.Children)
            if (child is Border b) cells.Add(b);

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var tb = FindFirstTextBlock(cell);
            if (tb is null) continue;

            var text = cell.Child is Panel p ? ExtractPanelText(p) : string.Empty;
            // Separator: "\n" if this is the last cell in its row, "\t" otherwise.
            bool isLastInRow = i == cells.Count - 1
                || Grid.GetRow(cells[i + 1]) != Grid.GetRow(cell);
            layer.Register(new IndexEntry(tb, text, isLastInRow ? "\n" : "\t"));
        }
    }

    /// <summary>Extracts plain text from a panel, joining child texts with a space.</summary>
    private static string ExtractPanelText(Panel panel)
    {
        var sb = new StringBuilder();
        foreach (var child in panel.Children)
        {
            switch (child)
            {
                case MarkdownSelectableTextBlock tb:
                    var t = MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!);
                    if (!string.IsNullOrEmpty(t)) { if (sb.Length > 0) sb.Append(' '); sb.Append(t); }
                    break;
                case Panel nested:
                    var nt = ExtractPanelText(nested);
                    if (!string.IsNullOrEmpty(nt)) { if (sb.Length > 0) sb.Append(' '); sb.Append(nt); }
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Returns the first <see cref="TextBlock"/> found in a control subtree.</summary>
    private static TextBlock? FindFirstTextBlock(Control control)
    {
        switch (control)
        {
            case TextBlock tb: return tb;
            case Border { Child: Control child }: return FindFirstTextBlock(child);
            case Panel panel:
                foreach (var c in panel.Children)
                {
                    var found = FindFirstTextBlock(c);
                    if (found is not null) return found;
                }
                return null;
            default: return null;
        }
    }

    // ── Pointer handlers ──────────────────────────────────────────────────────

    private void OnContentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.Properties.IsLeftButtonPressed || _selectionLayer is null) return;
        Focus();
        _isDragging = false;
        _dragStart = e.GetPosition(_selectionLayer);
        _selectionLayer.OnPointerPressed(_dragStart);
    }

    private void OnContentPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_selectionLayer is null) return;
        var pos = e.GetPosition(_selectionLayer);

        if (e.Properties.IsLeftButtonPressed)
        {
            if (!_isDragging)
            {
                var delta = pos - _dragStart;
                var dist = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
                if (dist > DragThreshold)
                {
                    _isDragging = true;
                    e.Pointer.Capture((IInputElement)sender!);
                }
            }
            if (_isDragging)
            {
                _selectionLayer.OnPointerMoved(pos);
                e.Handled = true;
                return;
            }
        }

        // Update cursor on hover (no drag)
        UpdateHyperlinkCursor(pos);
    }

    private void OnContentPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            e.Pointer.Capture(null);
            _isDragging = false;
            return;
        }

        // Click without drag — check for hyperlink
        if (_selectionLayer is not null)
            TryFireHyperlinkClick(e.GetPosition(_selectionLayer));
    }

    private void UpdateHyperlinkCursor(Point posInLayer)
    {
        if (_selectionLayer is null) return;
        Cursor = FindHyperlinkAt(posInLayer) != null
            ? HandCursor
            : Cursor.Default;
    }

    private void TryFireHyperlinkClick(Point posInLayer)
    {
        if (_selectionLayer is null) return;
        var entry = _selectionLayer.HitTestEntry(posInLayer);
        if (entry?.TextBlock is not MarkdownSelectableTextBlock mstb) return;
        var origin = mstb.TranslatePoint(new Point(0, 0), _selectionLayer) ?? default;
        var hyperlink = mstb.HitTestHyperlink(posInLayer - origin);
        if (hyperlink?.NavigateUri != null)
            OnLinkClicked(this, new LinkClickedEventArgs(hyperlink.NavigateUri.ToString()));
    }

    private MarkdownHyperlink? FindHyperlinkAt(Point posInLayer)
    {
        if (_selectionLayer is null) return null;
        var entry = _selectionLayer.HitTestEntry(posInLayer);
        if (entry?.TextBlock is not MarkdownSelectableTextBlock mstb) return null;
        var origin = mstb.TranslatePoint(new Point(0, 0), _selectionLayer) ?? default;
        return mstb.HitTestHyperlink(posInLayer - origin);
    }

    // ── Selection API ─────────────────────────────────────────────────────────

    /// <summary>Selects all text in the rendered document.</summary>
    public void SelectAll() => _selectionLayer?.SelectAll();

    /// <summary>Clears the current selection.</summary>
    public void ClearSelection() => _selectionLayer?.ClearSelection();

    /// <summary>Returns the currently selected plain text.</summary>
    public string GetSelectedText() => _selectionLayer?.GetSelectedText() ?? string.Empty;

    /// <summary>Copies selected text to the clipboard.</summary>
    public Task CopyToClipboardAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        return topLevel is not null
            ? _selectionLayer?.CopyToClipboardAsync(topLevel) ?? Task.CompletedTask
            : Task.CompletedTask;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var hotkeys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
        if (hotkeys is null) return;
        if (hotkeys.Copy.Any(g => g.Matches(e))) { _ = CopyToClipboardAsync(); e.Handled = true; }
        else if (hotkeys.SelectAll.Any(g => g.Matches(e))) { SelectAll(); e.Handled = true; }
    }

    // ── Link handling ─────────────────────────────────────────────────────────

    private void OnLinkClicked(object? sender, LinkClickedEventArgs e)
    {
        if (e.Url.StartsWith('#'))
        {
            ScrollToAnchor(e.Url[1..]);
            e.Handled = true;
            return;
        }

        e.RoutedEvent = LinkClickedEvent;
        RaiseEvent(e);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
    }

    public void ScrollToAnchor(string anchorId)
    {
        if (!_anchors.TryGetValue(anchorId, out var control))
            return;

        if (_scrollViewer is null)
        {
            control.BringIntoView();
            return;
        }

        // Translating to the ScrollViewer itself (rather than to Content) gives a
        // viewport-relative point that already accounts for Padding — Padding is applied
        // as the ContentPresenter's Margin *inside* the ScrollViewer, so Content's origin
        // is offset from the scrollable extent's origin by Padding.Top. Adding the current
        // Offset.Y back converts the viewport-relative point to absolute content-space
        // coordinates, which is what the new Offset value must be expressed in.
        var point = control.TranslatePoint(new Point(0, 0), _scrollViewer);
        if (point is null)
        {
            control.BringIntoView();
            return;
        }

        const double topMargin = 16;
        var desiredY = _scrollViewer.Offset.Y + point.Value.Y - topMargin;
        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, Math.Max(0, desiredY));
    }
}
