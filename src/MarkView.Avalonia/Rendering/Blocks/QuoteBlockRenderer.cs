using Avalonia.Controls;
using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering.Blocks;

public class QuoteBlockRenderer : AvaloniaObjectRenderer<QuoteBlock>
{
    protected override void Write(AvaloniaRenderer renderer, QuoteBlock obj)
    {
        var contentPanel = new StackPanel { Spacing = 8 };
        var border = new Border { Child = contentPanel };
        border.Classes.Add("markdown-blockquote");

        renderer.Push(contentPanel);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteBlock(border);
    }
}
