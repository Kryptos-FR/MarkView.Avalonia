// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

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

    /// <summary>
    /// Enables Markdig footnote parsing for use with MarkView.Avalonia's footnote renderers.
    /// Activate on the viewer with <c>viewer.UseFootnotes()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseFootnotes(this MarkdownPipelineBuilder builder)
    {
        return builder.Use<Markdig.Extensions.Footnotes.FootnoteExtension>();
    }

    /// <summary>
    /// Enables Markdig GitHub-style alert block parsing (NOTE, WARNING, TIP, IMPORTANT, CAUTION).
    /// Activate on the viewer with <c>viewer.UseAlertBlocks()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseAlertBlocks(this MarkdownPipelineBuilder builder)
    {
        return builder.Use<Markdig.Extensions.Alerts.AlertExtension>();
    }
}
