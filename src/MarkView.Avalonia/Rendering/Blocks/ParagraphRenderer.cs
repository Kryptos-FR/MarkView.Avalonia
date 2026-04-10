using Avalonia.Media;
using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="ParagraphBlock"/> as a <see cref="MarkdownSelectableTextBlock"/>.
/// </summary>
public class ParagraphRenderer : AvaloniaObjectRenderer<ParagraphBlock>
{
    protected override void Write(AvaloniaRenderer renderer, ParagraphBlock obj)
    {
        var textBlock = new MarkdownSelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Renderer = renderer,
        };
        textBlock.Classes.Add("markdown-paragraph");

        renderer.Push(textBlock.Inlines!);
        renderer.WriteLeafInline(obj);
        renderer.Pop();

        renderer.WriteBlock(textBlock);
    }
}
