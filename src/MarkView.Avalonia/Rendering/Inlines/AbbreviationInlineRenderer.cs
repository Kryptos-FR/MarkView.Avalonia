// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;

using Markdig.Extensions.Abbreviations;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="AbbreviationInline"/> as a <see cref="TextBlock"/>
/// inside an <see cref="InlineUIContainer"/> with a tooltip showing the full definition.
/// </summary>
public sealed class AbbreviationInlineRenderer : AvaloniaObjectRenderer<AbbreviationInline>
{
    protected override void Write(AvaloniaRenderer renderer, AbbreviationInline obj)
    {
        var abbr = obj.Abbreviation;
        // Abbreviation.Label is the matched word; Abbreviation.Text (a field) is the definition.
        var labelText = abbr?.Label ?? string.Empty;
        var tooltipText = abbr?.Text.ToString() ?? string.Empty;

        var tb = new TextBlock { Text = labelText };
        tb.Classes.Add("markdown-abbr");
        if (!string.IsNullOrEmpty(tooltipText))
            ToolTip.SetTip(tb, tooltipText);

        renderer.WriteInline(new InlineUIContainer { Child = tb });
    }
}
