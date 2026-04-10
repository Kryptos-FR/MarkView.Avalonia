// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using TextMateSharp.Grammars;

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extensions for attaching TextMate highlighting to a <see cref="MarkdownViewer"/>.
/// </summary>
public static class MarkdownViewerSyntaxHighlightingExtensions
{
    /// <summary>
    /// Adds <see cref="SyntaxHighlighting.TextMateExtension"/> to the viewer's
    /// <see cref="MarkdownViewer.Extensions"/> list.
    /// The extension automatically selects the appropriate highlighter theme based on
    /// the current <see cref="Avalonia.Styling.ThemeVariant"/>.
    /// </summary>
    public static MarkdownViewer UseTextMateHighlighting(
        this MarkdownViewer viewer,
        ThemeName darkTheme = ThemeName.DarkPlus,
        ThemeName lightTheme = ThemeName.LightPlus)
    {
        viewer.Extensions.Add(new SyntaxHighlighting.TextMateExtension(darkTheme, lightTheme));
        return viewer;
    }
}
