// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Extensions.Alerts;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// An <see cref="AlertInlineParser"/> that also accepts the Obsidian-style extras on the first line of an
/// alert, which Markdig's parser rejects (leaving the whole quote a plain blockquote):
/// <list type="bullet">
/// <item><c>[!KIND]-</c> / <c>[!KIND]+</c>: a foldable alert, starting collapsed / expanded;</item>
/// <item><c>[!KIND] Custom title</c> (optionally after a fold marker): a title shown instead of the kind.</item>
/// </list>
/// A plain <c>[!KIND]</c> line is handed to the base parser unchanged. Like the base parser, the kind is
/// any run of ASCII letters, so unknown kinds such as <c>[!QUESTION]</c> are accepted too.
/// </summary>
internal sealed class ExtendedAlertInlineParser : AlertInlineParser
{
    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        if (slice.PeekChar() != '!')
            return false;

        // slice.Start is on '['; the paragraph's lines follow, joined by '\n'.
        var text = slice.Text;
        var end = slice.End;
        var kindStart = slice.Start + 2;
        var i = kindStart;
        while (i <= end && char.IsAsciiLetter(text[i]))
            i++;
        if (i == kindStart || i > end || text[i] != ']')
            return false;
        var kind = new StringSlice(text, kindStart, i - 1);
        i++;

        var fold = AlertFold.None;
        if (i <= end && text[i] is '-' or '+')
        {
            fold = text[i] == '-' ? AlertFold.Collapsed : AlertFold.Expanded;
            i++;
        }

        var titleStart = i;
        while (titleStart <= end && text[titleStart] is ' ' or '\t')
            titleStart++;
        var hasTitle = titleStart <= end && text[titleStart] is not ('\n' or '\r');

        if (fold == AlertFold.None && !hasTitle)
            return base.Match(processor, ref slice);

        // A title must be separated from "]" or the fold marker: "[!NOTE]x" is not an alert.
        if (hasTitle && titleStart == i)
            return false;

        if (processor.Block is not ParagraphBlock { Parent: QuoteBlock parent, Inline: var inline }
            || inline?.FirstChild != null
            || parent is AlertBlock
            || IsInsideAlertBlock(parent)
            || (!AllowNestedAlerts && parent.Parent is not MarkdownDocument))
            return false;

        var alert = new ExtendedAlertBlock(kind)
        {
            Span = parent.Span,
            Line = parent.Line,
            Column = parent.Column,
            Fold = fold,
            HasTitle = hasTitle,
        };
        var attributes = alert.GetAttributes();
        attributes.AddClass("markdown-alert");
        attributes.AddClass($"markdown-alert-{kind.ToString().ToLowerInvariant()}");
        parent.ReplaceBy(alert);
        processor.ReplaceParentContainer(parent, alert);

        if (hasTitle)
        {
            // Leave the title to the inline parsers: it becomes the start of the first paragraph.
            slice.Start = titleStart;
        }
        else
        {
            // Consume the rest of the line, like the base parser does after a plain "[!KIND]".
            i = titleStart;
            if (i <= end && text[i] == '\r') i++;
            if (i <= end && text[i] == '\n') i++;
            slice.Start = i;
        }
        return true;
    }

    private static bool IsInsideAlertBlock(Block block)
    {
        for (var parent = block.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is AlertBlock)
                return true;
        }
        return false;
    }
}
