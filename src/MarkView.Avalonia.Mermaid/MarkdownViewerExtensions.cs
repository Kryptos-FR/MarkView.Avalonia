namespace MarkView.Avalonia;

/// <summary>
/// Convenience extensions for attaching Mermaid diagram rendering to a <see cref="MarkdownViewer"/>.
/// </summary>
public static class MarkdownViewerMermaidExtensions
{
    /// <summary>
    /// Adds <see cref="Mermaid.MermaidExtension"/> to the viewer's
    /// <see cref="MarkdownViewer.Extensions"/> list.
    /// </summary>
    public static MarkdownViewer UseMermaid(this MarkdownViewer viewer)
    {
        viewer.Extensions.Add(new Mermaid.MermaidExtension());
        return viewer;
    }
}
