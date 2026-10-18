// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Turns the HTML of a parsed document into the blocks and inlines of the HTML subset (see
/// <see cref="MarkdownExtensions.UseHtmlSubset"/>), once Markdig has parsed it.
/// </summary>
/// <remarks>
/// <para>
/// Inline HTML, in paragraphs, headings, table cells and the like, is built by <see cref="HtmlInlineBuilder"/>.
/// </para>
/// <para>
/// Markdown is not parsed inside an HTML block, so the text of each one is split into tags and text here: a
/// <c>&lt;p&gt;</c> or <c>&lt;h1&gt;</c>…<c>&lt;h6&gt;</c> element becomes a paragraph or heading of its inline
/// content, an <c>&lt;hr&gt;</c> a thematic break, and inline content outside them a paragraph of its own. Such a
/// paragraph or heading ends at its end tag, at the start of another block element, or at the end of the HTML
/// block. Any other tag is left to <see cref="HtmlInlineBuilder"/>, which drops it and keeps its text.
/// </para>
/// <para>
/// <c>&lt;div&gt;</c> tags are matched among the children of each container block, like elements, so a
/// <c>&lt;div align="center"&gt;</c> separated by blank lines from the markdown inside it, and from its
/// <c>&lt;/div&gt;</c>, centers that markdown. An <c>align</c> attribute on a matched <c>&lt;div&gt;</c>, a
/// <c>&lt;p&gt;</c> or a heading puts its blocks in an <see cref="HtmlAlignedBlock"/>; a <c>&lt;div&gt;</c>
/// without a match is dropped. Comments, and the HTML blocks Markdig starts with <c>&lt;script&gt;</c>,
/// <c>&lt;pre&gt;</c>, <c>&lt;style&gt;</c>, <c>&lt;textarea&gt;</c>, a comment or a declaration, are left as they
/// are and not rendered.
/// </para>
/// </remarks>
internal static class HtmlSubsetGrouper
{
    private enum TokenKind
    {
        /// <summary>A block other than an HTML block of the subset.</summary>
        Block,

        /// <summary>Inline content of an HTML block: text or a tag.</summary>
        Inline,
        DivStart,
        DivEnd,

        /// <summary>The start tag of a paragraph or heading.</summary>
        LeafStart,

        /// <summary>The end tag of a paragraph or heading.</summary>
        LeafEnd,
        Rule,

        /// <summary>The end of an HTML block.</summary>
        End,
    }

    /// <param name="Block">The block of a <see cref="TokenKind.Block"/> token, or the HTML block another token is from.</param>
    /// <param name="Html">The tag or text of an HTML token.</param>
    private readonly record struct Token(TokenKind Kind, Block Block, HtmlToken Html = default);

    /// <param name="Target">The block the blocks of the element are added to.</param>
    /// <param name="Aligned">The aligned block the element created, if any.</param>
    private readonly record struct Frame(ContainerBlock Target, HtmlAlignedBlock? Aligned);

    /// <summary>Builds the HTML of <paramref name="document"/>, at any depth.</summary>
    public static void Process(MarkdownDocument document)
    {
        // Inlines first: the blocks built from HTML blocks get their inlines built as they are created.
        ProcessInlines(document);
        ProcessBlocks(document);
    }

    private static void ProcessInlines(ContainerBlock container)
    {
        if (container is DetailsBlock { SummaryHtml: { } summary } details)
            details.SummaryInline = HtmlInlineBuilder.Build(summary);

        foreach (var block in container)
        {
            if (block is LeafBlock { Inline: { } inline })
                HtmlInlineBuilder.Process(inline);
            else if (block is ContainerBlock inner)
                ProcessInlines(inner);
        }
    }

    private static void ProcessBlocks(ContainerBlock container)
    {
        Group(container);
        foreach (var child in container)
        {
            if (child is ContainerBlock inner)
                ProcessBlocks(inner);
        }
    }

    private static bool IsSubsetHtml(Block block) =>
        block is HtmlBlock { Type: HtmlBlockType.InterruptingBlock or HtmlBlockType.NonInterruptingBlock };

    private static void Group(ContainerBlock container)
    {
        // Most containers hold no HTML at all: they are left without allocating anything.
        if (!container.Any(IsSubsetHtml))
            return;

        var tokens = new List<Token>();
        foreach (var child in container)
        {
            if (IsSubsetHtml(child))
                Tokenize((HtmlBlock)child, tokens);
            else
                tokens.Add(new Token(TokenKind.Block, child));
        }
        var matched = MatchDivs(tokens);

        container.Clear();
        var frames = new Stack<Frame>();
        frames.Push(new Frame(container, null));
        var pending = new List<HtmlToken>();
        HtmlTag? leaf = null;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == TokenKind.Inline)
            {
                pending.Add(token.Html);
                continue;
            }

            // Any other token ends the paragraph or heading being gathered, but the end tag of another element.
            if (token.Kind != TokenKind.LeafEnd || token.Html.Tag!.Name == leaf?.Name)
            {
                Flush(frames.Peek().Target, token.Block, leaf, pending);
                leaf = null;
            }

            switch (token.Kind)
            {
                case TokenKind.Block:
                    frames.Peek().Target.Add(token.Block);
                    break;
                case TokenKind.DivStart when matched[i]:
                    var aligned = Alignment(token.Html.Tag!) is { } alignment
                        ? Add(frames.Peek().Target, new HtmlAlignedBlock(alignment), token.Block)
                        : null;
                    frames.Push(new Frame(aligned ?? frames.Peek().Target, aligned));
                    break;
                case TokenKind.DivEnd when matched[i]:
                    if (frames.Pop().Aligned is { } closed)
                        closed.Span = new SourceSpan(closed.Span.Start, token.Block.Span.End);
                    break;
                case TokenKind.LeafStart:
                    leaf = token.Html.Tag;
                    break;
                case TokenKind.Rule:
                    Add(frames.Peek().Target, new ThematicBreakBlock(null!), token.Block);
                    break;
            }
        }
    }

    /// <summary>Appends the tags and text of <paramref name="html"/> to <paramref name="tokens"/>.</summary>
    private static void Tokenize(HtmlBlock html, List<Token> tokens)
    {
        foreach (var token in HtmlToken.Tokenize(html.Lines.ToString()))
        {
            var kind = token.Tag switch
            {
                { Name: "div", IsEnd: false } => TokenKind.DivStart,
                { Name: "div" } => TokenKind.DivEnd,
                { IsEnd: false } tag when IsLeaf(tag) => TokenKind.LeafStart,
                { } tag when IsLeaf(tag) => TokenKind.LeafEnd,
                { Name: "hr", IsEnd: false } => TokenKind.Rule,
                _ => TokenKind.Inline,
            };
            tokens.Add(new Token(kind, html, token));
        }
        tokens.Add(new Token(TokenKind.End, html));
    }

    private static bool IsLeaf(HtmlTag tag) => tag.Name == "p" || HeadingLevel(tag) > 0;

    /// <summary>The level of an <c>&lt;h1&gt;</c>…<c>&lt;h6&gt;</c> tag, or 0 for another tag.</summary>
    private static int HeadingLevel(HtmlTag tag) =>
        tag.Name is ['h', >= '1' and <= '6' and var digit] ? digit - '0' : 0;

    private static HtmlAlignment? Alignment(HtmlTag tag) => tag.Attribute("align")?.Trim().ToLowerInvariant() switch
    {
        "left" => HtmlAlignment.Left,
        "center" => HtmlAlignment.Center,
        "right" => HtmlAlignment.Right,
        "justify" => HtmlAlignment.Justify,
        _ => null,
    };

    /// <summary>
    /// Pairs each <c>&lt;/div&gt;</c> with the innermost <c>&lt;div&gt;</c> before it that is still open. Returns
    /// which tokens are paired.
    /// </summary>
    private static bool[] MatchDivs(List<Token> tokens)
    {
        var matched = new bool[tokens.Count];
        var open = new Stack<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == TokenKind.DivStart)
            {
                open.Push(i);
            }
            else if (tokens[i].Kind == TokenKind.DivEnd && open.TryPop(out var opening))
            {
                matched[opening] = true;
                matched[i] = true;
            }
        }
        return matched;
    }

    /// <summary>
    /// Adds the paragraph, or the heading <paramref name="leaf"/> starts, of the inline content gathered in
    /// <paramref name="pending"/> to <paramref name="target"/>, in an aligned block when <paramref name="leaf"/> has
    /// an <c>align</c> attribute, and clears it. Nothing is added when the content shows nothing.
    /// </summary>
    private static void Flush(ContainerBlock target, Block source, HtmlTag? leaf, List<HtmlToken> pending)
    {
        var inline = HtmlInlineBuilder.Build(pending);
        pending.Clear();
        if (inline is null)
            return;

        LeafBlock block = leaf is not null && HeadingLevel(leaf) is > 0 and var level
            ? new HeadingBlock(null!) { Level = level, HeaderChar = '#' }
            : new ParagraphBlock();
        block.Inline = inline;

        if (leaf is not null && Alignment(leaf) is { } alignment)
            target = Add(target, new HtmlAlignedBlock(alignment), source);
        Add(target, block, source);
    }

    /// <summary>Adds <paramref name="block"/> to <paramref name="target"/> at the position of <paramref name="source"/>.</summary>
    private static T Add<T>(ContainerBlock target, T block, Block source) where T : Block
    {
        block.Span = source.Span;
        block.Line = source.Line;
        block.Column = source.Column;
        target.Add(block);
        return block;
    }
}
