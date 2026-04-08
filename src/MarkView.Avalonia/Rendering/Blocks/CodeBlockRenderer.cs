using Avalonia.Controls;
using Avalonia.Media;
using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering.Blocks;

public class CodeBlockRenderer : AvaloniaObjectRenderer<CodeBlock>
{
    protected override void Write(AvaloniaRenderer renderer, CodeBlock obj)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
        };

        var border = new Border
        {
            Child = textBlock,
        };
        border.Classes.Add("markdown-code-block");

        if (obj is FencedCodeBlock fenced && !string.IsNullOrEmpty(fenced.Info))
        {
            border.Classes.Add($"language-{fenced.Info}");
        }

        renderer.Push(textBlock.Inlines!);
        renderer.WriteLeafRawLines(obj);
        renderer.Pop();

        renderer.WriteBlock(border);
    }
}
