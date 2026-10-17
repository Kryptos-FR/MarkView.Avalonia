// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Groups the blocks between an HTML <c>&lt;details&gt;</c> tag and its matching <c>&lt;/details&gt;</c> into a
/// <see cref="DetailsBlock"/>, once Markdig has parsed the document.
/// </summary>
/// <remarks>
/// <para>
/// Markdig parses the tags as <see cref="HtmlBlock"/>s, with the markdown between them as ordinary blocks
/// when blank lines separate them, or as a single HTML block when everything is on consecutive lines. The
/// tags are matched among the children of each container block, like elements: a <c>&lt;/details&gt;</c>
/// closes the innermost open <c>&lt;details&gt;</c>, so they nest. A tag without a match is left as it was.
/// </para>
/// <para>
/// The first <c>&lt;summary&gt;…&lt;/summary&gt;</c> of a details element, written within one HTML block,
/// gives its <see cref="DetailsBlock.Summary"/>. Other text of an HTML block holding one of these tags is
/// HTML, not markdown: inside a details element, it becomes a plain-text paragraph (tags removed, entities
/// decoded, whitespace collapsed, <c>&lt;br&gt;</c> as a line break); outside, it stays an HTML block.
/// HTML blocks without any of these tags are left untouched, wherever they are.
/// </para>
/// </remarks>
internal static partial class HtmlDetailsGrouper
{
    private enum TokenKind
    {
        /// <summary>A block whose content is not searched for tags: not HTML, or a comment or <c>&lt;pre&gt;</c> HTML block.</summary>
        Block,
        Open,
        Close,
        Summary,
        Text,
    }

    /// <param name="Raw">The source text of a tag or text fragment.</param>
    /// <param name="Value">The inner HTML of a summary.</param>
    /// <param name="Expanded">Whether an opening tag has the <c>open</c> attribute.</param>
    private readonly record struct Token(TokenKind Kind, string Raw = "", string Value = "", bool Expanded = false);

    [GeneratedRegex(@"<!--.*?-->|<details(?<attributes>\s[^>]*)?>|</details\s*>|<summary(?:\s[^>]*)?>(?<summary>.*?)</summary\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    // An attribute with its optional value; quoted values are consumed whole, so a name inside one is not matched.
    [GeneratedRegex(@"(?<name>[^\s""'>/=]+)(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'=<>`]+))?")]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LineBreakRegex();

    [GeneratedRegex(@"<!--.*?-->|</?[a-zA-Z][^>]*>", RegexOptions.Singleline)]
    private static partial Regex MarkupRegex();

    [GeneratedRegex(@"[ \t\r\n\f]+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>Groups the details elements of <paramref name="document"/>, at any depth.</summary>
    public static void Process(MarkdownDocument document) => Process((ContainerBlock)document);

    private static void Process(ContainerBlock container)
    {
        Group(container);
        foreach (var child in container)
        {
            if (child is ContainerBlock inner)
                Process(inner);
        }
    }

    private static void Group(ContainerBlock container)
    {
        if (!HasHtmlBlock(container))
            return;

        var children = container.ToArray();
        var tokens = new List<Token>();
        var ranges = new (int Start, int End)[children.Length];
        for (var i = 0; i < children.Length; i++)
        {
            var start = tokens.Count;
            if (children[i] is HtmlBlock { Type: HtmlBlockType.InterruptingBlock or HtmlBlockType.NonInterruptingBlock } html)
                Tokenize(html.Lines.ToString(), tokens);
            else
                tokens.Add(new Token(TokenKind.Block));
            ranges[i] = (start, tokens.Count);
        }

        if (Match(tokens) is not { } matched)
            return;

        container.Clear();
        var targets = new Stack<ContainerBlock>();
        targets.Push(container);
        for (var i = 0; i < children.Length; i++)
        {
            var (start, end) = ranges[i];
            if (!IsSplit(tokens, matched, start, end, insideDetails: targets.Count > 1))
            {
                targets.Peek().Add(children[i]);
                continue;
            }

            var html = (HtmlBlock)children[i];
            var pending = new StringBuilder();
            for (var t = start; t < end; t++)
            {
                var token = tokens[t];
                switch (token.Kind)
                {
                    case TokenKind.Open when matched[t]:
                        Flush(targets.Peek(), html, pending);
                        var details = new DetailsBlock
                        {
                            StartsExpanded = token.Expanded,
                            Span = html.Span,
                            Line = html.Line,
                            Column = html.Column,
                        };
                        targets.Peek().Add(details);
                        targets.Push(details);
                        break;
                    case TokenKind.Close when matched[t]:
                        Flush(targets.Peek(), html, pending);
                        var closed = targets.Pop();
                        closed.Span = new SourceSpan(closed.Span.Start, html.Span.End);
                        break;
                    case TokenKind.Summary when targets.Peek() is DetailsBlock { Summary: null } current:
                        Flush(current, html, pending);
                        current.Summary = ToPlainText(token.Value);
                        break;
                    default:
                        pending.Append(token.Raw);
                        break;
                }
            }
            Flush(targets.Peek(), html, pending);
        }
    }

    // Most containers hold no HTML at all: they are left without allocating anything.
    private static bool HasHtmlBlock(ContainerBlock container)
    {
        foreach (var child in container)
        {
            if (child is HtmlBlock)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Appends the details and summary tags in <paramref name="html"/>, and the text around them, to
    /// <paramref name="tokens"/>.
    /// </summary>
    private static void Tokenize(string html, List<Token> tokens)
    {
        var position = 0;
        foreach (Match match in TagRegex().Matches(html))
        {
            // Stryker disable once Equality : an empty text token adds nothing to the text it is gathered into
            if (match.Index > position)
                tokens.Add(new Token(TokenKind.Text, html[position..match.Index]));
            position = match.Index + match.Length;

            var raw = match.Value;
            if (raw.StartsWith("<!--", StringComparison.Ordinal))
            {
                tokens.Add(new Token(TokenKind.Text, raw));
                continue;
            }
            if (raw[1] == '/')
                tokens.Add(new Token(TokenKind.Close, raw));
            else if (match.Groups["summary"].Success)
                tokens.Add(new Token(TokenKind.Summary, raw, match.Groups["summary"].Value));
            else
                tokens.Add(new Token(TokenKind.Open, raw, Expanded: HasOpenAttribute(match.Groups["attributes"].Value)));
        }

        // Stryker disable once Equality : an empty text token adds nothing to the text it is gathered into
        if (position < html.Length)
            tokens.Add(new Token(TokenKind.Text, html[position..]));
    }

    private static bool HasOpenAttribute(string attributes)
    {
        foreach (Match attribute in AttributeRegex().Matches(attributes))
        {
            if (attribute.Groups["name"].Value.Equals("open", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Pairs each closing tag with the innermost opening tag before it that is still open. Returns which
    /// tokens are paired, or <c>null</c> when none is.
    /// </summary>
    private static bool[]? Match(List<Token> tokens)
    {
        bool[]? matched = null;
        var open = new Stack<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == TokenKind.Open)
            {
                open.Push(i);
            }
            else if (tokens[i].Kind == TokenKind.Close && open.TryPop(out var opening))
            {
                matched ??= new bool[tokens.Count];
                matched[opening] = true;
                matched[i] = true;
            }
        }
        return matched;
    }

    /// <summary>
    /// Whether the HTML block whose tokens are <paramref name="start"/> to <paramref name="end"/> must be
    /// split: it opens or closes a details element, or gives the summary of the one it is in. Otherwise
    /// (no tag, or only unmatched ones) it is kept as it is.
    /// </summary>
    private static bool IsSplit(List<Token> tokens, bool[] matched, int start, int end, bool insideDetails)
    {
        for (var t = start; t < end; t++)
        {
            if (matched[t] || (insideDetails && tokens[t].Kind == TokenKind.Summary))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Adds the text gathered in <paramref name="pending"/> to <paramref name="target"/> and clears it: as a
    /// plain-text paragraph inside a details element, or as an HTML block of its own outside.
    /// </summary>
    private static void Flush(ContainerBlock target, HtmlBlock html, StringBuilder pending)
    {
        var text = pending.ToString();
        pending.Clear();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (target is DetailsBlock)
        {
            if (ToParagraph(text) is { } paragraph)
            {
                paragraph.Span = html.Span;
                paragraph.Line = html.Line;
                paragraph.Column = html.Column;
                target.Add(paragraph);
            }
            return;
        }

        target.Add(new HtmlBlock(null)
        {
            Type = html.Type,
            Lines = new StringLineGroup(text.Trim('\r', '\n')),
            Span = html.Span,
            Line = html.Line,
            Column = html.Column,
        });
    }

    /// <summary>
    /// The text <paramref name="html"/> shows, as a paragraph with a hard line break for each <c>&lt;br&gt;</c>,
    /// or <c>null</c> when it shows none.
    /// </summary>
    private static ParagraphBlock? ToParagraph(string html)
    {
        var lines = LineBreakRegex().Split(html).Select(ToPlainText).ToList();
        var first = lines.FindIndex(line => line.Length > 0);
        if (first < 0)
            return null;
        var last = lines.FindLastIndex(line => line.Length > 0);

        var inline = new ContainerInline();
        for (var i = first; i <= last; i++)
        {
            if (i > first)
                inline.AppendChild(new LineBreakInline { IsHard = true });
            if (lines[i].Length > 0)
                inline.AppendChild(new LiteralInline(lines[i]));
        }
        return new ParagraphBlock { Inline = inline };
    }

    /// <summary>
    /// The text <paramref name="html"/> shows: without its tags and comments, with runs of whitespace
    /// collapsed to a space as HTML does, trimmed, and with its character references decoded.
    /// </summary>
    private static string ToPlainText(string html)
    {
        var text = WhitespaceRegex().Replace(MarkupRegex().Replace(html, string.Empty), " ").Trim(' ');
        return WebUtility.HtmlDecode(text);
    }
}
