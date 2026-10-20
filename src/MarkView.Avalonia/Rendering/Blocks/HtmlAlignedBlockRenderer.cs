// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Media;

using MarkView.Avalonia.Parsing;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders blocks aligned by an HTML <c>align</c> attribute (<see cref="HtmlAlignedBlock"/>) in a
/// <c>markdown-html-align</c> <see cref="StackPanel"/>, also classed <c>markdown-html-align-left</c>,
/// <c>-center</c>, <c>-right</c> or <c>-justify</c>. The panel's <see cref="TextBlock.TextAlignmentProperty"/>
/// is inherited by the text inside it, images included, as <c>text-align</c> is in HTML. A <c>&lt;div dir="…"&gt;</c>
/// without <c>align</c> gets a panel without these classes, which <see cref="AvaloniaRenderer"/> gives the direction.
/// </summary>
internal sealed class HtmlAlignedBlockRenderer : AvaloniaObjectRenderer<HtmlAlignedBlock>
{
    protected override void Write(AvaloniaRenderer renderer, HtmlAlignedBlock obj)
    {
        var panel = new StackPanel { Spacing = 8 };
        if (obj.Alignment is { } htmlAlignment)
        {
            var (alignment, name) = htmlAlignment switch
            {
                HtmlAlignment.Left => (TextAlignment.Left, "left"),
                HtmlAlignment.Center => (TextAlignment.Center, "center"),
                HtmlAlignment.Right => (TextAlignment.Right, "right"),
                _ => (TextAlignment.Justify, "justify"),
            };
            panel.Classes.Add("markdown-html-align");
            panel.Classes.Add($"markdown-html-align-{name}");
            TextBlock.SetTextAlignment(panel, alignment);
        }

        renderer.Push(panel);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteBlock(panel);
    }
}
