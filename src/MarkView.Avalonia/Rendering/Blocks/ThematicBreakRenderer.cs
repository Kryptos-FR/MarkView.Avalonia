// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering.Blocks;

public class ThematicBreakRenderer : AvaloniaObjectRenderer<ThematicBreakBlock>
{
    protected override void Write(AvaloniaRenderer renderer, ThematicBreakBlock obj)
    {
        var separator = new Separator();
        separator.Classes.Add("markdown-thematic-break");
        renderer.WriteBlock(separator);
    }
}
