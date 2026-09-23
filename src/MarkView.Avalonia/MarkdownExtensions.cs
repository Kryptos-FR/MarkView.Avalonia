// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Extensions.Tables;

namespace MarkView.Avalonia;

/// <summary>
/// Extension methods for configuring Markdig pipelines for use with MarkView.Avalonia.
/// </summary>
public static class MarkdownExtensions
{
    /// <summary>
    /// Enables all Markdig extensions supported by MarkView.Avalonia.
    /// </summary>
    /// <param name="builder">The pipeline builder.</param>
    /// <param name="pipeTableOptions">
    /// Options for pipe table parsing, e.g. <see cref="PipeTableOptions.InferColumnWidthsFromSeparator"/>
    /// to size <see cref="Rendering.Blocks.TableRenderer"/> columns proportionally to the header separator's dash counts.
    /// </param>
    public static MarkdownPipelineBuilder UseSupportedExtensions(this MarkdownPipelineBuilder builder, PipeTableOptions? pipeTableOptions = null)
    {
        return builder
            .UseAutoLinks()
            .UseCjkFriendlyEmphasis()
            .UseEmojiAndSmiley(enableSmileys: false)
            .UseEmphasisExtras()
            .UseGridTables()
            .UsePipeTables(pipeTableOptions)
            .UseTaskLists()
            .UseYamlFrontMatter();
    }

    /// <summary>
    /// Enables Markdig abbreviation parsing. Matched words gain tooltip definitions.
    /// Activate on the viewer with <c>viewer.UseAbbreviations()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseAbbreviations(this MarkdownPipelineBuilder builder)
    {
        return builder.Use<Markdig.Extensions.Abbreviations.AbbreviationExtension>();
    }

    /// <summary>
    /// Enables Markdig GitHub-style alert block parsing (NOTE, WARNING, TIP, IMPORTANT, CAUTION).
    /// Activate on the viewer with <c>viewer.UseAlertBlocks()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseAlertBlocks(this MarkdownPipelineBuilder builder)
    {
        return builder.Use<Markdig.Extensions.Alerts.AlertExtension>();
    }

    /// <summary>
    /// Enables Markdig citation parsing (<c>""quoted text""</c> renders as an italic citation span).
    /// Activate on the viewer with <c>viewer.UseCitations()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseCitations(this MarkdownPipelineBuilder builder)
    {
        return builder.Use<Markdig.Extensions.Citations.CitationExtension>();
    }

    /// <summary>
    /// Enables Markdig figure block parsing (^^^ fences with optional caption).
    /// Activate on the viewer with <c>viewer.UseFigures()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseFigures(this MarkdownPipelineBuilder builder)
    {
        return builder.Use<Markdig.Extensions.Figures.FigureExtension>();
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
    /// Enables Markdig hardline-break parsing: every soft line break within a paragraph renders
    /// as an explicit line break instead of collapsing to a space.
    /// Activate on the viewer with <c>viewer.UseHardlineBreaks()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseHardlineBreaks(this MarkdownPipelineBuilder builder)
    {
        return builder.UseSoftlineBreakAsHardlineBreak();
    }

    /// <summary>
    /// Enables Markdig media link parsing. YouTube image-syntax links are rendered
    /// as clickable thumbnail embeds. Activate on the viewer with <c>viewer.UseMediaLinks()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseMediaLinks(this MarkdownPipelineBuilder builder)
    {
        return builder.Use<Markdig.Extensions.MediaLinks.MediaLinkExtension>();
    }
}
