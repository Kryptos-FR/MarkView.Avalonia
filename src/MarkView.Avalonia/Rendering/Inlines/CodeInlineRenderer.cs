using Avalonia.Controls;
using Avalonia.Layout;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

public class CodeInlineRenderer : AvaloniaObjectRenderer<CodeInline>
{
    protected override void Write(AvaloniaRenderer renderer, CodeInline obj)
    {
        var textBlock = new TextBlock
        {
            Text = obj.Content,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var border = new Border
        {
            Child = textBlock,
        };
        border.Classes.Add("markdown-code-inline");

        renderer.WriteInline(border);
    }
}
