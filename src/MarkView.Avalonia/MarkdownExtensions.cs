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
    /// alerts (<c>&gt; [!NOTE]</c>), footnotes, CJK-friendly emphasis, hidden YAML front matter,
    /// collapsible HTML <c>&lt;details&gt;</c> blocks (see <see cref="UseHtmlDetails"/>) and the HTML elements
    /// READMEs use, such as centered headers, badges and <c>&lt;kbd&gt;</c> (see <see cref="UseHtmlSubset"/>).
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
            .UseHtmlSubset()
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
    /// <c>==mark==</c>), autolinks, emoji shortcodes, CJK-friendly emphasis, hidden YAML front matter and the
    /// HTML subset (see <see cref="UseHtmlSubset"/>). Unlike <see cref="UseGitHubExtensions"/>, <c>~text~</c> is
    /// subscript, and alerts, footnotes and details blocks are not enabled.
    /// </summary>
    /// <param name="builder">The pipeline builder.</param>
    /// <param name="options">Options for the extensions enabled by this method. See <see cref="MarkdownExtensionsOptions"/>.</param>
    public static MarkdownPipelineBuilder UseSupportedExtensions(this MarkdownPipelineBuilder builder, MarkdownExtensionsOptions? options = null)
    {
        return builder
            .UseHtmlSubset()
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
    /// Renders a safe subset of HTML with native controls, as GitHub shows it in a README, without running or
    /// fetching anything but images:
    /// <list type="bullet">
    /// <item>inline <c>&lt;b&gt;</c>/<c>&lt;strong&gt;</c>, <c>&lt;i&gt;</c>/<c>&lt;em&gt;</c>,
    /// <c>&lt;del&gt;</c>/<c>&lt;s&gt;</c>/<c>&lt;strike&gt;</c>, <c>&lt;ins&gt;</c>/<c>&lt;u&gt;</c>, <c>&lt;mark&gt;</c>,
    /// <c>&lt;sub&gt;</c>, <c>&lt;sup&gt;</c>, <c>&lt;kbd&gt;</c> and <c>&lt;code&gt;</c>; <c>&lt;a href&gt;</c> as a link
    /// and <c>&lt;img src alt width height&gt;</c> as an image, like their markdown counterparts; <c>&lt;br&gt;</c>;</item>
    /// <item>HTML blocks of <c>&lt;p&gt;</c>, <c>&lt;div&gt;</c> and <c>&lt;h1&gt;</c>…<c>&lt;h6&gt;</c> elements, with their
    /// <c>align</c> attribute, holding inline HTML; <c>&lt;hr&gt;</c>; and a <c>&lt;div align="center"&gt;</c> around
    /// markdown, separated from it by blank lines.</item>
    /// </list>
    /// Only the <c>href</c>, <c>src</c>, <c>alt</c>, <c>title</c>, <c>width</c>, <c>height</c> and <c>align</c>
    /// attributes are read. Any other tag, and a tag without its match, is dropped, and the text around it
    /// shows; the content of <c>&lt;script&gt;</c> and <c>&lt;style&gt;</c> is dropped too. So is the content of
    /// <c>&lt;svg&gt;</c>, but a tag that names no HTML element, such as the <c>&lt;string&gt;</c> of
    /// <c>List&lt;string&gt;</c>, shows as text. Part of <see cref="UseGitHubExtensions"/> and
    /// <see cref="UseSupportedExtensions"/>; <see cref="UseHtmlMode"/> turns it off.
    /// </summary>
    public static MarkdownPipelineBuilder UseHtmlSubset(this MarkdownPipelineBuilder builder)
    {
        builder.Extensions.AddIfNotAlready<Parsing.HtmlSubsetExtension>();
        return builder;
    }

    /// <summary>
    /// Sets how the pipeline treats HTML, whichever extensions are added before or after it; the last call wins.
    /// <see cref="HtmlMode.SafeSubset"/> renders the HTML subset and details blocks (see
    /// <see cref="UseHtmlSubset"/> and <see cref="UseHtmlDetails"/>), and adds them if needed;
    /// <see cref="HtmlMode.Escape"/> shows HTML as literal text, and <see cref="HtmlMode.Ignore"/> renders none.
    /// Without it, a pipeline renders the HTML of the extensions it has: <see cref="UseGitHubExtensions"/> and
    /// <see cref="UseSupportedExtensions"/> render the subset.
    /// </summary>
    /// <example>
    /// For the messages of a chat, whose <c>&lt;tags&gt;</c> are text:
    /// <code>
    /// MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
    ///     .UseGitHubExtensions()
    ///     .UseHtmlMode(HtmlMode.Escape)
    ///     .Build();
    /// </code>
    /// </example>
    public static MarkdownPipelineBuilder UseHtmlMode(this MarkdownPipelineBuilder builder, HtmlMode mode)
    {
        if (builder.Extensions.Find<Parsing.HtmlModeExtension>() is { } extension)
            extension.Mode = mode;
        else
            builder.Extensions.Add(new Parsing.HtmlModeExtension(mode));

        return mode == HtmlMode.SafeSubset ? builder.UseHtmlDetails().UseHtmlSubset() : builder;
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
