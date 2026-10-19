// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Renderers;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Turns the HTML of the HTML subset into blocks and inlines MarkView renders once Markdig has parsed a document
/// (see <see cref="HtmlSubsetGrouper"/>). Added by <see cref="MarkdownExtensions.UseHtmlSubset"/>,
/// <see cref="MarkdownExtensions.UseGitHubExtensions"/> and <see cref="MarkdownExtensions.UseSupportedExtensions"/>.
/// </summary>
/// <remarks>
/// With <see cref="HtmlDetailsExtension"/> in the same pipeline, in either order, the details elements are grouped
/// first, keeping the HTML text inside them (and their summary) as HTML for this extension to build. Neither
/// builds anything in another <see cref="HtmlMode"/> than <see cref="HtmlMode.SafeSubset"/>.
/// </remarks>
internal sealed class HtmlSubsetExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!HtmlModeExtension.BuildsHtml(pipeline))
            return;
        if (pipeline.Extensions.Contains<HtmlDetailsExtension>())
            pipeline.DocumentProcessed += document => HtmlDetailsGrouper.Process(document, keepHtml: true);
        pipeline.DocumentProcessed += HtmlSubsetGrouper.Process;
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}
