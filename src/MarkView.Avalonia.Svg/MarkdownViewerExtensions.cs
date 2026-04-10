// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extensions for attaching SVG support to a <see cref="MarkdownViewer"/>.
/// </summary>
public static class MarkdownViewerSvgExtensions
{
    /// <summary>
    /// Adds <see cref="Svg.SvgExtension"/> to the viewer's
    /// <see cref="MarkdownViewer.Extensions"/> list.
    /// </summary>
    public static MarkdownViewer UseSvg(this MarkdownViewer viewer)
    {
        viewer.Extensions.Add(new Svg.SvgExtension());
        return viewer;
    }
}
