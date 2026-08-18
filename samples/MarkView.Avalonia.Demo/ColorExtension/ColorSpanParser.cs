// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;

namespace MarkView.Avalonia.Demo.ColorExtension;

/// <summary>
/// Parses <c>%[color:NAME]text%</c> spans into a <see cref="ColorSpanInline"/>.
/// <c>NAME</c> is anything Avalonia's <c>Color.TryParse</c> accepts (named colour or hex);
/// the content between the closing <c>]</c> and the next <c>%</c> is taken literally and must
/// not span multiple lines — either constraint failing leaves the input as plain text.
/// </summary>
public sealed class ColorSpanParser : InlineParser
{
    private const string Prefix = "[color:";

    public ColorSpanParser()
    {
        OpeningCharacters = ['%'];
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var span = slice.AsSpan();
        if (span.Length < Prefix.Length + 1 || !span.Slice(1).StartsWith(Prefix))
            return false;

        var nameStart = 1 + Prefix.Length;
        var closeBracket = span.Slice(nameStart).IndexOf(']');
        if (closeBracket < 0)
            return false;
        closeBracket += nameStart;

        var color = span.Slice(nameStart, closeBracket - nameStart).ToString();
        if (color.Length == 0)
            return false;

        var contentStart = closeBracket + 1;
        var rest = span.Slice(contentStart);
        var closingPercent = rest.IndexOfAny('%', '\r', '\n');
        if (closingPercent < 0 || rest[closingPercent] != '%')
            return false;

        var content = rest.Slice(0, closingPercent).ToString();

        var startPosition = slice.Start;
        var consumedLength = contentStart + closingPercent + 1;
        slice.Start = startPosition + consumedLength;

        var colorSpan = new ColorSpanInline(color, content)
        {
            Span = new SourceSpan(
                processor.GetSourcePosition(startPosition, out var line, out var column),
                processor.GetSourcePosition(slice.Start - 1)),
            Line = line,
            Column = column,
        };

        processor.Inline = colorSpan;
        return true;
    }
}
