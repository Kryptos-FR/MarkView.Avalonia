// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Markdig;
using Markdig.Extensions.Mathematics;
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
/// An unmatched backtick run is closed with a run of the same length, unless the content (the text
/// without its trailing whitespace) ends with a backtick: the closer would merge with that backtick
/// into a longer run that closes nothing, so the run is left literal.
/// A link or image whose text is closed (<c>](</c>) but whose address is not gets the closing
/// <c>)</c>, one per parenthesis still open in the address, after the closing quote of a title still
/// open; a title quote opens only after whitespace, and the address is opaque to the other rules. A
/// bracket alone (<c>[t</c>, <c>[t]</c>, <c>- [ </c>) is left literal, since it may be a reference
/// link, a footnote or a task list item. An HTML tag or autolink (<c>&lt;</c> followed by a letter,
/// <c>/</c>, <c>!</c> or <c>?</c>) is opaque up to its <c>&gt;</c>; one without it ends the scan, so
/// nothing after it is healed.
/// A run of <c>*</c> or <c>_</c>, or exactly <c>~~</c>, opens or closes emphasis (strikethrough)
/// following CommonMark's flanking rules, the start of the text and the end of the content counting
/// as whitespace: <c>*</c> and <c>~~</c> open when left-flanking and close when right-flanking;
/// <c>_</c> does not open or close inside a word (<c>snake_case</c>). A closing run consumes the
/// nearest open run of its character, as many delimiters as both have, and the delimiters opened
/// after that one become literal; what is left of it opens when it can. CommonMark's rule of
/// multiples of 3 is not applied. Other runs of <c>~</c>, and <c>^</c>, <c>+</c> and <c>=</c>, are
/// literal. A run with nothing after it is not closed: closing it would show nothing. When the
/// content's last line holds only <c>*</c>, <c>_</c> and <c>~</c> runs (and spaces, tabs or quote
/// markers), the text is returned as it is: closers appended to that line would make it a thematic break (<c>***</c>) or
/// a fence (<c>~~~~</c>).
/// With <see cref="HealingOptions.Math"/>, a run of one or two <c>$</c> and the next run of the same
/// length enclose a math span, opaque to the other rules; a run with no match, or of three or more, is
/// literal, and an unmatched one is never closed, since <c>$5</c> is a price. With
/// <see cref="HealingOptions.BracketMath"/>, <c>\(</c> and <c>\[</c> do the same up to <c>\)</c> and
/// <c>\]</c> (a backslash escapes the character after it), except that an unmatched one is closed
/// with its closer, the rest of the text being the formula; without the option they are plain escapes.
/// When the content ends with an unescaped backslash, the text is returned as it is, since any closer
/// written after it would be escaped.
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tailStart"/> is negative.</exception>
    internal static string Heal(string text, int tailStart, HealingOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tailStart);

        var end = text.Length;
        while (end > tailStart && IsWhitespace(text[end - 1]))
            end--;

        if (EndsWithDelimiterOnlyLine(text, tailStart, end))
            return text;

        var backslashes = end - RunStart(text, tailStart, end, '\\');
        if (backslashes % 2 == 1)
            return text;

        var open = new List<(char Char, int Count)>();
        var i = tailStart;
        while (i < end)
        {
            var c = text[i];
            // Before the escape case, which would skip the backslash and its bracket. A trailing lone
            // backslash was rejected above, so text[i + 1] exists; an opener with nothing after it stays
            // literal (closing it would show an empty formula).
            if (options.BracketMath && c == '\\' && text[i + 1] is '(' or '[' && i + 2 < end)
            {
                var round = text[i + 1] == '(';
                var mathEnd = BracketMathEnd(text, i + 2, end, round ? ')' : ']');
                if (mathEnd == -1)
                {
                    // The rest of the text is formula, opaque to the other rules.
                    open.Add(('\\', round ? 1 : 2));
                    break;
                }
                i = mathEnd;
            }
            else if (c == '\\')
            {
                // CommonMark lets a backslash escape only ASCII punctuation, but every character this scan
                // reacts to is ASCII punctuation, so skipping whatever follows the backslash is the same.
                i += 2;
            }
            else if (c == '`')
            {
                var runEnd = RunEnd(text, i, end, '`');
                var length = runEnd - i;
                var spanEnd = ClosingRunEnd(text, runEnd, end, '`', length);
                if (spanEnd != -1)
                {
                    i = spanEnd;
                }
                else if (text[end - 1] == '`')
                {
                    // The content ends with a backtick (this run itself when nothing follows it): a closer
                    // would merge with it into a longer run, and an opener with nothing after it would
                    // show nothing anyway, so the run is literal.
                    i = runEnd;
                }
                else
                {
                    // Everything after the opener is code span content, opaque to the other rules.
                    open.Add(('`', length));
                    break;
                }
            }
            else if (c == '$' && options.Math)
            {
                var runEnd = RunEnd(text, i, end, '$');
                var length = runEnd - i;
                // Math is opaque up to the next run of the same length; a run of three or more, or one that
                // finds no match, is literal.
                var mathEnd = length <= 2 ? ClosingRunEnd(text, runEnd, end, '$', length) : -1;
                i = mathEnd == -1 ? runEnd : mathEnd;
            }
            else if (c is '*' or '_' or '~')
            {
                var runEnd = RunEnd(text, i, end, c);
                var length = runEnd - i;
                if (c != '~' || length == 2)
                {
                    var (canOpen, canClose) = Flanking(text, i, runEnd, end, c);
                    if (canClose)
                        length = Close(open, c, length);
                    if (canOpen && length > 0)
                        open.Add((c, length));
                }
                i = runEnd;
            }
            else if (c == '<' && i + 1 < end && IsTagStart(text[i + 1]))
            {
                var tagEnd = text.IndexOf('>', i + 1, end - i - 1);
                if (tagEnd == -1)
                {
                    // A half tag is literal text until its > arrives; what follows it may be an attribute.
                    break;
                }
                i = tagEnd + 1;
            }
            else if (c == '[')
            {
                open.Add(('[', 0));
                i++;
            }
            else if (c == ']')
            {
                var bracket = open.FindLastIndex(entry => entry.Char == '[');
                if (bracket == -1)
                {
                    i++;
                }
                else if (i + 1 < end && text[i + 1] == '(')
                {
                    // Delimiters opened inside the link text never reach outside it.
                    open.RemoveRange(bracket, open.Count - bracket);
                    var addressEnd = AddressEnd(text, i + 2, end, out var depth, out var quote);
                    if (addressEnd == -1)
                    {
                        // The title quote opened after the parentheses, so it is pushed last and closed first.
                        open.Add((')', depth));
                        if (quote != '\0')
                            open.Add((quote, 1));
                        break;
                    }
                    i = addressEnd;
                }
                else
                {
                    // A link text without an address (a reference link, maybe): only the bracket is closed.
                    open.RemoveAt(bracket);
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        var closers = new StringBuilder();
        WriteClosers(closers, open);
        return closers.Length == 0 ? text : text.Insert(end, closers.ToString());
    }

    /// <summary>
    /// The full name of the extension, declared in the <c>MarkView.Avalonia.Math</c> package, that turns
    /// <c>\(…\)</c> and <c>\[…\]</c> into math. The core package does not reference that package, so
    /// the extension is recognised by its type name; the Math package's tests pin the name.
    /// </summary>
    private const string BracketMathExtensionName = "MarkView.Avalonia.Math.Parsing.BracketMathExtension";

    /// <summary>
    /// The optional syntaxes <paramref name="pipeline"/> enables that <see cref="Heal"/> recognises.
    /// </summary>
    /// <param name="pipeline">The pipeline the healed text is parsed with.</param>
    /// <returns>
    /// <see cref="HealingOptions.Math"/> when the pipeline has Markdig's math extension, and
    /// <see cref="HealingOptions.BracketMath"/> when it has the Math package's bracket math extension.
    /// </returns>
    internal static HealingOptions OptionsFor(MarkdownPipeline pipeline) =>
        new(
            pipeline.Extensions.Contains<MathExtension>(),
            pipeline.Extensions.Any(extension => extension.GetType().FullName == BracketMathExtensionName));

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
    /// An ATX heading written with closing hashes (<c># **a #</c>) is never the tail while its line is
    /// being typed, because Markdig's heading span ends before the closing hashes; healing it would
    /// append the closers after them and show the hashes as text.
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
            if (!IsWhitespace(c))
                return null;
            // Markdig ends a line at "\n", "\r\n" or a lone "\r".
            var lineBreak = c == '\n' || (c == '\r' && (i + 1 == text.Length || text[i + 1] != '\n'));
            if (lineBreak && ++lineBreaks > 1)
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
    /// Appends the closers of the <paramref name="open"/> delimiters, the last opened first: each
    /// entry's character, as many times as its count. A link text entry, <c>('[', 0)</c>, so writes
    /// nothing. The entries of unclosed bracket math use the backslash as their character:
    /// <c>('\\', 1)</c> for <c>\(</c>, closed by <c>\)</c>, and <c>('\\', 2)</c> for <c>\[</c>, closed by
    /// <c>\]</c>; they cannot reuse <c>(</c> or <c>[</c>, which stand for an address and a link text.
    /// </summary>
    private static void WriteClosers(StringBuilder closers, List<(char Char, int Count)> open)
    {
        for (var i = open.Count - 1; i >= 0; i--)
        {
            var (c, count) = open[i];
            if (c == '\\')
                closers.Append('\\').Append(count == 1 ? ')' : ']');
            else
                closers.Append(c, count);
        }
    }

    /// <summary>
    /// Whether the last line of the content, from the character after the last line break before
    /// <paramref name="end"/> (or from <paramref name="start"/> when there is none), is not empty and holds
    /// only <c>*</c>, <c>_</c>, <c>~</c>, spaces, tabs and quote markers (<c>&gt;</c>).
    /// </summary>
    private static bool EndsWithDelimiterOnlyLine(string text, int start, int end)
    {
        var lineStart = end;
        while (lineStart > start && text[lineStart - 1] is '*' or '_' or '~' or ' ' or '\t' or '>')
            lineStart--;
        return lineStart < end && (lineStart == start || text[lineStart - 1] is '\n' or '\r');
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
    /// The index of the first character of the run of <paramref name="c"/> that ends just before
    /// <paramref name="end"/>, not looking before <paramref name="start"/>.
    /// </summary>
    private static int RunStart(string text, int start, int end, char c)
    {
        var i = end;
        while (i > start && text[i - 1] == c)
            i--;
        return i;
    }

    /// <summary>
    /// Whether the delimiter run of <paramref name="c"/> between <paramref name="start"/> and
    /// <paramref name="runEnd"/> can open and can close emphasis, following CommonMark's definitions of
    /// left- and right-flanking runs. The start of the text and <paramref name="end"/>, the end of the
    /// content, count as whitespace; the character before <paramref name="start"/> is read even when it
    /// lies before the scanned tail.
    /// </summary>
    /// <remarks>
    /// <c>*</c> and <c>~</c> open when left-flanking and close when right-flanking. <c>_</c> opens when
    /// left-flanking and either not right-flanking or preceded by punctuation, and closes when
    /// right-flanking and either not left-flanking or followed by punctuation, so it does neither
    /// inside a word.
    /// </remarks>
    private static (bool CanOpen, bool CanClose) Flanking(string text, int start, int runEnd, int end, char c)
    {
        var before = start > 0 ? text[start - 1] : ' ';
        var after = runEnd < end ? text[runEnd] : ' ';
        var spaceBefore = char.IsWhiteSpace(before);
        var spaceAfter = char.IsWhiteSpace(after);
        var punctuationBefore = IsPunctuation(before);
        var punctuationAfter = IsPunctuation(after);

        var left = !spaceAfter && (!punctuationAfter || spaceBefore || punctuationBefore);
        var right = !spaceBefore && (!punctuationBefore || spaceAfter || punctuationAfter);
        return c == '_'
            ? (left && (!right || punctuationBefore), right && (!left || punctuationAfter))
            : (left, right);
    }

    /// <summary>
    /// Closes the runs of <paramref name="c"/> still <paramref name="open"/> with a closing run of
    /// <paramref name="count"/> delimiters. The nearest entry of <paramref name="c"/>, from the last
    /// opened down, gives up as many delimiters as both have; the entries opened after it are removed,
    /// since they become literal, and so is the entry itself once it has none left. Delimiters left over
    /// look further down. When no entry of <paramref name="c"/> is open, nothing is removed.
    /// </summary>
    /// <returns>The number of delimiters of the closing run left over.</returns>
    private static int Close(List<(char Char, int Count)> open, char c, int count)
    {
        var k = open.FindLastIndex(entry => entry.Char == c);
        while (k != -1 && count > 0)
        {
            var used = Math.Min(open[k].Count, count);
            count -= used;
            var remaining = open[k].Count - used;
            open.RemoveRange(k + 1, open.Count - k - 1);
            if (remaining == 0)
                open.RemoveAt(k);
            else
                open[k] = (c, remaining);
            k = open.FindLastIndex(entry => entry.Char == c);
        }
        return count;
    }

    /// <summary>
    /// Whether <paramref name="c"/> is punctuation in CommonMark's sense: a Unicode punctuation or
    /// symbol character, which every ASCII punctuation character is.
    /// </summary>
    private static bool IsPunctuation(char c) => char.IsPunctuation(c) || char.IsSymbol(c);

    /// <summary>
    /// The index just after the first run of exactly <paramref name="length"/> characters
    /// <paramref name="c"/> between <paramref name="start"/> and <paramref name="end"/>, which closes the
    /// code span or math span opened just before <paramref name="start"/>; -1 when there is none.
    /// </summary>
    private static int ClosingRunEnd(string text, int start, int end, char c, int length)
    {
        var i = start;
        while (i < end)
        {
            if (text[i] != c)
            {
                i++;
                continue;
            }

            var runEnd = RunEnd(text, i, end, c);
            if (runEnd - i == length)
                return runEnd;
            i = runEnd;
        }
        return -1;
    }

    /// <summary>
    /// The index just after the first <c>\</c> followed by <paramref name="closer"/> (<c>)</c> or
    /// <c>]</c>) between <paramref name="start"/> and <paramref name="end"/>, which closes the bracket
    /// math opened just before <paramref name="start"/>; -1 when there is none. A backslash escapes the
    /// character after it, so in <c>\\)</c> the parenthesis is not part of a closer.
    /// </summary>
    private static int BracketMathEnd(string text, int start, int end, char closer)
    {
        var i = start;
        while (i + 1 < end)
        {
            if (text[i] != '\\')
                i++;
            else if (text[i + 1] == closer)
                return i + 2;
            else
                i += 2;
        }
        return -1;
    }

    /// <summary>
    /// Scans the address of a link or image, starting at <paramref name="start"/> just after its
    /// <c>](</c>: a destination, in which parentheses nest, then an optional title opened by a
    /// <c>"</c> or <c>'</c> after whitespace and closed by the same quote, in which parentheses are
    /// plain. A backslash escapes the character after it.
    /// </summary>
    /// <param name="text">The text being healed.</param>
    /// <param name="start">The index just after the address's opening parenthesis.</param>
    /// <param name="end">The end of the content; the scan does not look at or past it.</param>
    /// <param name="depth">
    /// When the content ends inside the address, the number of parentheses still open, the address's
    /// own included.
    /// </param>
    /// <param name="quote">
    /// When the content ends inside the address, the quote of the title still open, or <c>'\0'</c>.
    /// </param>
    /// <returns>
    /// The index just after the parenthesis that closes the address; -1 when the content ends first.
    /// </returns>
    private static int AddressEnd(string text, int start, int end, out int depth, out char quote)
    {
        depth = 1;
        quote = '\0';
        var i = start;
        while (i < end)
        {
            var c = text[i];
            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
            }
            else if ((c is '"' or '\'') && IsWhitespace(text[i - 1]))
            {
                quote = c;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && --depth == 0)
            {
                return i + 1;
            }
            i++;
        }
        return -1;
    }

    /// <summary>
    /// Whether <paramref name="c"/>, following a <c>&lt;</c>, starts an HTML tag, comment, declaration,
    /// processing instruction or autolink: an ASCII letter, <c>/</c>, <c>!</c> or <c>?</c>.
    /// </summary>
    private static bool IsTagStart(char c) => char.IsAsciiLetter(c) || c is '/' or '!' or '?';

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
