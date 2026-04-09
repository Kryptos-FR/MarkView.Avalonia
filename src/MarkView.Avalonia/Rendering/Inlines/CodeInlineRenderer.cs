using Avalonia.Controls.Documents;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="CodeInline"/> as a styled <see cref="Run"/>.
/// Using Run (instead of Border+InlineUIContainer) keeps the text selectable.
/// </summary>
public class CodeInlineRenderer : AvaloniaObjectRenderer<CodeInline>
{
    protected override void Write(AvaloniaRenderer renderer, CodeInline obj)
    {
        var run = new Run(obj.Content);
        run.Classes.Add("markdown-code-inline");
        renderer.WriteInline(run);
    }
}
