// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="CodeInline"/> as a styled <see cref="Run"/>.
/// Using Run (instead of Border+InlineUIContainer) keeps the text selectable.
/// In right-to-left text, the code gets a left-to-right mark on each side (see <see cref="DirectionMark"/>), so it
/// reads left to right in one piece.
/// </summary>
public sealed class CodeInlineRenderer : AvaloniaObjectRenderer<CodeInline>
{
    protected override void Write(AvaloniaRenderer renderer, CodeInline obj)
    {
        var run = new Run(obj.Content);
        run.Classes.Add("markdown-code-inline");
        if (!renderer.IsRightToLeft(obj))
        {
            renderer.WriteInline(run);
            return;
        }

        renderer.WriteInline(DirectionMark.LeftToRight());
        renderer.WriteInline(run);
        renderer.WriteInline(DirectionMark.LeftToRight());
    }
}
