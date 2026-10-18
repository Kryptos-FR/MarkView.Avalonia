// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Mermaid;

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extensions for attaching Mermaid diagram rendering to a <see cref="MarkdownViewer"/>.
/// </summary>
public static class MarkdownViewerMermaidExtensions
{
    /// <summary>
    /// Adds <see cref="Mermaid.MermaidExtension"/> to the extension list.
    /// </summary>
    /// <param name="extensions">The extension list.</param>
    /// <param name="style">The visual style diagrams are painted in.</param>
    /// <example>
    /// <code>
    /// // Global (App.axaml.cs)
    /// MarkdownViewerDefaults.Extensions.AddMermaid();
    /// // Per-instance, as a technical drawing
    /// viewer.Extensions.AddMermaid(MermaidStyle.Blueprint);
    /// </code>
    /// </example>
    public static void AddMermaid(this IList<IMarkViewExtension> extensions, MermaidStyle style = MermaidStyle.Quiet)
    {
        extensions.Add(new MermaidExtension(style));
    }

    /// <summary>
    /// Adds <see cref="Mermaid.MermaidExtension"/> to the viewer's
    /// <see cref="MarkdownViewer.Extensions"/> list.
    /// </summary>
    /// <param name="viewer">The viewer.</param>
    /// <param name="style">The visual style diagrams are painted in.</param>
    public static MarkdownViewer UseMermaid(this MarkdownViewer viewer, MermaidStyle style = MermaidStyle.Quiet)
    {
        viewer.Extensions.AddMermaid(style);
        return viewer;
    }
}
