// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Keeps the text in view where it is when content above it changes height, such as an image loading
/// or a formula rendered once it comes near (see <see cref="ViewportTracking"/>), as browsers do with
/// CSS scroll anchoring. After each layout pass, the anchor (the first top-level block that starts in
/// view, or the element the viewer last scrolled to) is compared with where it was: when content above
/// it has pushed it, the scroll offset moves by as much, before that pass is drawn.
/// </summary>
/// <remarks>
/// Avalonia's own anchoring (<see cref="IScrollAnchorProvider"/>) anchors the candidate nearest the top
/// of the viewport, which is often the block just above the one in view, partly scrolled out: when that
/// block grows (an image in it loading), it stays put and pushes the text in view down.
/// </remarks>
internal sealed class ScrollAnchor
{
    private Control? _anchor;
    private double _anchorTop;
    private bool _pinned;
    private double? _offset;

    /// <summary>
    /// Anchors <paramref name="target"/>, which the viewer has just scrolled to, until the document is
    /// scrolled again: content above it that grows as it is realized leaves it where it was shown.
    /// </summary>
    public void Pin(Control target, Panel root, ScrollViewer scrollViewer)
    {
        if (TopOf(target, root) is not { } top)
            return;
        _anchor = target;
        _anchorTop = top;
        _pinned = true;
        _offset = scrollViewer.Offset.Y;
    }

    /// <summary>Forgets the anchor, e.g. when another document replaces the one it was in.</summary>
    public void Reset()
    {
        _anchor = null;
        _pinned = false;
        _offset = null;
    }

    /// <summary>
    /// Moves the scroll offset by as much as the anchor moved within <paramref name="root"/> since the
    /// last call, then chooses the anchor for the next one. Called after every layout pass.
    /// </summary>
    public void Update(Panel root, ScrollViewer scrollViewer)
    {
        if (root.FindAncestorOfType<ScrollContentPresenter>() is not { } presenter
            || root.TranslatePoint(default, presenter) is not { } rootOrigin)
        {
            Reset();
            return;
        }

        // The top and bottom of the visible area, in the root's coordinates.
        var visibleTop = -rootOrigin.Y;
        var visibleBottom = visibleTop + presenter.Bounds.Height;
        var scrolled = _offset is { } offset && System.Math.Abs(scrollViewer.Offset.Y - offset) > 0.5;
        if (scrolled)
            _pinned = false;

        var anchorTop = _anchor is null ? null : TopOf(_anchor, root);
        if (anchorTop is null)
            _pinned = false;

        // After a scroll, the anchor only counts while it is still in view: content realized around a
        // place the document jumped to may have grown above an anchor far from it, which says nothing
        // about the text now in view.
        if (anchorTop is { } top && top != _anchorTop
            && (!scrolled || (top >= visibleTop && top <= visibleBottom)))
        {
            var shift = top - _anchorTop;
            _anchorTop = top;
            scrollViewer.Offset = scrollViewer.Offset.WithY(scrollViewer.Offset.Y + shift);
            // The next layout pass lays the document out at the new offset and chooses the anchor then.
            _offset = scrollViewer.Offset.Y;
            return;
        }

        if (!_pinned)
            Choose(root, visibleTop, visibleBottom);
        _offset = scrollViewer.Offset.Y;
    }

    // The first block that starts in view; or, when none does (the view is inside a long block), the
    // block the view is in, which keeps it in place when content above that block grows.
    private void Choose(Panel root, double visibleTop, double visibleBottom)
    {
        var children = root.Children;
        int low = 0, high = children.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (children[middle].Bounds.Y < visibleTop)
                low = middle + 1;
            else
                high = middle;
        }

        if (low < children.Count && children[low].Bounds.Y <= visibleBottom)
            _anchor = children[low];
        else
            _anchor = low > 0 ? children[low - 1] : null;
        if (_anchor is not null)
            _anchorTop = _anchor.Bounds.Y;
    }

    // A top-level block's top is its own Bounds.Y; anything deeper is translated. Null once the element
    // has left the document.
    private static double? TopOf(Control element, Panel root) =>
        ReferenceEquals(element.GetVisualParent(), root) ? element.Bounds.Y
        : element.TranslatePoint(default, root)?.Y;
}
