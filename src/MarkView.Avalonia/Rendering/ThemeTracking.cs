// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Layout;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Keeps content whose colours are baked in at render time (bitmaps, SVG, highlighted runs)
/// in sync with the application theme. Standard controls follow theme changes on their own
/// through <c>DynamicResource</c>; this covers content that must be rebuilt instead.
/// </summary>
public static class ThemeTracking
{
    /// <summary>
    /// Calls <paramref name="apply"/> whenever <paramref name="element"/> is in a visual tree and
    /// the application theme differs from the one in effect when the content was last built:
    /// on a theme switch while attached, and on attach after a switch that happened while detached.
    /// While the element is hidden (it or an ancestor has <see cref="Visual.IsVisible"/> off, as
    /// with a viewer on an inactive tab), the rebuild waits until it is shown again.
    /// The caller builds the initial content itself; <paramref name="apply"/> is not called here.
    /// </summary>
    /// <param name="element">
    /// The element that hosts the theme-dependent content. It must stay in the document for as
    /// long as the content should follow the theme.
    /// </param>
    /// <param name="apply">Rebuilds the content for the current application theme.</param>
    /// <remarks>
    /// The handler listens to <paramref name="element"/>'s own inherited
    /// <see cref="StyledElement.ActualThemeVariantChanged"/> event, so nothing outside the element
    /// references it: an element that is never shown, or is discarded, is collected normally.
    /// </remarks>
    public static void ReapplyOnThemeChange(StyledElement element, Action apply)
    {
        var appliedTheme = Application.Current?.ActualThemeVariant;
        var waitingToBeShown = false;
        element.ActualThemeVariantChanged += (_, _) => Reapply();

        void Reapply()
        {
            var theme = Application.Current?.ActualThemeVariant;
            if (theme == appliedTheme) return;

            // Visibility changes raise no public event, but showing the element runs a layout
            // pass. LayoutUpdated is only listened to while a rebuild is waiting, and a detached
            // element is not referenced by the layout manager.
            if (element is Layoutable { IsEffectivelyVisible: false } layoutable)
            {
                if (!waitingToBeShown)
                {
                    waitingToBeShown = true;
                    layoutable.LayoutUpdated += OnLayoutUpdated;
                }
                return;
            }

            appliedTheme = theme;
            apply();
        }

        void OnLayoutUpdated(object? sender, EventArgs e)
        {
            var layoutable = (Layoutable)element;
            if (!layoutable.IsEffectivelyVisible) return;
            layoutable.LayoutUpdated -= OnLayoutUpdated;
            waitingToBeShown = false;
            Reapply();
        }
    }
}
