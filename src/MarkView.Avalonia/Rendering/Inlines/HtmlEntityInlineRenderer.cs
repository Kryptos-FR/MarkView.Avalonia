using Avalonia.Controls.Documents;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

public class HtmlEntityInlineRenderer : AvaloniaObjectRenderer<HtmlEntityInline>
{
    protected override void Write(AvaloniaRenderer renderer, HtmlEntityInline obj)
    {
        renderer.WriteInline(new Run(obj.Transcoded.ToString()));
    }
}
