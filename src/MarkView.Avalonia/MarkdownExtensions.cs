// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;

namespace MarkView.Avalonia;

/// <summary>
/// Extension methods for configuring Markdig pipelines for use with MarkView.Avalonia.
/// Opt-in extensions such as footnotes or alert blocks are enabled with Markdig's own methods
/// (<c>UseFootnotes()</c>, <c>UseAlertBlocks()</c>…); this class deliberately defines no method
/// with the same name as one of Markdig's, since code importing both namespaces could not call it.
/// </summary>
public static class MarkdownExtensions
{
    /// <summary>
    /// Enables all Markdig extensions supported by MarkView.Avalonia.
    /// </summary>
    /// <param name="builder">The pipeline builder.</param>
    /// <param name="options">Options for the extensions enabled by this method. See <see cref="MarkdownExtensionsOptions"/>.</param>
    public static MarkdownPipelineBuilder UseSupportedExtensions(this MarkdownPipelineBuilder builder, MarkdownExtensionsOptions? options = null)
    {
        return builder
            .UseAutoLinks()
            .UseCjkFriendlyEmphasis()
            .UseEmojiAndSmiley(enableSmileys: false)
            .UseEmphasisExtras()
            .UseGridTables()
            .UsePipeTables(options?.PipeTable)
            .UseTaskLists()
            .UseYamlFrontMatter();
    }

    /// <summary>
    /// Enables Markdig hardline-break parsing: every soft line break within a paragraph renders
    /// as an explicit line break instead of collapsing to a space.
    /// Activate on the viewer with <c>viewer.UseHardlineBreaks()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseHardlineBreaks(this MarkdownPipelineBuilder builder)
    {
        return builder.UseSoftlineBreakAsHardlineBreak();
    }

    /// <summary>
    /// Enables Markdig GitHub-style alert block parsing (as <c>UseAlertBlocks()</c> does), plus the
    /// Obsidian-style extras that Markdig alone leaves as a plain blockquote: foldable alerts
    /// (<c>&gt; [!NOTE]-</c> starts collapsed, <c>&gt; [!NOTE]+</c> starts expanded) and custom titles
    /// (<c>&gt; [!TIP] My title</c>). Can be combined with Markdig's <c>UseAlertBlocks()</c> in either order.
    /// Activate on the viewer with <c>viewer.UseAlertBlocks()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseExtendedAlertBlocks(this MarkdownPipelineBuilder builder)
    {
        builder.Extensions.AddIfNotAlready<Parsing.ExtendedAlertExtension>();
        return builder.Use<Markdig.Extensions.Alerts.AlertExtension>();
    }
}
