// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Math.Parsing;

/// <summary>
/// Adds <see cref="BracketMathInlineParser"/>, then turns each <c>\[...\]</c> formula that has its lines to
/// itself into a <see cref="BracketMathBlock"/>, splitting its paragraph around it. A block parser could
/// not do that: an unclosed <c>\[</c> must stay paragraph text, which is only known once the paragraph
/// has been read, and LaTeX output often puts the formula right under a line of text, without the blank
/// line a block would need. Added by <see cref="MarkdownMathExtensions.UseMathBracketDelimiters"/>.
/// </summary>
internal sealed class BracketMathExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        // Ahead of EscapeInlineParser, which would read "\(" as an escaped parenthesis.
        pipeline.InlineParsers.Insert(0, new BracketMathInlineParser());
        pipeline.DocumentProcessed += PromoteDisplayFormulas;
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }

    private static void PromoteDisplayFormulas(MarkdownDocument document)
    {
        foreach (var paragraph in document.Descendants<ParagraphBlock>().ToList())
        {
            var formulas = paragraph.Inline!.OfType<BracketMathInline>().Where(HasItsLinesToItself).ToList();
            if (formulas.Count > 0)
                Split(paragraph, formulas);
        }
    }

    private static bool HasItsLinesToItself(BracketMathInline formula) =>
        formula is { IsDisplay: true, PreviousSibling: null or LineBreakInline, NextSibling: null or LineBreakInline };

    /// <summary>
    /// Replaces <paramref name="paragraph"/> with a block for each of <paramref name="formulas"/>, and a
    /// paragraph for each run of lines around them. The lines before the first formula stay in
    /// <paramref name="paragraph"/>, which keeps its source position (a task list item's checkbox is found
    /// through it). The other blocks get their line from the paragraph's; their column and span are, like
    /// any inline's, only tracked with <c>UsePreciseSourceLocation()</c>.
    /// </summary>
    private static void Split(ParagraphBlock paragraph, List<BracketMathInline> formulas)
    {
        var parent = paragraph.Parent!;
        var index = parent.IndexOf(paragraph);
        parent.RemoveAt(index);
        var text = formulas[0].Content.Text;
        var inlines = paragraph.Inline!.ToList();
        foreach (var inline in inlines)
            inline.Remove();

        ContainerInline? lines = paragraph.Inline;
        var line = paragraph.Line;
        for (var i = 0; i < inlines.Count; i++)
        {
            if (inlines[i] is BracketMathInline formula && formulas.Contains(formula))
            {
                AddLines();
                parent.Insert(index++, ToBlock(formula, paragraph.Line + LineCount(formula.TextStart)));
                line = paragraph.Line + LineCount(formula.TextEnd) + 1;
                i++; // past the line break after the formula
            }
            else
            {
                (lines ??= new ContainerInline()).AppendChild(inlines[i]);
            }
        }
        AddLines();

        void AddLines()
        {
            if (lines?.LastChild is LineBreakInline lineBreak)
                lineBreak.Remove(); // the line break before the formula
            if (lines?.FirstChild is { } first)
            {
                parent.Insert(index++, lines == paragraph.Inline ? paragraph : new ParagraphBlock(paragraph.Parser)
                {
                    Inline = lines,
                    Span = new SourceSpan(first.Span.Start, lines.LastChild!.Span.End),
                    Line = line,
                    Column = first.Column,
                });
            }
            lines = null;
        }

        int LineCount(int offset) => text.AsSpan(0, offset).Count('\n');
    }

    private static BracketMathBlock ToBlock(BracketMathInline formula, int line)
    {
        var block = new BracketMathBlock
        {
            Span = formula.Span,
            Line = line,
            Column = formula.Column,
            Lines = new StringLineGroup(1),
        };

        // One line per source line, without its line ending, as Markdig stores the lines of a
        // $$ block: the source is then joined by '\n' even when the document uses "\r\n".
        var content = formula.Content;
        var text = content.Text;
        var lineStart = content.Start;
        // Stryker disable once Equality : the content is trimmed, so its last character is never '\n'
        for (var i = content.Start; i < content.End; i++)
        {
            if (text[i] != '\n')
                continue;
            block.Lines.Add(new StringSlice(text, lineStart, text[i - 1] == '\r' ? i - 2 : i - 1, NewLine.LineFeed));
            lineStart = i + 1;
        }
        block.Lines.Add(new StringSlice(text, lineStart, content.End));
        return block;
    }
}
