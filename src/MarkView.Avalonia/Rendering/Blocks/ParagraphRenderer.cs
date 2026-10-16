// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;

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
        var textBlock = CreateTextBlock(renderer);

        renderer.Push(textBlock.Inlines!);
        renderer.WriteLeafInline(obj);
        renderer.Pop();

        renderer.WriteBlock(textBlock);
    }

    /// <summary>
    /// Writes a paragraph made of <paramref name="first"/> and its following siblings, for a renderer that
    /// shows a paragraph's leading inlines elsewhere (an alert's custom title).
    /// </summary>
    internal static void Write(AvaloniaRenderer renderer, Inline first)
    {
        var textBlock = CreateTextBlock(renderer);

        renderer.Push(textBlock.Inlines!);
        for (Inline? inline = first; inline is not null; inline = inline.NextSibling)
            renderer.Write(inline);
        renderer.Pop();

        renderer.WriteBlock(textBlock);
    }

    private static MarkdownSelectableTextBlock CreateTextBlock(AvaloniaRenderer renderer)
    {
        var textBlock = new MarkdownSelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Renderer = renderer,
        };
        textBlock.Classes.Add("markdown-paragraph");
        return textBlock;
    }
}
