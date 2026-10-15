// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The fold state of a foldable alert (<c>&gt; [!KIND]-</c> / <c>&gt; [!KIND]+</c>). Clicking (tapping) the
/// header, or pressing Enter or Space while it has focus, shows or hides the body. The alert's
/// <see cref="Border"/> carries the <c>:expanded</c> pseudo-class while the body is shown, and the
/// header's chevron run (<c>markdown-alert-chevron</c>) points right when collapsed and down when expanded.
/// </summary>
internal sealed class CollapsibleAlert
{
    internal const string CollapsedGlyph = "▸"; // ▸
    internal const string ExpandedGlyph = "▾";  // ▾

    private static readonly AttachedProperty<CollapsibleAlert?> StateProperty =
        AvaloniaProperty.RegisterAttached<Border, CollapsibleAlert?>("State", typeof(CollapsibleAlert));

    private readonly Border _border;
    private readonly Control _body;
    private readonly Run _chevron;

    private CollapsibleAlert(Border border, TextBlock header, Run chevron, Control body, bool isExpanded)
    {
        _border = border;
        _body = body;
        _chevron = chevron;
        IsExpanded = isExpanded;

        header.Focusable = true;
        // A click, like a link's in MarkdownViewer: the press implicitly captures the pointer to the header,
        // so the header sees the release only when the press started on it, and no release at all once
        // the viewer has turned the press into a selection drag (it then captures the pointer itself).
        // Unlike Tapped, every click of a double click counts.
        header.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Left
                || !new Rect(header.Bounds.Size).Contains(e.GetPosition(header)))
                return;
            Toggle();
            e.Handled = true;
        };
        header.KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Space)) return;
            Toggle();
            e.Handled = true;
        };
        border.SetValue(StateProperty, this);
    }

    /// <summary>Whether the body is shown.</summary>
    public bool IsExpanded
    {
        get => _body.IsVisible;
        set
        {
            _body.IsVisible = value;
            _chevron.Text = value ? ExpandedGlyph : CollapsedGlyph;
            ((IPseudoClasses)_border.Classes).Set(":expanded", value);
        }
    }

    /// <summary>Shows the body if hidden, hides it otherwise.</summary>
    public void Toggle() => IsExpanded = !IsExpanded;

    /// <summary>
    /// Makes <paramref name="border"/> a foldable alert whose <paramref name="header"/> toggles
    /// <paramref name="body"/>, with <paramref name="chevron"/> (a run of the header) showing the state.
    /// </summary>
    public static CollapsibleAlert Attach(Border border, TextBlock header, Run chevron, Control body, bool isExpanded) =>
        new(border, header, chevron, body, isExpanded);

    /// <summary>Returns the fold state of an alert border, or <c>null</c> when it is not foldable.</summary>
    public static CollapsibleAlert? Get(Border border) => border.GetValue(StateProperty);

    /// <summary>
    /// Expands every collapsed alert that contains <paramref name="control"/>, so that it can be shown
    /// (e.g. a heading navigated to by its anchor). Returns <c>true</c> when an alert was expanded.
    /// </summary>
    public static bool ExpandAncestors(Control control)
    {
        var expanded = false;
        for (var visual = control.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Border border && Get(border) is { IsExpanded: false } alert)
            {
                alert.IsExpanded = true;
                expanded = true;
            }
        }
        return expanded;
    }
}
