// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Extensions.Tables;

namespace MarkView.Avalonia;

/// <summary>
/// Options for <see cref="MarkdownExtensions.UseSupportedExtensions(Markdig.MarkdownPipelineBuilder, MarkdownExtensionsOptions?)"/>.
/// New options are added as properties here rather than as additional parameters, so the method's signature stays stable.
/// </summary>
public sealed record MarkdownExtensionsOptions
{
    /// <summary>
    /// Options for pipe table parsing, e.g. <see cref="PipeTableOptions.InferColumnWidthsFromSeparator"/>
    /// to size <see cref="Rendering.Blocks.TableRenderer"/> columns proportionally to the header separator's dash counts.
    /// </summary>
    public PipeTableOptions? PipeTable { get; init; }
}
