// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Parsers;
using Markdig.Syntax;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// The reasoning a language model writes between tags such as <c>&lt;think&gt;</c> and <c>&lt;/think&gt;</c>
/// before its answer, parsed as markdown by <see cref="ThinkingBlockParser"/>.
/// </summary>
internal sealed class ThinkingBlock(BlockParser parser) : ContainerBlock(parser)
{
    /// <summary>The tag's name as written, e.g. <c>think</c>.</summary>
    public string TagName { get; init; } = string.Empty;

    /// <summary>
    /// <c>true</c> once the closing tag was found; <c>false</c> while the model is still thinking, or when its
    /// output stopped before the closing tag.
    /// </summary>
    public bool HasClosingTag { get; internal set; }

    /// <summary>
    /// Set when the closing tag ended a line that also held content: that line still belongs to the block,
    /// which closes before the next one.
    /// </summary>
    internal bool ClosesBeforeNextLine { get; set; }
}
