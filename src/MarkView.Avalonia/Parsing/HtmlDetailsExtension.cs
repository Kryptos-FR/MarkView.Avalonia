// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Renderers;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Turns HTML <c>&lt;details&gt;</c> / <c>&lt;summary&gt;</c> elements into <see cref="DetailsBlock"/>s once
/// Markdig has parsed a document (see <see cref="HtmlDetailsGrouper"/>). Added by
/// <see cref="MarkdownExtensions.UseHtmlDetails"/> and <see cref="MarkdownExtensions.UseGitHubExtensions"/>.
/// With <see cref="HtmlSubsetExtension"/> in the same pipeline, that extension groups them instead, before it builds
/// the rest of the HTML.
/// </summary>
internal sealed class HtmlDetailsExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.Extensions.Contains<HtmlSubsetExtension>())
            pipeline.DocumentProcessed += HtmlDetailsGrouper.Process;
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}
