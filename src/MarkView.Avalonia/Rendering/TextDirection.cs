// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;
using Avalonia.Media.TextFormatting.Unicode;

using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Parsing;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Finds the direction of a block's text from its first strong character, as HTML's <c>dir="auto"</c> does
/// (rules P2 and P3 of the Unicode Bidirectional Algorithm): a Hebrew or Arabic letter makes it right to
/// left, a Latin, Cyrillic or CJK letter left to right, and digits, punctuation and symbols decide nothing.
/// </summary>
/// <remarks>
/// Unlike a browser, the text of code spans, math, autolinks, raw HTML tags and images is skipped: it is
/// technical text that rarely says which way the prose around it reads, so a Hebrew paragraph that opens
/// with <c>`npm install`</c> stays right to left.
/// </remarks>
internal static class TextDirection
{
    /// <summary>
    /// The key of the direction an HTML <c>dir</c> attribute gives a block, kept in the block's data
    /// (<see cref="MarkdownObject.SetData"/>) as a <see cref="FlowDirection"/>.
    /// </summary>
    public static readonly object ExplicitKey = typeof(TextDirection);

    /// <summary>The direction an HTML <c>dir</c> attribute gives <paramref name="block"/>, or <c>null</c>.</summary>
    public static FlowDirection? Explicit(Block block) => block.GetData(ExplicitKey) as FlowDirection?;

    /// <summary>
    /// The direction of the first strong character of <paramref name="block"/>'s text, or <c>null</c> when it
    /// has none. A container block reads its blocks in order, skipping code blocks and the blocks an HTML
    /// <c>dir</c> attribute sets, as HTML skips the elements that have their own <c>dir</c>.
    /// </summary>
    public static FlowDirection? Detect(Block block)
    {
        switch (block)
        {
            case CodeBlock:
                return null;
            case LeafBlock leaf:
                return Detect(leaf.Inline);
            case DetailsBlock details when (details.SummaryInline is { } summary ? Detect(summary) : Detect(details.Summary)) is { } direction:
                return direction;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    if (Explicit(child) is null && Detect(child) is { } childDirection)
                        return childDirection;
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>The direction of the first strong character of the text of <paramref name="inline"/> and its siblings.</summary>
    public static FlowDirection? Detect(Inline? inline)
    {
        for (; inline is not null; inline = inline.NextSibling)
        {
            var direction = inline switch
            {
                LiteralInline literal => Detect(literal.Content.AsSpan()),
                HtmlEntityInline entity => Detect(entity.Transcoded.AsSpan()),
                LinkInline { IsImage: true } => null,
                ContainerInline container => Detect(container.FirstChild),
                // Code spans, math, autolinks, raw HTML tags, task markers…
                _ => null,
            };
            if (direction is not null)
                return direction;
        }
        return null;
    }

    /// <summary>
    /// The direction of the first strong character of <paramref name="text"/> outside directional isolates
    /// (U+2066…U+2069), or <c>null</c> when it has none.
    /// </summary>
    public static FlowDirection? Detect(ReadOnlySpan<char> text)
    {
        var isolates = 0;
        for (var i = 0; i < text.Length;)
        {
            var codepoint = Codepoint.ReadAt(text, i, out var count);
            i += count;
            switch (codepoint.BiDiClass)
            {
                case BidiClass.LeftToRightIsolate or BidiClass.RightToLeftIsolate or BidiClass.FirstStrongIsolate:
                    isolates++;
                    break;
                case BidiClass.PopDirectionalIsolate when isolates > 0:
                    isolates--;
                    break;
                case BidiClass.LeftToRight when isolates == 0:
                    return FlowDirection.LeftToRight;
                case BidiClass.RightToLeft or BidiClass.ArabicLetter when isolates == 0:
                    return FlowDirection.RightToLeft;
            }
        }
        return null;
    }

    /// <summary>Whether <paramref name="text"/> has a right-to-left letter (Hebrew, Arabic…).</summary>
    public static bool HasRightToLeft(ReadOnlySpan<char> text)
    {
        for (var i = 0; i < text.Length;)
        {
            var codepoint = Codepoint.ReadAt(text, i, out var count);
            i += count;
            if (codepoint.BiDiClass is BidiClass.RightToLeft or BidiClass.ArabicLetter)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The direction a block's controls take: the one its HTML <c>dir</c> attribute gives it, left to right
    /// for code (code blocks, math, diagrams, front matter), else the direction of its text when
    /// <paramref name="detect"/> is <c>true</c>. <c>null</c> leaves the controls with their parent's direction.
    /// </summary>
    public static FlowDirection? Of(Block block, bool detect) =>
        Explicit(block)
        ?? (block is CodeBlock ? FlowDirection.LeftToRight : (FlowDirection?)null)
        ?? (detect ? Detect(block) : null);

    /// <summary>
    /// The direction <paramref name="block"/>'s text is laid out in, as far as the document tells: that of
    /// <see cref="Of"/> for the block or, when it gives none, for the nearest block around it that does.
    /// <c>null</c> when only the viewer's own direction applies.
    /// </summary>
    public static FlowDirection? Resolve(Block block, bool detect)
    {
        for (Block? current = block; current is not null and not MarkdownDocument; current = current.Parent)
        {
            // These are laid out by the renderer of the list or table around them, which takes the direction.
            if (current is ListItemBlock or TableRow or TableCell)
                continue;
            if (Of(current, detect) is { } direction)
                return direction;
        }
        return null;
    }

    /// <summary>
    /// The direction of the list or table that lays <paramref name="block"/> out: that of the list, for a block
    /// of a list item, whose marker is on the list's start side; that of the table, for a block of a cell in a
    /// column without an alignment, whose cells start on the table's start side. <c>null</c> otherwise.
    /// </summary>
    public static FlowDirection? OfLayout(Block block, bool detect) => block.Parent switch
    {
        ListItemBlock { Parent: ListBlock list } => Resolve(list, detect),
        TableCell { Parent: TableRow { Parent: Table table } row } cell
            when Column(row, cell) is var column && (column >= table.ColumnDefinitions.Count || table.ColumnDefinitions[column].Alignment is null)
            => Resolve(table, detect),
        _ => null,
    };

    // The column of a cell, counted as TableRenderer places it: TableCell.ColumnIndex is not filled in for pipe tables.
    private static int Column(TableRow row, TableCell cell)
    {
        var column = 0;
        foreach (var other in row)
        {
            if (other == cell)
                break;
            column += ((TableCell)other).ColumnSpan;
        }
        return column;
    }

    private static FlowDirection? Detect(string? text) => text is null ? null : Detect(text.AsSpan());
}
