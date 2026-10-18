// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Parses the reasoning of a language model, between an opening tag such as <c>&lt;think&gt;</c> at the start of a
/// line and its closing tag, into a <see cref="ThinkingBlock"/> whose content is markdown. Added by
/// <see cref="ThinkingBlockExtension"/> before Markdig's HTML block parser, which would otherwise take the tags.
/// </summary>
/// <remarks>
/// <para>
/// The opening tag starts a line (indented by up to three spaces); text after it on the same line is the
/// block's first line. The closing tag either starts a line, and text after it follows the block, or ends a
/// line, after the block's last text (not on an indented line, which may be code). A closing tag inside a fenced
/// code block of the thinking block is code.
/// The block can interrupt a paragraph, since models often write the tag right after a line of text.
/// </para>
/// <para>
/// Without its closing tag the block runs to the end of its container: a model still thinking (streaming),
/// or whose output was cut off. <see cref="ThinkingBlock.HasClosingTag"/> tells the two states apart.
/// </para>
/// </remarks>
internal sealed class ThinkingBlockParser : BlockParser
{
    private readonly string[] _tagNames;

    public ThinkingBlockParser(IEnumerable<string> tagNames)
    {
        OpeningCharacters = ['<'];
        _tagNames = [.. tagNames.Where(name => name.Length > 0)];
    }

    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (processor.IsCodeIndent || MatchTag(processor.Line, closing: false) is not { } opening)
            return BlockState.None;

        var block = new ThinkingBlock(this)
        {
            TagName = opening.Name,
            Column = processor.Column,
            Line = processor.LineIndex,
            Span = new SourceSpan(processor.Start, processor.Line.End),
        };
        processor.NewBlocks.Push(block);

        for (var i = 0; i < opening.Length; i++)
            processor.NextChar();
        CloseAtLineEnd(processor, block);
        return BlockState.Continue;
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        var thinking = (ThinkingBlock)block;
        if (thinking.ClosesBeforeNextLine)
            return Break(processor, thinking);
        if (processor.IsCodeIndent || IsInCode(processor, thinking))
        {
            thinking.UpdateSpanEnd(processor.Line.End);
            return BlockState.Continue;
        }

        if (MatchTag(processor.Line, closing: true, thinking.TagName) is { } closing)
        {
            thinking.HasClosingTag = true;
            thinking.UpdateSpanEnd(processor.Start + closing.Length - 1);
            for (var i = 0; i < closing.Length; i++)
                processor.NextChar();
            // Text after the closing tag follows the block.
            return processor.Line.IsEmptyOrWhitespace() ? BlockState.BreakDiscard : Break(processor, thinking);
        }

        thinking.UpdateSpanEnd(processor.Line.End);
        CloseAtLineEnd(processor, thinking);
        return BlockState.Continue;
    }

    /// <summary>
    /// Closes <paramref name="block"/> and lets the rest of the line start what follows it. Its open paragraph,
    /// if any, is closed first: otherwise the line would continue that paragraph lazily.
    /// </summary>
    private static BlockState Break(BlockProcessor processor, ThinkingBlock block)
    {
        if (DeepestOpenBlock(processor, block) is ParagraphBlock paragraph)
            processor.Close(paragraph);
        return BlockState.Break;
    }

    private static Block DeepestOpenBlock(BlockProcessor processor, ContainerBlock container)
    {
        Block deepest = container;
        while (deepest is ContainerBlock { LastChild: { } last } && processor.IsOpen(last))
            deepest = last;
        return deepest;
    }

    /// <summary>
    /// When the rest of the line ends with the block's closing tag, removes the tag from the line, which stays
    /// in the block, and marks the block to close before the next line.
    /// </summary>
    private static void CloseAtLineEnd(BlockProcessor processor, ThinkingBlock block)
    {
        var line = processor.Line;
        var end = line.End;
        while (end >= line.Start && line.Text[end].IsWhitespace())
            end--;
        var closing = $"</{block.TagName}>";
        var start = end - closing.Length + 1;
        if (start < line.Start || string.Compare(line.Text, start, closing, 0, closing.Length, StringComparison.OrdinalIgnoreCase) != 0)
            return;

        block.HasClosingTag = true;
        block.ClosesBeforeNextLine = true;
        block.UpdateSpanEnd(end);
        processor.Line.End = start - 1;
    }

    // A closing tag inside an open fenced code block of the thinking block is code, not the end of the block.
    private static bool IsInCode(BlockProcessor processor, ThinkingBlock block) =>
        DeepestOpenBlock(processor, block) is FencedCodeBlock;

    /// <summary>
    /// The tag at the start of <paramref name="line"/>: <c>&lt;name&gt;</c>, or <c>&lt;/name&gt;</c> when
    /// <paramref name="closing"/>, for one of the parser's names (or only <paramref name="name"/>), ignoring case
    /// and with optional spaces before the <c>&gt;</c>. Returns the name as configured and the tag's length.
    /// </summary>
    private (string Name, int Length)? MatchTag(StringSlice line, bool closing, string? name = null)
    {
        var text = line.Text;
        var position = line.Start;
        if (position > line.End || text[position] != '<')
            return null;
        position++;
        if (closing)
        {
            if (position > line.End || text[position] != '/')
                return null;
            position++;
        }

        foreach (var candidate in name is null ? _tagNames : [name])
        {
            var end = position + candidate.Length;
            if (end > line.End + 1 || string.Compare(text, position, candidate, 0, candidate.Length, StringComparison.OrdinalIgnoreCase) != 0)
                continue;
            while (end <= line.End && text[end] is ' ' or '\t')
                end++;
            if (end <= line.End && text[end] == '>')
                return (candidate, end + 1 - line.Start);
        }
        return null;
    }
}
