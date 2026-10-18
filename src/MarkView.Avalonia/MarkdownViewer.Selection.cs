// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>
    /// Identifies the <see cref="SelectionChanged"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<MarkdownViewer, RoutedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Identifies the <see cref="CopyingToClipboard"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<CopyingToClipboardEventArgs> CopyingToClipboardEvent =
        RoutedEvent.Register<MarkdownViewer, CopyingToClipboardEventArgs>(nameof(CopyingToClipboard), RoutingStrategies.Bubble);

    /// <summary>
    /// Defines the <see cref="HasSelection"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, bool> HasSelectionProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, bool>(nameof(HasSelection), o => o.HasSelection);

    /// <summary>
    /// Raised when the selected range changes: at each step of a drag that moves its end, on
    /// <see cref="SelectAll"/> and <see cref="ClearSelection"/>, on a click that clears it, when a new document
    /// replaces the one it was in, and when a streaming update shortens or clears it. Not raised when the range
    /// stays the same. Bubbles up the visual tree.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>
    /// Raised before the viewer writes to the clipboard: when the selection is copied (with the copy hotkey or
    /// <see cref="CopyToClipboardAsync"/>) and when a code block's copy button is clicked. A handler can change
    /// <see cref="CopyingToClipboardEventArgs.Text"/> or set <see cref="CopyingToClipboardEventArgs.Cancel"/>.
    /// Not raised when there is nothing to copy. Bubbles up the visual tree.
    /// </summary>
    public event EventHandler<CopyingToClipboardEventArgs>? CopyingToClipboard
    {
        add => AddHandler(CopyingToClipboardEvent, value);
        remove => RemoveHandler(CopyingToClipboardEvent, value);
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
    /// It is computed on each read, so it is not a bindable property; bind to <see cref="HasSelection"/> or handle
    /// <see cref="SelectionChanged"/> instead.
    /// </summary>
    public string SelectedText => GetSelectedText();

    /// <summary>Selects all text in the rendered document.</summary>
    public void SelectAll() => _selectionLayer?.SelectAll();

    /// <summary>Clears the current selection.</summary>
    public void ClearSelection() => _selectionLayer?.ClearSelection();

    /// <summary>Returns the currently selected plain text, with lines separated by <c>'\n'</c>.</summary>
    public string GetSelectedText() => _selectionLayer?.GetSelectedText() ?? string.Empty;

    /// <summary>
    /// Copies the selected text to the clipboard, with the platform's line endings, after raising
    /// <see cref="CopyingToClipboard"/>. Does nothing when nothing is selected.
    /// </summary>
    public Task CopyToClipboardAsync() => CopyAsync(MarkdownCopyOrigin.Selection, GetSelectedText());

    /// <summary>
    /// Raises <see cref="CopyingToClipboard"/> for <paramref name="text"/> and writes what the handlers left,
    /// with the platform's line endings. Returns whether anything was written: not when the text is empty,
    /// the copy was cancelled or the viewer has no clipboard.
    /// </summary>
    internal async Task<bool> CopyAsync(MarkdownCopyOrigin origin, string text)
    {
        if (text.Length == 0) return false;

        var e = new CopyingToClipboardEventArgs(origin, text) { RoutedEvent = CopyingToClipboardEvent };
        RaiseEvent(e);
        if (e.Cancel || e.Text.Length == 0 || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return false;

        await clipboard.SetTextAsync(e.Text.ReplaceLineEndings());
        return true;
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
        RaiseEvent(new RoutedEventArgs(SelectionChangedEvent, this));
    }
}
