// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;

namespace MarkView.Avalonia;

/// <summary>
/// Markdig pipeline extensions for LaTeX math.
/// </summary>
public static class MarkdownMathExtensions
{
    /// <summary>
    /// Enables LaTeX's bracket delimiters for math, which LLMs commonly emit: <c>\(...\)</c> for an inline
    /// formula and <c>\[...\]</c> for a display formula, rendered as a block when it has its lines to itself
    /// and inline otherwise. Markdig's <c>UseMathematics()</c> only knows <c>$...$</c>/<c>$$...$$</c>; the two
    /// can be combined in either order, or this one used alone to keep <c>$</c> a plain character.
    /// Once enabled, <c>\(</c> and <c>\[</c> no longer escape a parenthesis or bracket when they open a
    /// formula; an opening glued to a letter or digit (<c>arr\[i\]</c>) or left unclosed still does.
    /// Called by <c>viewer.UseMath()</c>.
    /// </summary>
    public static MarkdownPipelineBuilder UseMathBracketDelimiters(this MarkdownPipelineBuilder builder)
    {
        builder.Extensions.AddIfNotAlready<Math.Parsing.BracketMathExtension>();
        return builder;
    }
}
