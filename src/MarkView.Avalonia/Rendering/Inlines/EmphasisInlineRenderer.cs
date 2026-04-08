using Avalonia.Controls.Documents;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

public class EmphasisInlineRenderer : AvaloniaObjectRenderer<EmphasisInline>
{
    protected override void Write(AvaloniaRenderer renderer, EmphasisInline obj)
    {
        Span span = obj.DelimiterChar switch
        {
            '*' or '_' when obj.DelimiterCount == 2 => new Bold(),
            '*' or '_' => new Italic(),
            _ => new Span(),
        };

        renderer.Push(span.Inlines);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteInline(span);
    }
}
