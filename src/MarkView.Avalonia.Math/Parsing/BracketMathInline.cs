// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Extensions.Mathematics;

namespace MarkView.Avalonia.Math.Parsing;

/// <summary>
/// A formula written between LaTeX bracket delimiters inside a line: <c>\(...\)</c>, or <c>\[...\]</c>
/// sharing its line with other text. Being a <see cref="MathInline"/>, it is rendered like <c>$...$</c>;
/// only the delimiters copied with a selection differ.
/// </summary>
internal sealed class BracketMathInline : MathInline
{
    /// <summary>Whether the formula was written <c>\[...\]</c> rather than <c>\(...\)</c>.</summary>
    public bool IsDisplay { get; init; }

    /// <summary>
    /// Offsets of the formula's opening and closing backslash in the text <see cref="MathInline.Content"/>
    /// is a slice of: its paragraph's lines, joined by <c>'\n'</c>.
    /// </summary>
    public int TextStart { get; init; }

    /// <inheritdoc cref="TextStart"/>
    public int TextEnd { get; init; }
}
