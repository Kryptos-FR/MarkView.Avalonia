using Markdig;

namespace MarkView.Avalonia;

public static class MarkdownExtensions
{
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
