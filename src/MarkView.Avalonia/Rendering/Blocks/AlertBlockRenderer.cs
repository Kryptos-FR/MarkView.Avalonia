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
        // obj.Kind is a StringSlice — call ToString() once, then use a lookup
        // to avoid two further allocations (ToLowerInvariant + ToUpperInvariant).
        var kindRaw = obj.Kind.ToString();
        var (kindLower, kindUpper) = kindRaw.ToUpperInvariant() switch
        {
            "NOTE"      => ("note",      "NOTE"),
            "TIP"       => ("tip",       "TIP"),
            "WARNING"   => ("warning",   "WARNING"),
            "IMPORTANT" => ("important", "IMPORTANT"),
            "CAUTION"   => ("caution",   "CAUTION"),
            var upper   => (kindRaw.ToLowerInvariant(), upper),
        };

        var header = new TextBlock { Text = kindUpper };
        header.Classes.Add("markdown-alert-header");

        var contentPanel = new StackPanel { Spacing = 4 };
        contentPanel.Classes.Add("markdown-alert-content");

        var outer = new StackPanel { Spacing = 4 };
        outer.Children.Add(header);
        outer.Children.Add(contentPanel);

        var border = new Border { Child = outer };
        border.Classes.Add("markdown-alert");
        border.Classes.Add($"markdown-alert-{kindLower}");

        renderer.Push(contentPanel);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteBlock(border);
    }
}
