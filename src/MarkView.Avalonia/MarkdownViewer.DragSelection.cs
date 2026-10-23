// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    private SelectionAutoScroll? _selectionAutoScroll;
    // The scroll offset the selection was last extended at, during a drag.
    private double _dragOffset;

    /// <summary>The scrolling of the drag in progress held near or past an edge, while it runs, or <c>null</c>, for tests.</summary>
    internal SelectionAutoScroll? ActiveSelectionAutoScroll =>
        _selectionAutoScroll is { IsRunning: true } autoScroll ? autoScroll : null;

    /// <summary>
    /// Asks for the frames a drag held near or past an edge scrolls on, instead of the top level's
    /// <see cref="TopLevel.RequestAnimationFrame"/>. Tests replace it, to run the frames themselves.
    /// </summary>
    internal Action<Action<TimeSpan>>? RequestAnimationFrame { get; set; }

    /// <summary>
    /// Extends the selection of the drag in progress to the pointer, and scrolls while the pointer is near or past the
    /// top or bottom edge (see <see cref="SelectionAutoScroll"/>).
    /// </summary>
    private void DragTo(PointerEventArgs e)
    {
        if (_scrollViewer is not { } scrollViewer)
        {
            _selectionLayer?.OnPointerMoved(e.GetPosition(_selectionLayer));
            return;
        }
        _selectionAutoScroll ??= new SelectionAutoScroll(scrollViewer);
        var topLevel = TopLevel.GetTopLevel(this);
        _selectionAutoScroll.Update(e.GetPosition(scrollViewer),
            RequestAnimationFrame ?? (topLevel is null ? null : topLevel.RequestAnimationFrame));
        ExtendDragSelection();
    }

    /// <summary>
    /// Extends the selection to the pointer's last position. Above or below the viewport, that is the text at its
    /// edge, which scrolling brings into view, rather than text out of view.
    /// </summary>
    private void ExtendDragSelection()
    {
        if (_selectionLayer is null || _scrollViewer is not { } scrollViewer || _selectionAutoScroll is not { } autoScroll)
            return;
        _dragOffset = scrollViewer.Offset.Y;
        var pointer = autoScroll.Pointer;
        var y = Math.Clamp(pointer.Y, 0, Math.Max(0, scrollViewer.Bounds.Height - 1));
        if (scrollViewer.TranslatePoint(pointer.WithY(y), _selectionLayer) is { } posInLayer)
            _selectionLayer.OnPointerMoved(posInLayer);
    }

    // The document scrolled under a pointer held still in the middle of a drag (auto-scrolling, the mouse wheel, or
    // scroll anchoring): the text now under the pointer is where the selection ends. After layout, so that the text is
    // where the new offset shows it.
    private void OnLayoutUpdatedFollowDrag(object? sender, EventArgs e)
    {
        if (_isDragging && _scrollViewer is { } scrollViewer && scrollViewer.Offset.Y != _dragOffset)
            ExtendDragSelection();
    }
}
