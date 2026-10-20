// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The <c>Border</c> around a control an application's factory returned for a block (a fence
/// renderer's, a container renderer's). The document can't see into that control, so the host
/// tells it where the block's rendered markdown is (<see cref="ContentPanel"/>) and which of the
/// control's own parts take the keyboard focus (<see cref="GetFocusableParts"/>).
/// </summary>
internal sealed class ComponentHost : Border
{
    protected override Type StyleKeyOverride => typeof(Border);

    /// <summary>
    /// The panel holding the block's rendered markdown, somewhere inside <see cref="Decorator.Child"/>,
    /// or <see langword="null"/> when the block has none (a fenced block).
    /// </summary>
    public Panel? ContentPanel { get; init; }

    /// <summary>
    /// The controls of <see cref="Decorator.Child"/> that are Tab stops now, in visual order, the
    /// control itself included, leaving out <see cref="ContentPanel"/>, whose stops the document records
    /// on its own. Read at each Tab press, so it finds what the control's template holds once applied
    /// (an expander's header) and skips the parts it hides or disables.
    /// </summary>
    public List<IInputElement> GetFocusableParts()
    {
        var parts = new List<IInputElement>();
        if (Child is { } child)
            AddFocusableParts(child, parts);
        return parts;
    }

    private void AddFocusableParts(Visual visual, List<IInputElement> parts)
    {
        if (visual == ContentPanel || !visual.IsVisible)
            return;
        if (visual is InputElement { Focusable: true, IsTabStop: true } input)
        {
            if (input.IsEffectivelyEnabled)
                parts.Add(input);
            // A focusable control is one stop, whatever it is made of.
            return;
        }
        foreach (var child in visual.GetVisualChildren())
            AddFocusableParts(child, parts);
    }
}
