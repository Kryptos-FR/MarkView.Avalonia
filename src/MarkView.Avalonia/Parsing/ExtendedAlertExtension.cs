// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Swaps Markdig's <see cref="AlertInlineParser"/> for <see cref="ExtendedAlertInlineParser"/>, which also
/// parses foldable alerts and custom alert titles. Added alongside <see cref="AlertExtension"/> by
/// <see cref="MarkdownExtensions.UseAlertBlocks"/>, in either order: <see cref="AlertExtension"/> finds the
/// derived parser and keeps it, only applying its <see cref="AlertExtension.AllowNestedAlerts"/> option.
/// </summary>
internal sealed class ExtendedAlertExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        var existing = pipeline.InlineParsers.Find<AlertInlineParser>();
        var parser = new ExtendedAlertInlineParser { AllowNestedAlerts = existing?.AllowNestedAlerts ?? false };
        if (existing is null)
            pipeline.InlineParsers.InsertBefore<LinkInlineParser>(parser);
        else
            pipeline.InlineParsers.Replace<AlertInlineParser>(parser);
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}
