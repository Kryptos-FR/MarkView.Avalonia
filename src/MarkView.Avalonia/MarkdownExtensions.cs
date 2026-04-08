using Markdig;

namespace MarkView.Avalonia;

/// <summary>
/// Extension methods for configuring Markdig pipelines for use with MarkView.Avalonia.
/// </summary>
public static class MarkdownExtensions
{
    /// <summary>
    /// Enables all Markdig extensions supported by MarkView.Avalonia.
    /// </summary>
    public static MarkdownPipelineBuilder UseSupportedExtensions(this MarkdownPipelineBuilder builder)
    {
        return builder
            .UseEmphasisExtras()
            .UseAutoLinks()
            .UseGridTables()
            .UsePipeTables()
            .UseTaskLists();
    }
}
