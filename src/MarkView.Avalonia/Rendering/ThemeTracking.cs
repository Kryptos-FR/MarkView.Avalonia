// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;

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
        element.ActualThemeVariantChanged += (_, _) =>
        {
            var theme = Application.Current?.ActualThemeVariant;
            if (theme == appliedTheme) return;
            appliedTheme = theme;
            apply();
        };
    }
}
