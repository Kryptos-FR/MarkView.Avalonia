// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// The key an HTML <c>&lt;kbd&gt;</c> element is rendered as: a <see cref="Border"/> (styled as one) around the
/// <see cref="TextBlock"/> of its text, embedded in a line of text with that text on the line's baseline rather
/// than with its bottom edge there.
/// </summary>
internal sealed class HtmlKeyboardKey : Border
{
    protected override Type StyleKeyOverride => typeof(Border);

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        // The text a key is embedded in reads its baseline from this property, measured from the top of the key.
        if (Child is TextBlock text)
            TextBlock.SetBaselineOffset(this, Margin.Top + BorderThickness.Top + Padding.Top + text.TextLayout.Baseline);
        return size;
    }
}
