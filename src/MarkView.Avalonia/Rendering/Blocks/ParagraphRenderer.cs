// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;

using Markdig.Extensions.Alerts;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="ParagraphBlock"/> as a <see cref="MarkdownSelectableTextBlock"/>.
/// </summary>
public sealed class ParagraphRenderer : AvaloniaObjectRenderer<ParagraphBlock>
{
    protected override void Write(AvaloniaRenderer renderer, ParagraphBlock obj)
    {
        var textBlock = CreateTextBlock(renderer, obj);

        var wasWritingParagraph = renderer.IsWritingParagraph;
        renderer.IsWritingParagraph = true;
        renderer.Push(textBlock.Inlines!);
        renderer.WriteLeafInline(obj);
        renderer.Pop();
        renderer.IsWritingParagraph = wasWritingParagraph;

        renderer.WriteBlock(textBlock);
    }

    /// <summary>
    /// Writes a paragraph made of <paramref name="first"/> and its following siblings, for a renderer that
    /// shows a paragraph's leading inlines elsewhere (an alert's custom title).
    /// </summary>
    internal static void Write(AvaloniaRenderer renderer, Inline first)
    {
        var root = first.Parent;
        while (root?.Parent is not null)
            root = root.Parent;
        var textBlock = CreateTextBlock(renderer, root?.ParentBlock);

        var wasWritingParagraph = renderer.IsWritingParagraph;
        renderer.IsWritingParagraph = true;
        renderer.Push(textBlock.Inlines!);
        for (Inline? inline = first; inline is not null; inline = inline.NextSibling)
            renderer.Write(inline);
        renderer.Pop();
        renderer.IsWritingParagraph = wasWritingParagraph;

        renderer.WriteBlock(textBlock);
    }

    private static MarkdownSelectableTextBlock CreateTextBlock(AvaloniaRenderer renderer, Block? paragraph)
    {
        var textBlock = new MarkdownSelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Renderer = renderer,
        };
        textBlock.Classes.Add("markdown-paragraph");
        renderer.ApplyTheme(textBlock, ThemeKey(paragraph));
        return textBlock;
    }

    /// <summary>
    /// The theme of a paragraph: that of the innermost alert it is in, or else of a table, a quote or a
    /// list, in that order.
    /// </summary>
    private static string ThemeKey(Block? paragraph)
    {
        bool inTable = false, inQuote = false, inList = false;
        for (var parent = paragraph?.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is AlertBlock)
                return "MarkdownAlertParagraphTheme";
            inTable |= parent is Table;
            inQuote |= parent is QuoteBlock;
            inList |= parent is ListBlock;
        }
        return inTable ? "MarkdownTableParagraphTheme"
            : inQuote ? "MarkdownQuoteParagraphTheme"
            : inList ? "MarkdownListParagraphTheme"
            : "MarkdownParagraphTheme";
    }
}
