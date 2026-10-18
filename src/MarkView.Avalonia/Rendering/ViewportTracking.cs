// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Defers costly content (a decoded image, a formula bitmap, highlighted code, a diagram) until the
/// element that shows it is about to scroll into view, so the parts of a long document nobody reaches
/// cost nothing. Renderers honour <see cref="AvaloniaRenderer.IsLazyRenderingEnabled"/> before using it.
/// </summary>
public static class ViewportTracking
{
    /// <summary>
    /// Calls <paramref name="realize"/> once, the first time <paramref name="element"/> is laid out
    /// within one viewport height of the visible part of its scroll viewer (or of its window, outside
    /// one). It is called during the layout pass that brings the element near, before that pass is
    /// rendered, so content it builds is laid out before it is ever seen. Call it before the element
    /// is attached to a visual tree, as a renderer does.
    /// </summary>
    /// <param name="element">The element whose position decides; it hosts the deferred content.</param>
    /// <param name="realize">Builds the deferred content.</param>
    /// <remarks>
    /// Avalonia's <see cref="Layoutable.EffectiveViewportChanged"/> clips the viewport to every
    /// ancestor that clips its content: an image inside a paragraph (a <see cref="TextBlock"/> clips to
    /// its bounds) would see nothing of the viewport until the paragraph itself scrolled into view. So
    /// the event is listened to on the outermost such ancestor below the scroll viewer, and the
    /// element's own bounds are measured against it. The layout manager only tracks that ancestor while
    /// the element is attached: an element that is never shown, or is discarded first, never calls
    /// <paramref name="realize"/> and is collected normally.
    /// </remarks>
    public static void RunWhenNearViewport(Control element, Action realize)
    {
        Layoutable? tracker = null;
        element.AttachedToVisualTree += OnAttached;
        element.DetachedFromVisualTree += OnDetached;

        void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            tracker = TrackerOf(element);
            tracker.EffectiveViewportChanged += OnEffectiveViewportChanged;
        }

        void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => StopTracking();

        void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            var origin = ReferenceEquals(tracker, element) ? new Point() : element.TranslatePoint(default, tracker!);
            if (origin is not { } position || !IsNearViewport(new Rect(position, element.Bounds.Size), e.EffectiveViewport))
                return;
            StopTracking();
            element.AttachedToVisualTree -= OnAttached;
            element.DetachedFromVisualTree -= OnDetached;
            realize();
        }

        void StopTracking()
        {
            if (tracker is null) return;
            tracker.EffectiveViewportChanged -= OnEffectiveViewportChanged;
            tracker = null;
        }
    }

    /// <summary>
    /// Returns the outermost ancestor of <paramref name="element"/> that clips its content, below the
    /// nearest scroll viewer's presenter (which clips the document to the visible area), or the element
    /// itself when none does.
    /// </summary>
    internal static Layoutable TrackerOf(Control element)
    {
        Layoutable tracker = element;
        for (var visual = element.GetVisualParent(); visual is not null and not ScrollContentPresenter; visual = visual.GetVisualParent())
        {
            if (visual.ClipToBounds && visual is Layoutable layoutable)
                tracker = layoutable;
        }
        return tracker;
    }

    // Both rectangles are in the same coordinates; the viewport is widened by its own height so content
    // is realized a screen before it scrolls into view. Edges count as touching, since an element
    // whose content is not realized yet may have no height at all.
    internal static bool IsNearViewport(Rect bounds, Rect viewport)
    {
        var area = viewport.Inflate(new Thickness(0, viewport.Height));
        return bounds.Right >= area.Left && area.Right >= bounds.Left
            && bounds.Bottom >= area.Top && area.Bottom >= bounds.Top;
    }
}
