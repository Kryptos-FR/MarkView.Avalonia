using Avalonia.Controls;
using Avalonia.Media;
using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="HeadingBlock"/> as an Avalonia <see cref="TextBlock"/>.
/// </summary>
public class HeadingRenderer : AvaloniaObjectRenderer<HeadingBlock>
{
    protected override void Write(AvaloniaRenderer renderer, HeadingBlock obj)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
        };
        textBlock.Classes.Add("markdown-heading");
        textBlock.Classes.Add($"markdown-h{obj.Level}");

        renderer.Push(textBlock.Inlines!);
        renderer.WriteLeafInline(obj);
        renderer.Pop();

        renderer.WriteBlock(textBlock);
    }
}
