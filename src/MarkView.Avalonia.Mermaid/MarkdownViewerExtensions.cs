// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extensions for attaching Mermaid diagram rendering to a <see cref="MarkdownViewer"/>.
/// </summary>
public static class MarkdownViewerMermaidExtensions
{
    /// <summary>
    /// Adds <see cref="Mermaid.MermaidExtension"/> to the extension list.
    /// </summary>
    /// <example>
    /// <code>
    /// // Global (App.axaml.cs)
    /// MarkdownViewerDefaults.Extensions.AddMermaid();
    /// // Per-instance
    /// viewer.Extensions.AddMermaid();
    /// </code>
    /// </example>
    public static void AddMermaid(this IList<IMarkViewExtension> extensions)
    {
        extensions.Add(new Mermaid.MermaidExtension());
    }

    /// <summary>
    /// Adds <see cref="Mermaid.MermaidExtension"/> to the viewer's
    /// <see cref="MarkdownViewer.Extensions"/> list.
    /// </summary>
    public static MarkdownViewer UseMermaid(this MarkdownViewer viewer)
    {
        viewer.Extensions.AddMermaid();
        return viewer;
    }
}
