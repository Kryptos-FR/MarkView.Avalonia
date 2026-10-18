// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Closes the inline syntax left open at the end of a streamed text (an unmatched code span opener,
/// for instance) so the text parses as it will once complete.
/// </summary>
/// <remarks>
/// <see cref="Heal"/> only ever adds closers: it never removes or rewrites a character of the text.
/// The closers are inserted before the text's trailing whitespace, since a closer on a line of its own
/// would follow whitespace and could not close anything. Nothing before the tail block's start is
/// scanned, apart from the character just before it, which later rules read to decide whether a
/// delimiter run is flanking. When there is nothing to close, the input string instance itself is
/// returned, so a caller can tell with a reference comparison that the text needs no second parse.
/// </remarks>
internal static class MarkdownHealer
{
    /// <summary>
    /// Returns <paramref name="text"/> with the closers of the inline syntax still open at its end
    /// inserted before its trailing whitespace, scanning from <paramref name="tailStart"/>, the start of
    /// the block that contains the end of the text (see <see cref="TailBlock"/>).
    /// </summary>
    /// <param name="text">The markdown text streamed so far.</param>
    /// <param name="tailStart">The index the scan starts at; the syntax before it is left as it is.</param>
    /// <param name="options">The optional syntaxes the scan recognises.</param>
    /// <returns>The healed text, or <paramref name="text"/> itself when nothing is left open.</returns>
    internal static string Heal(string text, int tailStart, HealingOptions options)
    {
        var end = text.Length;
        while (end > tailStart && IsWhitespace(text[end - 1]))
            end--;

        var open = new List<(char Char, int Count)>();
        var i = tailStart;
        while (i < end)
        {
            var c = text[i];
            if (c == '\\')
            {
                // CommonMark lets a backslash escape only ASCII punctuation, but every character this scan
                // reacts to is ASCII punctuation, so skipping whatever follows the backslash is the same.
                i += 2;
            }
            else if (c == '`')
            {
                var runEnd = RunEnd(text, i, end, '`');
                var length = runEnd - i;
                var spanEnd = CodeSpanEnd(text, runEnd, end, length);
                if (spanEnd != -1)
                {
                    i = spanEnd;
                }
                else if (runEnd == end)
                {
                    // An opener with nothing after it: closing it would add nothing visible.
                    i = runEnd;
                }
                else
                {
                    // Everything after the opener is code span content, opaque to the other rules.
                    open.Add(('`', length));
                    break;
                }
            }
            else
            {
                i++;
            }
        }

        if (open.Count == 0)
            return text;

        var closers = new StringBuilder();
        WriteClosers(closers, open);
        return text.Insert(end, closers.ToString());
    }

    /// <summary>
    /// Returns the block that contains the end of <paramref name="text"/>: the deepest last descendant
    /// (following <see cref="ContainerBlock.LastChild"/>) of the top-level block that ends last, when
    /// nothing but whitespace holding at most one line break follows that descendant.
    /// </summary>
    /// <param name="document">The document parsed from <paramref name="text"/>.</param>
    /// <param name="text">The text <paramref name="document"/> was parsed from.</param>
    /// <returns>
    /// The tail block, or <see langword="null"/> when the document is empty or a blank line follows its
    /// last block (the next text starts a new block).
    /// </returns>
    /// <remarks>
    /// The top-level block is chosen by <see cref="SourceSpan.End"/>, a later index winning a tie, not by
    /// index: Markdig moves the footnote group to the end of the document and keeps the link reference
    /// definition group where its first definition was, so index order is not text order.
    /// <see cref="Block.IsOpen"/> is not used either, since Markdig closes an ATX heading at once.
    /// </remarks>
    internal static Block? TailBlock(MarkdownDocument document, string text)
    {
        Block? last = null;
        foreach (var block in document)
        {
            if (last is null || block.Span.End >= last.Span.End)
                last = block;
        }

        if (last is null)
            return null;

        while (last is ContainerBlock { LastChild: { } child })
            last = child;

        var lineBreaks = 0;
        for (var i = EndOf(last) + 1; i < text.Length; i++)
        {
            var c = text[i];
            if (!IsWhitespace(c) || (c == '\n' && ++lineBreaks > 1))
                return null;
        }
        return last;
    }

    /// <summary>
    /// The index of the last character of <paramref name="block"/>. Markdig leaves the span of an open
    /// fenced code block on its opening fence line, so the end of a leaf block's last line counts too.
    /// </summary>
    private static int EndOf(Block block) =>
        block is LeafBlock { Lines.Count: > 0 } leaf
            ? Math.Max(block.Span.End, leaf.Lines.Lines[leaf.Lines.Count - 1].Slice.End)
            : block.Span.End;

    /// <summary>
    /// Appends the closers of the <paramref name="open"/> delimiters, the last opened first.
    /// </summary>
    private static void WriteClosers(StringBuilder closers, List<(char Char, int Count)> open)
    {
        for (var i = open.Count - 1; i >= 0; i--)
        {
            var (c, count) = open[i];
            switch (c)
            {
                case '`':
                    closers.Append('`', count);
                    break;
            }
        }
    }

    /// <summary>
    /// The index just after the run of <paramref name="c"/> that starts at <paramref name="start"/>,
    /// not looking at or past <paramref name="end"/>.
    /// </summary>
    private static int RunEnd(string text, int start, int end, char c)
    {
        var i = start;
        while (i < end && text[i] == c)
            i++;
        return i;
    }

    /// <summary>
    /// The index just after the first run of exactly <paramref name="length"/> backticks between
    /// <paramref name="start"/> and <paramref name="end"/>, which closes the code span opened just
    /// before <paramref name="start"/>; -1 when there is none.
    /// </summary>
    private static int CodeSpanEnd(string text, int start, int end, int length)
    {
        var i = start;
        while (i < end)
        {
            if (text[i] != '`')
            {
                i++;
                continue;
            }

            var runEnd = RunEnd(text, i, end, '`');
            if (runEnd - i == length)
                return runEnd;
            i = runEnd;
        }
        return -1;
    }

    /// <summary>Whether <paramref name="c"/> is a space, a tab or a line break character.</summary>
    private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n';
}

/// <summary>
/// The optional syntaxes <see cref="MarkdownHealer.Heal"/> recognises, following the extensions the
/// viewer's pipeline enables.
/// </summary>
/// <param name="Math">Whether <c>$…$</c> and <c>$$…$$</c> are math.</param>
/// <param name="BracketMath">Whether <c>\(…\)</c> and <c>\[…\]</c> are math.</param>
internal readonly record struct HealingOptions(bool Math, bool BracketMath);
