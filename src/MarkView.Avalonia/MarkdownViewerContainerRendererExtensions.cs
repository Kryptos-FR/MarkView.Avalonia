// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extensions for rendering the custom containers of a name as an application's own
/// control (see <see cref="ContainerRendererExtension"/>).
/// </summary>
public static class MarkdownViewerContainerRendererExtensions
{
    /// <summary>
    /// Adds a <see cref="ContainerRendererExtension"/> for <paramref name="name"/> to the extension list.
    /// </summary>
    /// <example>
    /// <code>
    /// // Global (App.axaml.cs)
    /// MarkdownViewerDefaults.Extensions.AddContainerRenderer("spoiler",
    ///     (content, title) => new Expander { Header = title, Content = content });
    /// // Per-instance
    /// viewer.Extensions.AddContainerRenderer("spoiler",
    ///     (content, title) => new Expander { Header = title, Content = content });
    /// </code>
    /// </example>
    public static void AddContainerRenderer(this IList<IMarkViewExtension> extensions, string name, ContainerRendererFactory factory)
    {
        extensions.Add(new ContainerRendererExtension(name, factory));
    }

    /// <summary>
    /// Renders the viewer's custom containers named <paramref name="name"/> as the control
    /// <paramref name="factory"/> returns, replacing a container renderer the viewer's
    /// <see cref="MarkdownViewer.Extensions"/> already had for that name, and re-renders the document.
    /// Custom containers need Markdig's <c>UseCustomContainers()</c> in the viewer's pipeline.
    /// </summary>
    /// <example>
    /// <code>
    /// viewer.UseContainerRenderer("spoiler", (content, title) => new Expander { Header = title, Content = content });
    /// </code>
    /// </example>
    public static MarkdownViewer UseContainerRenderer(this MarkdownViewer viewer, string name, ContainerRendererFactory factory)
    {
        var extension = new ContainerRendererExtension(name, factory);
        var extensions = viewer.Extensions;
        for (var i = extensions.Count - 1; i >= 0; i--)
        {
            if (extensions[i] is ContainerRendererExtension existing
                && string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
                extensions.RemoveAt(i);
        }
        extensions.Add(extension);
        viewer.RenderMarkdown();
        return viewer;
    }
}
