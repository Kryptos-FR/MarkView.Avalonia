// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extensions for rendering the fenced code blocks of a language as an application's
/// own control (see <see cref="FenceRendererExtension"/>).
/// </summary>
public static class MarkdownViewerFenceRendererExtensions
{
    /// <summary>
    /// Adds a <see cref="FenceRendererExtension"/> for <paramref name="language"/> to the extension list.
    /// </summary>
    /// <example>
    /// <code>
    /// // Global (App.axaml.cs)
    /// MarkdownViewerDefaults.Extensions.AddFenceRenderer("chart", (code, info) => new MyChart(code));
    /// // Per-instance
    /// viewer.Extensions.AddFenceRenderer("chart", (code, info) => new MyChart(code));
    /// </code>
    /// </example>
    public static void AddFenceRenderer(this IList<IMarkViewExtension> extensions, string language, FenceRendererFactory factory)
    {
        extensions.Add(new FenceRendererExtension(language, factory));
    }

    /// <summary>
    /// Renders the viewer's fenced code blocks of <paramref name="language"/> as the control
    /// <paramref name="factory"/> returns, replacing a fence renderer the viewer's
    /// <see cref="MarkdownViewer.Extensions"/> already had for that language, and re-renders the document.
    /// </summary>
    /// <example>
    /// <code>
    /// viewer.UseFenceRenderer("chart", (code, info) => new MyChart(code));
    /// </code>
    /// </example>
    public static MarkdownViewer UseFenceRenderer(this MarkdownViewer viewer, string language, FenceRendererFactory factory)
    {
        var extension = new FenceRendererExtension(language, factory);
        var extensions = viewer.Extensions;
        for (var i = extensions.Count - 1; i >= 0; i--)
        {
            if (extensions[i] is FenceRendererExtension existing
                && string.Equals(existing.Language, language, StringComparison.OrdinalIgnoreCase))
                extensions.RemoveAt(i);
        }
        extensions.Add(extension);
        viewer.RenderMarkdown();
        return viewer;
    }
}
