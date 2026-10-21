// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

using TextMateSharp.Grammars;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Keeps the tokens of the lines a highlighter has seen recently, and the state each line ended in, by grammar,
/// the state the line started from and line text, so that a code block rendered again (on each update of a
/// streamed document, the whole open block is) only tokenises the lines it has not seen.
/// </summary>
/// <remarks>
/// TextMate states compare by value, so a line is found wherever it starts from the same state as before:
/// a block seen before is found line after line, up to its first changed line, and the lines after a
/// change are found again once the state they start from is back to one seen before. The cache holds at most
/// <see cref="Budget"/> characters of lines; when a line does not fit, it starts again empty. A line longer
/// than <see cref="MaxLineLength"/> is not kept.
/// </remarks>
internal sealed class LineTokenCache<T>
    where T : class
{
    /// <summary>The number of characters of lines the cache holds at most.</summary>
    public const int Budget = 64 * 1024;

    /// <summary>The length of the longest line the cache keeps.</summary>
    public const int MaxLineLength = 1024;

    private readonly Dictionary<(IGrammar Grammar, IStateStack? Start), Dictionary<string, (T Tokens, IStateStack End)>> _lines = [];

    /// <summary>The number of characters of the lines the cache holds.</summary>
    public int Length { get; private set; }

    public bool TryGet(
        IGrammar grammar,
        IStateStack? start,
        ReadOnlySpan<char> line,
        [MaybeNullWhen(false)] out T tokens,
        [MaybeNullWhen(false)] out IStateStack end)
    {
        if (_lines.TryGetValue((grammar, start), out var lines)
            && lines.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(line, out var found))
        {
            (tokens, end) = found;
            return true;
        }
        tokens = null;
        end = null;
        return false;
    }

    public void Add(IGrammar grammar, IStateStack? start, ReadOnlySpan<char> line, T tokens, IStateStack end)
    {
        if (line.Length > MaxLineLength)
            return;
        if (Length + line.Length > Budget)
        {
            _lines.Clear();
            Length = 0;
        }

        if (!_lines.TryGetValue((grammar, start), out var lines))
            _lines[(grammar, start)] = lines = new Dictionary<string, (T, IStateStack)>(StringComparer.Ordinal);
        if (lines.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(line, (tokens, end)))
            Length += line.Length;
    }
}
