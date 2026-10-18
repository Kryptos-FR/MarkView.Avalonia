// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia;

/// <summary>
/// Options for <see cref="MarkdownViewer.Find"/>. Without any, the search is case-insensitive
/// (ordinal, no culture-specific rules) and matches the text anywhere, including inside a word.
/// </summary>
[Flags]
public enum MarkdownFindOptions
{
    /// <summary>Case-insensitive ordinal search, matching inside words too. Default.</summary>
    None = 0,

    /// <summary>Only matches text with the same case as the searched text (ordinal comparison).</summary>
    MatchCase = 1,

    /// <summary>
    /// Only matches text that is not preceded or followed by a letter, a digit or an underscore,
    /// so <c>cat</c> finds "cat" and "cat's" but neither "concat" nor "cats".
    /// </summary>
    WholeWord = 2,
}
