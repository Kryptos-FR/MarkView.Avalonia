// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;

namespace MarkView.Avalonia.Parsing;

/// <summary>The value of an HTML <c>align</c> attribute.</summary>
internal enum HtmlAlignment
{
    Left,
    Center,
    Right,
    Justify,
}

/// <summary>
/// Blocks aligned by the <c>align</c> attribute of an HTML element, or given a direction by its <c>dir</c>
/// attribute: the blocks between a <c>&lt;div align="…"&gt;</c> or <c>&lt;div dir="…"&gt;</c> and its matching
/// <c>&lt;/div&gt;</c>, or the paragraph or heading of a <c>&lt;p align="…"&gt;</c> or <c>&lt;h1 align="…"&gt;</c>.
/// The direction is kept as the block's explicit direction (see <see cref="Rendering.TextDirection.Explicit"/>).
/// Created by <see cref="HtmlSubsetGrouper"/>.
/// </summary>
internal sealed class HtmlAlignedBlock(HtmlAlignment? alignment) : ContainerBlock(null)
{
    /// <summary>The alignment of the <c>align</c> attribute, or <c>null</c> when the element has a direction only.</summary>
    public HtmlAlignment? Alignment { get; } = alignment;
}
