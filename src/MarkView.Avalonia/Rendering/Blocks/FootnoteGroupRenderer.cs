// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

using Markdig.Extensions.Footnotes;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="FootnoteGroup"/> block as a numbered definition list below a separator.
/// Each footnote definition is registered as an anchor for back-navigation.
/// </summary>
public class FootnoteGroupRenderer : AvaloniaObjectRenderer<FootnoteGroup>
{
    protected override void Write(AvaloniaRenderer renderer, FootnoteGroup obj)
    {
        var separator = new Separator();
        separator.Classes.Add("markdown-thematic-break");
        renderer.WriteBlock(separator);

        var group = new StackPanel { Spacing = 4 };
        group.Classes.Add("markdown-footnote-group");

        foreach (var item in obj)
        {
            if (item is not Footnote fn) continue;

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            };
            row.Classes.Add("markdown-footnote-item");

            var label = new TextBlock
            {
                Text = $"{fn.Order}.",
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var content = new StackPanel { Spacing = 2 };
            Grid.SetColumn(content, 1);
            row.Children.Add(content);

            renderer.Push(content);
            renderer.WriteChildren(fn);
            renderer.Pop();

            renderer.RegisterAnchor($"fn-{fn.Order}", row);

            group.Children.Add(row);
        }

        renderer.WriteBlock(group);
    }
}
