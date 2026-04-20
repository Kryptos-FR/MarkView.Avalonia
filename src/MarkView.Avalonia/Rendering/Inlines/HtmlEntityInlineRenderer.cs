// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

public sealed class HtmlEntityInlineRenderer : AvaloniaObjectRenderer<HtmlEntityInline>
{
    protected override void Write(AvaloniaRenderer renderer, HtmlEntityInline obj)
    {
        renderer.WriteInline(new Run(obj.Transcoded.ToString()));
    }
}
