// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Renderers;

namespace MarkView.Avalonia.Demo.ColorExtension;

/// <summary>
/// Markdig-side half of the colour span extension: registers <see cref="ColorSpanParser"/>
/// so <c>%[color:NAME]text%</c> parses into a <see cref="ColorSpanInline"/>.
/// Rendering that node is a separate concern, handled by <see cref="ColorSpanExtension"/>.
/// </summary>
public sealed class ColorMarkdownExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.InlineParsers.AddIfNotAlready<ColorSpanParser>();
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}

public static class ColorMarkdownExtensions
{
    /// <summary>
    /// Enables <c>%[color:NAME]text%</c> colour span parsing.
    /// Pair with <c>renderer.Extensions.Add(new ColorSpanExtension())</c> to render the result.
    /// </summary>
    public static MarkdownPipelineBuilder UseColorSpans(this MarkdownPipelineBuilder builder)
    {
        return builder.Use<ColorMarkdownExtension>();
    }
}
