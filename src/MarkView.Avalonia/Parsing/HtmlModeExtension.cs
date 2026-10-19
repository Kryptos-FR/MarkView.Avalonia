// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Renderers;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Records the <see cref="HtmlMode"/> of a pipeline (see <see cref="MarkdownExtensions.UseHtmlMode"/>).
/// <see cref="HtmlSubsetExtension"/> and <see cref="HtmlDetailsExtension"/> build no HTML in another mode than
/// <see cref="HtmlMode.SafeSubset"/>, whatever the order they were added in, and <see cref="HtmlMode.Escape"/>
/// disables HTML parsing, as Markdig's <c>DisableHtml()</c> does.
/// </summary>
internal sealed class HtmlModeExtension(HtmlMode mode) : IMarkdownExtension
{
    public HtmlMode Mode { get; set; } = mode;

    /// <summary>Whether <paramref name="pipeline"/> builds the HTML subset: unless another mode is set.</summary>
    public static bool BuildsHtml(MarkdownPipelineBuilder pipeline) =>
        pipeline.Extensions.Find<HtmlModeExtension>() is not { Mode: not HtmlMode.SafeSubset };

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (Mode == HtmlMode.Escape)
            pipeline.DisableHtml();
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}
