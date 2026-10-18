// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

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
    /// rendered, so content it builds is laid out before it is ever seen.
    /// </summary>
    /// <param name="element">The element whose position decides; it hosts the deferred content.</param>
    /// <param name="realize">Builds the deferred content.</param>
    /// <remarks>
    /// Listens to <paramref name="element"/>'s own <see cref="Layoutable.EffectiveViewportChanged"/>
    /// event, which the layout manager only tracks while the element is in a visual tree: an element
    /// that is never shown, or is discarded first, never calls <paramref name="realize"/> and is
    /// collected normally.
    /// </remarks>
    public static void RunWhenNearViewport(Control element, Action realize)
    {
        element.EffectiveViewportChanged += OnEffectiveViewportChanged;

        void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            if (!IsNearViewport(element.Bounds.Size, e.EffectiveViewport)) return;
            element.EffectiveViewportChanged -= OnEffectiveViewportChanged;
            realize();
        }
    }

    // The viewport is in the element's own coordinates; it is widened by its own height so content
    // is realized a screen before it scrolls into view. Edges count as touching, since an element
    // whose content is not realized yet may have no height at all.
    internal static bool IsNearViewport(Size size, Rect viewport)
    {
        var area = viewport.Inflate(new Thickness(0, viewport.Height));
        return size.Width >= area.Left && area.Right >= 0
            && size.Height >= area.Top && area.Bottom >= 0;
    }
}
