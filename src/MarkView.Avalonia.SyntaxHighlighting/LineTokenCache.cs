// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

using TextMateSharp.Grammars;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Keeps the tokens of the lines a highlighter has seen recently, by grammar and line text, so that a code
/// block rendered again (on each update of a streamed document, the whole open block is) only tokenises the
/// lines it has not seen.
/// </summary>
/// <remarks>
/// The line text is the whole key because each line is tokenised on its own, without the state the previous
/// line ended in. The cache holds at most <see cref="Budget"/> characters of lines; when a line does not fit,
/// it starts again empty. A line longer than <see cref="MaxLineLength"/> is not kept.
/// </remarks>
internal sealed class LineTokenCache<T>
    where T : class
{
    /// <summary>The number of characters of lines the cache holds at most.</summary>
    public const int Budget = 64 * 1024;

    /// <summary>The length of the longest line the cache keeps.</summary>
    public const int MaxLineLength = 1024;

    private readonly Dictionary<IGrammar, Dictionary<string, T>> _lines = [];

    /// <summary>The number of characters of the lines the cache holds.</summary>
    public int Length { get; private set; }

    public bool TryGet(IGrammar grammar, ReadOnlySpan<char> line, [MaybeNullWhen(false)] out T tokens)
    {
        if (_lines.TryGetValue(grammar, out var lines))
            return lines.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(line, out tokens);
        tokens = null;
        return false;
    }

    public void Add(IGrammar grammar, ReadOnlySpan<char> line, T tokens)
    {
        if (line.Length > MaxLineLength)
            return;
        if (Length + line.Length > Budget)
        {
            _lines.Clear();
            Length = 0;
        }

        if (!_lines.TryGetValue(grammar, out var lines))
            _lines[grammar] = lines = new Dictionary<string, T>(StringComparer.Ordinal);
        if (lines.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(line, tokens))
            Length += line.Length;
    }
}
