// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

using Markdig.Extensions.Footers;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="FooterBlock"/> (<c>^^</c> lines, Markdig's <c>UseFooters()</c>)
/// as a <c>StackPanel.markdown-footer</c> around its blocks.
/// </summary>
public sealed class FooterBlockRenderer : AvaloniaObjectRenderer<FooterBlock>
{
    protected override void Write(AvaloniaRenderer renderer, FooterBlock obj)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Classes.Add("markdown-footer");

        renderer.Push(panel);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteBlock(panel);
    }
}
