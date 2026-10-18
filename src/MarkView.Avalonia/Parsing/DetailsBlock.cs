// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// An HTML <c>&lt;details&gt;</c> element: the blocks between a <c>&lt;details&gt;</c> tag and its matching
/// <c>&lt;/details&gt;</c>, shown under a summary line that folds them. Created by <see cref="HtmlDetailsGrouper"/>
/// from the HTML blocks Markdig parses the tags into.
/// </summary>
internal sealed class DetailsBlock() : ContainerBlock(null)
{
    /// <summary><c>true</c> for <c>&lt;details open&gt;</c>: the body is shown at first.</summary>
    public bool StartsExpanded { get; init; }

    /// <summary>
    /// The plain text of the <c>&lt;summary&gt;</c> element, or <c>null</c> when there is none
    /// (browsers then show "Details").
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>The HTML inside the <c>&lt;summary&gt;</c> element, or <c>null</c> when there is none.</summary>
    public string? SummaryHtml { get; set; }

    /// <summary>
    /// The inlines of the summary, built from <see cref="SummaryHtml"/> by <see cref="HtmlSubsetGrouper"/> when the
    /// pipeline has the HTML subset; shown instead of <see cref="Summary"/> when set.
    /// </summary>
    public Markdig.Syntax.Inlines.ContainerInline? SummaryInline { get; set; }
}
