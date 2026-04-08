using Avalonia.Controls.Documents;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

public class DelimiterInlineRenderer : AvaloniaObjectRenderer<DelimiterInline>
{
    protected override void Write(AvaloniaRenderer renderer, DelimiterInline obj)
    {
        renderer.WriteInline(new Run(obj.ToLiteral()));
        renderer.WriteChildren(obj);
    }
}
