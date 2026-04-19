// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering.Blocks;

public sealed class QuoteBlockRenderer : AvaloniaObjectRenderer<QuoteBlock>
{
    protected override void Write(AvaloniaRenderer renderer, QuoteBlock obj)
    {
        var contentPanel = new StackPanel { Spacing = 8 };
        var border = new Border { Child = contentPanel };
        border.Classes.Add("markdown-blockquote");

        renderer.Push(contentPanel);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteBlock(border);
    }
}
