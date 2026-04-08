using Avalonia.Controls.Documents;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="LiteralInline"/> as an Avalonia <see cref="Run"/>.
/// </summary>
public class LiteralInlineRenderer : AvaloniaObjectRenderer<LiteralInline>
{
    protected override void Write(AvaloniaRenderer renderer, LiteralInline obj)
    {
        renderer.WriteInline(new Run(obj.Content.ToString()));
    }
}
