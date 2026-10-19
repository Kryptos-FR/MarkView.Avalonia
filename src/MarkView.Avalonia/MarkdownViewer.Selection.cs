// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

using Markdig.Renderers;
using Markdig.Syntax;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>
    /// Identifies the <see cref="SelectionChanged"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<MarkdownSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<MarkdownViewer, MarkdownSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Identifies the <see cref="CopyingToClipboard"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<CopyingToClipboardEventArgs> CopyingToClipboardEvent =
        RoutedEvent.Register<MarkdownViewer, CopyingToClipboardEventArgs>(nameof(CopyingToClipboard), RoutingStrategies.Bubble);

    /// <summary>
    /// Defines the <see cref="IsHtmlCopyEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsHtmlCopyEnabledProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(IsHtmlCopyEnabled), true);

    /// <summary>
    /// Defines the <see cref="HasSelection"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, bool> HasSelectionProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, bool>(nameof(HasSelection), o => o.HasSelection);

    /// <summary>
    /// Raised when the selected range changes: at each step of a drag that moves its end, on
    /// <see cref="SelectAll"/> and <see cref="ClearSelection"/>, on a click that clears it, when a new document
    /// replaces the one it was in, and when a streaming update shortens or clears it. Not raised when the range
    /// stays the same, except once more when a drag that changed it ends: the events raised during the drag
    /// have <see cref="MarkdownSelectionChangedEventArgs.IsSelecting"/> set, the last one does not. Bubbles up
    /// the visual tree.
    /// </summary>
    public event EventHandler<MarkdownSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>
    /// Raised before the viewer writes to the clipboard: when the selection is copied (with the copy hotkey,
    /// <see cref="CopyToClipboardAsync"/> or <see cref="CopyAsMarkdownAsync"/>) and when a code block's copy
    /// button is clicked. A handler can change
    /// <see cref="CopyingToClipboardEventArgs.Text"/> or set <see cref="CopyingToClipboardEventArgs.Cancel"/>.
    /// Not raised when there is nothing to copy. Bubbles up the visual tree.
    /// </summary>
    public event EventHandler<CopyingToClipboardEventArgs>? CopyingToClipboard
    {
        add => AddHandler(CopyingToClipboardEvent, value);
        remove => RemoveHandler(CopyingToClipboardEvent, value);
    }

    /// <summary>
    /// Gets or sets whether copying the selection also writes it as HTML, next to the plain text, so that
    /// pasting into a word processor, a chat or a web editor keeps its formatting. Only on Windows, macOS and
    /// Linux; other platforms get the plain text alone. Defaults to <c>true</c>.
    /// </summary>
    public bool IsHtmlCopyEnabled
    {
        get => GetValue(IsHtmlCopyEnabledProperty);
        set => SetValue(IsHtmlCopyEnabledProperty, value);
    }

    /// <summary>
    /// Gets whether some text is selected. <see cref="SelectionChanged"/> is raised whenever the selection
    /// changes, this property only when it starts or stops being empty.
    /// </summary>
    public bool HasSelection
    {
        get;
        private set => SetAndRaise(HasSelectionProperty, ref field, value);
    }

    /// <summary>
    /// Gets the selected plain text, with lines separated by <c>'\n'</c>; the same as <see cref="GetSelectedText"/>.
    /// It is built on the first read after the selection changes, and later reads return the same string. It is
    /// not a bindable property; bind to <see cref="HasSelection"/> or handle <see cref="SelectionChanged"/> instead.
    /// </summary>
    public string SelectedText => GetSelectedText();

    /// <summary>Selects all text in the rendered document.</summary>
    public void SelectAll() => _selectionLayer?.SelectAll();

    /// <summary>Clears the current selection.</summary>
    public void ClearSelection() => _selectionLayer?.ClearSelection();

    /// <summary>Returns the currently selected plain text, with lines separated by <c>'\n'</c>.</summary>
    public string GetSelectedText() => _selectionLayer?.GetSelectedText() ?? string.Empty;

    /// <summary>
    /// Returns the selection as Markdown, with lines separated by <c>'\n'</c>. A top-level block selected whole
    /// is copied as its source text, so selecting the whole document gives back the Markdown it was rendered
    /// from. A block only partly selected, at either end of the selection, is rebuilt for its selected part:
    /// its text with its emphasis, code, links and images, inside its heading, list item, quote or table row,
    /// written as normalised Markdown (<c>**bold**</c>, <c>*italic*</c>, <c>-</c> bullets). A selection within a
    /// single block copies only its text, with its inline syntax.
    /// </summary>
    public string GetSelectedMarkdown()
    {
        if (_selectionLayer is not { } layer || Content is not Grid { Children: [Panel root, ..] })
            return string.Empty;
        return SelectionMarkdownWriter.Write(layer, root, SourceOf, _renderedMarkdown);
    }

    /// <summary>
    /// Copies the selected text to the clipboard, with the platform's line endings, after raising
    /// <see cref="CopyingToClipboard"/>. While <see cref="IsHtmlCopyEnabled"/> is <c>true</c>, the selection is
    /// also written as HTML. Does nothing when nothing is selected.
    /// </summary>
    public Task CopyToClipboardAsync()
    {
        var text = GetSelectedText();
        var html = text.Length > 0 && IsHtmlCopyEnabled && HtmlClipboardFormat.IsSupported ? GetSelectedHtml() : null;
        return CopyAsync(MarkdownCopyOrigin.Selection, text, html);
    }

    /// <summary>
    /// Copies the selection as Markdown (see <see cref="GetSelectedMarkdown"/>) to the clipboard as plain text,
    /// with the platform's line endings, after raising <see cref="CopyingToClipboard"/>. Does nothing when
    /// nothing is selected.
    /// </summary>
    public Task CopyAsMarkdownAsync() => CopyAsync(MarkdownCopyOrigin.SelectionAsMarkdown, GetSelectedMarkdown());

    /// <summary>
    /// Raises <see cref="CopyingToClipboard"/> for <paramref name="text"/> and <paramref name="html"/> and writes
    /// what the handlers left, the text with the platform's line endings. Returns whether anything was written:
    /// not when the text is empty, the copy was cancelled or the viewer has no clipboard.
    /// </summary>
    internal async Task<bool> CopyAsync(MarkdownCopyOrigin origin, string text, string? html = null)
    {
        if (text.Length == 0) return false;

        var e = new CopyingToClipboardEventArgs(origin, text, html) { RoutedEvent = CopyingToClipboardEvent };
        RaiseEvent(e);
        if (e.Cancel || e.Text.Length == 0 || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return false;

        if (e.Html is { Length: > 0 } eventHtml && HtmlClipboardFormat.IsSupported)
        {
            var item = new DataTransferItem();
            item.SetText(e.Text.ReplaceLineEndings());
            HtmlClipboardFormat.Set(item, eventHtml);
            var transfer = new DataTransfer();
            transfer.Add(item);
            await clipboard.SetDataAsync(transfer);
        }
        else
            await clipboard.SetTextAsync(e.Text.ReplaceLineEndings());
        return true;
    }

    /// <summary>The selection as an HTML fragment, rendered from <see cref="GetSelectedMarkdown"/>.</summary>
    private string? GetSelectedHtml()
    {
        var markdown = GetSelectedMarkdown();
        if (markdown.Length == 0) return null;

        var pipeline = EffectivePipeline;
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        pipeline.Setup(renderer);
        // Pasted elsewhere, a relative address would no longer find its target.
        if ((BaseUri ?? InferBaseUri(Source)) is { } baseUri)
            renderer.LinkRewriter = url => ResolveAgainst(baseUri, url);
        renderer.Render(Markdig.Markdown.Parse(markdown, pipeline));
        writer.Flush();
        return writer.ToString();
    }

    private static string ResolveAgainst(Uri baseUri, string url) =>
        url.Length == 0 || url[0] == '#' || Uri.TryCreate(url, UriKind.Absolute, out _)
            || !Uri.TryCreate(baseUri, url, out var resolved)
            ? url
            : resolved.ToString();

    /// <summary>
    /// The source text of the top-level blocks from <paramref name="first"/> to <paramref name="last"/>, in the
    /// markdown as given (before image size hints were rewritten), with task toggles applied.
    /// </summary>
    private string SourceOf(Block first, Block last)
    {
        var text = _renderedMarkdown ?? string.Empty;
        // Positions past the text are those of closers a streaming update added to heal the tail.
        var start = Math.Clamp(ToSourcePosition(_sourceShifts, first.Span.Start, 0).Position, 0, text.Length);
        var end = Math.Clamp(ToSourcePosition(_sourceShifts, last.Span.End + 1, 0).Position, start, text.Length);
        // A block always ends a line, but its span can stop before the end of that line (an ATX heading's
        // closing sequence).
        var lineEnd = text.IndexOfAny(['\r', '\n'], end);
        return text[start..(lineEnd < 0 ? text.Length : lineEnd)].TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Makes <paramref name="layer"/>, a layer with nothing selected yet, the document's selection layer. The
    /// selection of the layer it replaces is gone, which is a change when it was not empty.
    /// </summary>
    private void SetSelectionLayer(DocumentSelectionLayer? layer)
    {
        var previous = _selectionLayer;
        if (previous is not null)
            previous.SelectionChanged -= OnLayerSelectionChanged;
        _selectionLayer = layer;
        if (layer is not null)
            layer.SelectionChanged += OnLayerSelectionChanged;

        if (previous?.HasSelection == true)
            OnLayerSelectionChanged(layer, EventArgs.Empty);
    }

    private void OnLayerSelectionChanged(object? sender, EventArgs e)
    {
        HasSelection = _selectionLayer?.HasSelection ?? false;
        RaiseEvent(new MarkdownSelectionChangedEventArgs(this, _selectionLayer?.IsSelecting ?? false));
    }
}
