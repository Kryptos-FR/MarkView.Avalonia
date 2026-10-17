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
    /// Enables the Markdig extensions that make a document render as it does on GitHub: pipe tables,
    /// task lists, strikethrough (<c>~~text~~</c> and <c>~text~</c>), autolinks, emoji shortcodes,
    /// alerts (<c>&gt; [!NOTE]</c>), footnotes, CJK-friendly emphasis, hidden YAML front matter and
    /// collapsible HTML <c>&lt;details&gt;</c> blocks (see <see cref="UseHtmlDetails"/>).
    /// This is the pipeline a <see cref="MarkdownViewer"/> uses when none is set. Math and Mermaid
    /// diagrams come from their own packages (<c>UseMath()</c>, <c>UseMermaid()</c>).
    /// </summary>
    /// <param name="builder">The pipeline builder.</param>
    /// <param name="options">Options for the extensions enabled by this method. See <see cref="MarkdownExtensionsOptions"/>.</param>
    public static MarkdownPipelineBuilder UseGitHubExtensions(this MarkdownPipelineBuilder builder, MarkdownExtensionsOptions? options = null)
    {
        builder.Extensions.AddIfNotAlready<Parsing.GitHubStrikethroughExtension>();
        return builder
            .UseHtmlDetails()
            .UseAlertBlocks()
            .UseAutoLinks()
            .UseCjkFriendlyEmphasis()
            .UseEmojiAndSmiley(enableSmileys: false)
            .UseFootnotes()
            .UsePipeTables(options?.PipeTable)
            .UseTaskLists()
            .UseYamlFrontMatter();
    }

    /// <summary>
    /// Enables a broader set of Markdig extensions supported by MarkView.Avalonia: pipe and grid tables,
    /// task lists, all emphasis extras (<c>~~strike~~</c>, <c>~sub~</c>, <c>^sup^</c>, <c>++ins++</c>,
    /// <c>==mark==</c>), autolinks, emoji shortcodes, CJK-friendly emphasis and hidden YAML front matter.
    /// Unlike <see cref="UseGitHubExtensions"/>, <c>~text~</c> is subscript, and alerts and footnotes are
    /// not enabled.
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
    /// Renders HTML <c>&lt;details&gt;</c> / <c>&lt;summary&gt;</c> elements as collapsible blocks, as GitHub
    /// does: the summary line (or "Details" without one) toggles the body, which starts hidden unless the tag
    /// is <c>&lt;details open&gt;</c>. The markdown between the tags, separated from them by blank lines, is
    /// rendered as usual; details elements can nest. Tags without a match, and any other HTML, are left as
    /// they are. Part of <see cref="UseGitHubExtensions"/>.
    /// </summary>
    public static MarkdownPipelineBuilder UseHtmlDetails(this MarkdownPipelineBuilder builder)
    {
        builder.Extensions.AddIfNotAlready<Parsing.HtmlDetailsExtension>();
        return builder;
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
