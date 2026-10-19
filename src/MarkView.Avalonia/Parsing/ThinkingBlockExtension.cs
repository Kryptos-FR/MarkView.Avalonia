// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Parses the reasoning of a language model into <see cref="ThinkingBlock"/>s (see <see cref="ThinkingBlockParser"/>)
/// and gives its options to the <see cref="ThinkingBlockRenderer"/> of an <see cref="AvaloniaRenderer"/>. Added by
/// <see cref="MarkdownExtensions.UseThinkingBlocks"/>.
/// </summary>
internal sealed class ThinkingBlockExtension(ThinkingBlockOptions options) : IMarkdownExtension
{
    public ThinkingBlockOptions Options { get; } = options;

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.BlockParsers.TryRemove<ThinkingBlockParser>();
        var parser = new ThinkingBlockParser(Options.TagNames);
        // Before the HTML block parser, which would otherwise take the tags as HTML.
        if (!pipeline.BlockParsers.InsertBefore<HtmlBlockParser>(parser))
            pipeline.BlockParsers.Add(parser);
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is AvaloniaRenderer avaloniaRenderer && avaloniaRenderer.ObjectRenderers.Find<ThinkingBlockRenderer>() is { } thinkingRenderer)
            thinkingRenderer.Options = Options;
    }
}
