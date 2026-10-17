// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;

namespace MarkView.Avalonia.Math.Parsing;

/// <summary>
/// Parses LaTeX's bracket delimiters into a <see cref="BracketMathInline"/>: <c>\(...\)</c> on one line,
/// and <c>\[...\]</c>, which may span the lines of its paragraph. Without it, CommonMark reads <c>\(</c>
/// and <c>\[</c> as an escaped parenthesis or bracket. Anything else is left to the other parsers: an
/// opening glued to a letter or digit (<c>arr\[i\]</c>, like <c>$</c> math), an unclosed opening and an
/// empty formula all keep their CommonMark meaning.
/// </summary>
internal sealed class BracketMathInlineParser : InlineParser
{
    public BracketMathInlineParser()
    {
        OpeningCharacters = ['\\'];
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var opening = slice.PeekChar();
        if (opening is not ('(' or '[') || char.IsLetterOrDigit(slice.PeekCharExtra(-1)))
            return false;

        var display = opening == '[';
        var closing = display ? ']' : ')';
        var text = slice.Text;
        var start = slice.Start;
        // slice.Start is on '\'; the paragraph's lines follow, joined by '\n'.
        for (var i = start + 2; i < slice.End; i++)
        {
            switch (text[i])
            {
                case '\n' when !display:
                    return false;
                case '\\' when text[i + 1] == closing:
                    return Accept(processor, ref slice, display, i);
                case '\\' when text[i + 1] != '\n':
                    i++; // a TeX control symbol such as "\\" or "\}" cannot close the formula
                    break;
            }
        }
        return false;
    }

    private static bool Accept(InlineProcessor processor, ref StringSlice slice, bool display, int closingStart)
    {
        var start = slice.Start;
        var content = new StringSlice(slice.Text, start + 2, closingStart - 1);
        content.Trim();
        if (content.IsEmpty)
            return false; // "\(\)" or "\[ \]": nothing to typeset

        var end = closingStart + 1;
        processor.Inline = new BracketMathInline
        {
            Span = new SourceSpan(processor.GetSourcePosition(start, out var line, out var column), processor.GetSourcePosition(end)),
            Line = line,
            Column = column,
            Content = content,
            IsDisplay = display,
            TextStart = start,
            TextEnd = closingStart,
        };
        slice.Start = end + 1;
        return true;
    }
}
