// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Extensions.Mathematics;

namespace MarkView.Avalonia.Math.Parsing;

/// <summary>
/// A <c>\[...\]</c> formula on lines of its own. Being a <see cref="MathBlock"/>, it is rendered like
/// <c>$$...$$</c>; only the delimiters copied with a selection differ.
/// </summary>
internal sealed class BracketMathBlock() : MathBlock(null!)
{
}
