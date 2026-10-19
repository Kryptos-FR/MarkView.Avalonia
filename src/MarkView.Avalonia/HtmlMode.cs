// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia;

/// <summary>
/// How a pipeline treats the HTML in a document. Set with
/// <see cref="MarkdownExtensions.UseHtmlMode(Markdig.MarkdownPipelineBuilder, HtmlMode)"/>.
/// In every mode, nothing is run and nothing is fetched but the images of the subset.
/// </summary>
public enum HtmlMode
{
    /// <summary>
    /// Renders the safe subset of HTML READMEs use, and collapsible <c>&lt;details&gt;</c> blocks, with native
    /// controls (see <see cref="MarkdownExtensions.UseHtmlSubset"/> and <see cref="MarkdownExtensions.UseHtmlDetails"/>).
    /// Other HTML elements are dropped and their text shows, as on GitHub. The mode of the default pipeline.
    /// </summary>
    SafeSubset,

    /// <summary>
    /// Shows HTML as the literal text it was typed as, <c>&lt;b&gt;x&lt;/b&gt;</c> included, like Markdig's
    /// <c>DisableHtml()</c>. Markdown between the tags of what would have been an HTML block is parsed as usual.
    /// For text whose tags are not meant as HTML, such as the messages of a chat.
    /// </summary>
    Escape,

    /// <summary>
    /// Renders no HTML: tags are dropped, the text between inline tags shows, and HTML blocks do not show.
    /// <c>&lt;br&gt;</c> still breaks the line, since markdown has no other way to do so in a table cell.
    /// </summary>
    Ignore,
}
