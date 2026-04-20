// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

public sealed class LineBreakInlineRenderer : AvaloniaObjectRenderer<LineBreakInline>
{
    protected override void Write(AvaloniaRenderer renderer, LineBreakInline obj)
    {
        if (obj.IsHard)
            renderer.WriteInline(new LineBreak());
        else
            renderer.WriteInline(new Run(" "));
    }
}
