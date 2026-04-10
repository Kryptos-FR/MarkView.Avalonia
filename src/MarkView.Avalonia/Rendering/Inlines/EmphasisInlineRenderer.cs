// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;
using Avalonia.Media;

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
            '~' when obj.DelimiterCount == 2 => CreateStrikethrough(),
            _ => new Span(),
        };

        renderer.Push(span.Inlines);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteInline(span);
    }

    private static Span CreateStrikethrough()
    {
        var span = new Span();
        span.TextDecorations = TextDecorations.Strikethrough;
        return span;
    }
}
