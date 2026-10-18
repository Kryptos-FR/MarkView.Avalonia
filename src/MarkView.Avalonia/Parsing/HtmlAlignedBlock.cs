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
/// Blocks aligned by the <c>align</c> attribute of an HTML element: the blocks between a
/// <c>&lt;div align="…"&gt;</c> and its matching <c>&lt;/div&gt;</c>, or the paragraph or heading of a
/// <c>&lt;p align="…"&gt;</c> or <c>&lt;h1 align="…"&gt;</c>. Created by <see cref="HtmlSubsetGrouper"/>.
/// </summary>
internal sealed class HtmlAlignedBlock(HtmlAlignment alignment) : ContainerBlock(null)
{
    public HtmlAlignment Alignment { get; } = alignment;
}
