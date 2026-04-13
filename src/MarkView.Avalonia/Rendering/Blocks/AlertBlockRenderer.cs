// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

using Markdig.Extensions.Alerts;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="AlertBlock"/> as a styled bordered panel with a header label.
/// Supports GitHub GFM variants: NOTE, TIP, WARNING, IMPORTANT, CAUTION.
/// BorderBrush per variant is provided by AXAML theme styles (markdown-alert-note, etc.).
/// </summary>
public class AlertBlockRenderer : AvaloniaObjectRenderer<AlertBlock>
{
    protected override void Write(AvaloniaRenderer renderer, AlertBlock obj)
    {
        var kind = obj.Kind.ToString().ToLowerInvariant();

        var header = new TextBlock { Text = kind.ToUpperInvariant() };
        header.Classes.Add("markdown-alert-header");

        var contentPanel = new StackPanel { Spacing = 4 };
        contentPanel.Classes.Add("markdown-alert-content");

        var outer = new StackPanel { Spacing = 4 };
        outer.Children.Add(header);
        outer.Children.Add(contentPanel);

        var border = new Border { Child = outer };
        border.Classes.Add("markdown-alert");
        border.Classes.Add($"markdown-alert-{kind}");

        renderer.Push(contentPanel);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteBlock(border);
    }
}
