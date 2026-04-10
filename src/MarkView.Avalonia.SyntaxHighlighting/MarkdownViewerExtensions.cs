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
    /// </summary>
    public static MarkdownViewer UseTextMateHighlighting(
        this MarkdownViewer viewer,
        ThemeName theme = ThemeName.DarkPlus)
    {
        viewer.Extensions.Add(new SyntaxHighlighting.TextMateExtension(theme));
        return viewer;
    }
}
