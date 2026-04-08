using Avalonia.Controls.Documents;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

public class LineBreakInlineRenderer : AvaloniaObjectRenderer<LineBreakInline>
{
    protected override void Write(AvaloniaRenderer renderer, LineBreakInline obj)
    {
        if (obj.IsHard)
            renderer.WriteInline(new LineBreak());
        else
            renderer.WriteInline(new Run(" "));
    }
}
