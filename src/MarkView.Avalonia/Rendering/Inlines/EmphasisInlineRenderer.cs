// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;
using Avalonia.Media;

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

public sealed class EmphasisInlineRenderer : AvaloniaObjectRenderer<EmphasisInline>
{
    protected override void Write(AvaloniaRenderer renderer, EmphasisInline obj)
    {
        Span span = obj.DelimiterChar switch
        {
            // bold
            '*' or '_' when obj.DelimiterCount == 2 => new Bold(),
            // italic
            '*' or '_' => new Italic(),
            // strikethrough
            '~' when obj.DelimiterCount == 2 => new Span
            {
                TextDecorations = TextDecorations.Strikethrough
            },
            // subscript
            '~' => new Span
            {
                BaselineAlignment = BaselineAlignment.Subscript,
                FontFeatures = [FontFeature.Parse("subs")]
            },
            // superscript
            '^' => new Span
            {
                BaselineAlignment = BaselineAlignment.Superscript,
                FontFeatures = [FontFeature.Parse("sups")]
            },
            // underline
            '+' when obj.DelimiterCount == 2 => new Underline(),
            // marked
            '=' when obj.DelimiterCount == 2 => new Span
            {
                Classes = { "markdown-marked" }
            },
            _ => new Span(),
        };

        renderer.Push(span.Inlines);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteInline(span);
    }
}
