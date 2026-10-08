// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Attached properties through which renderers make non-text content (images, formulas,
/// diagrams) take part in <see cref="MarkdownViewer"/>'s document-wide text selection.
/// </summary>
public static class MarkdownSelection
{
    /// <summary>
    /// The text copied in place of a control when a selection spans it.
    /// <list type="bullet">
    /// <item>A control written inline (<see cref="AvaloniaRenderer.WriteInline(Control)"/>) is
    /// highlighted with the surrounding text and copied as this text, or as nothing when unset.</item>
    /// <item>A control written as a block (<see cref="AvaloniaRenderer.WriteBlock"/>) is selectable as a
    /// whole only when this property is set; otherwise it is skipped by selection.</item>
    /// </list>
    /// </summary>
    public static readonly AttachedProperty<string?> CopyTextProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("CopyText", typeof(MarkdownSelection));

    /// <summary>Gets the text copied in place of <paramref name="control"/>.</summary>
    public static string? GetCopyText(Control control) => control.GetValue(CopyTextProperty);

    /// <summary>Sets the text copied in place of <paramref name="control"/>.</summary>
    public static void SetCopyText(Control control, string? value) => control.SetValue(CopyTextProperty, value);
}
