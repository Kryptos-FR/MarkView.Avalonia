// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extensions for attaching SVG support to a <see cref="MarkdownViewer"/>.
/// </summary>
public static class MarkdownViewerSvgExtensions
{
    /// <summary>
    /// Adds <see cref="Svg.SvgExtension"/> to the extension list.
    /// </summary>
    /// <example>
    /// <code>
    /// // Global (App.axaml.cs)
    /// MarkdownViewerDefaults.Extensions.AddSvg();
    /// // Per-instance
    /// viewer.Extensions.AddSvg();
    /// </code>
    /// </example>
    public static void AddSvg(this IList<IMarkViewExtension> extensions)
    {
        extensions.Add(new Svg.SvgExtension());
    }

    /// <summary>
    /// Adds <see cref="Svg.SvgExtension"/> to the viewer's
    /// <see cref="MarkdownViewer.Extensions"/> list.
    /// </summary>
    public static MarkdownViewer UseSvg(this MarkdownViewer viewer)
    {
        viewer.Extensions.AddSvg();
        return viewer;
    }
}
