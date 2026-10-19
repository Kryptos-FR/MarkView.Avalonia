// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using MarkView.Avalonia.Automation;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The fold state of a foldable alert (<c>&gt; [!KIND]-</c> / <c>&gt; [!KIND]+</c>), or of an HTML
/// <c>&lt;details&gt;</c> element (see <see cref="DetailsBlockRenderer"/>). Clicking (tapping) the
/// header, or pressing Enter or Space while it has focus, shows or hides the body. The block's
/// <see cref="Border"/> carries the <c>:expanded</c> pseudo-class while the body is shown, and the
/// header's chevron (<c>markdown-alert-chevron</c> / <c>markdown-details-chevron</c>) points right when
/// collapsed and down when expanded.
/// </summary>
internal sealed class CollapsibleAlert
{
    private static readonly AttachedProperty<CollapsibleAlert?> StateProperty =
        AvaloniaProperty.RegisterAttached<Border, CollapsibleAlert?>("State", typeof(CollapsibleAlert));

    private readonly Border _border;
    private readonly FoldHeader _header;
    private readonly Control _body;
    private readonly AlertChevron _chevron;
    private readonly Action<bool>? _expandedChanged;

    private CollapsibleAlert(Border border, FoldHeader header, AlertChevron chevron, Control body, bool isExpanded, Action<bool>? expandedChanged)
    {
        _border = border;
        _header = header;
        _body = body;
        _chevron = chevron;
        SetState(isExpanded);
        _expandedChanged = expandedChanged;

        header.Fold = this;
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

    /// <summary>
    /// Whether the body is shown. Changing it reports the new state to the callback given to <see cref="Attach"/>.
    /// </summary>
    public bool IsExpanded
    {
        get => _body.IsVisible;
        set
        {
            if (value == IsExpanded) return;
            SetState(value);
            FoldButtonAutomationPeer.RaiseExpandCollapseStateChanged(_header, !value, value);
            _expandedChanged?.Invoke(value);
        }
    }

    private void SetState(bool isExpanded)
    {
        _body.IsVisible = isExpanded;
        _chevron.IsExpanded = isExpanded;
        ((IPseudoClasses)_border.Classes).Set(":expanded", isExpanded);
    }

    /// <summary>The block's border, named by its title for screen readers.</summary>
    internal Border Border => _border;

    /// <summary>Shows the body if hidden, hides it otherwise.</summary>
    public void Toggle() => IsExpanded = !IsExpanded;

    /// <summary>
    /// Makes <paramref name="border"/> a foldable alert whose <paramref name="header"/> toggles
    /// <paramref name="body"/>, with <paramref name="chevron"/> (inline in the header) showing the state.
    /// <paramref name="expandedChanged"/>, when given, is called with the new state each time the body is shown or hidden.
    /// </summary>
    public static CollapsibleAlert Attach(Border border, FoldHeader header, AlertChevron chevron, Control body, bool isExpanded,
        Action<bool>? expandedChanged = null) =>
        new(border, header, chevron, body, isExpanded, expandedChanged);

    /// <summary>Returns the fold state of an alert border, or <c>null</c> when it is not foldable.</summary>
    public static CollapsibleAlert? Get(Border border) => border.GetValue(StateProperty);

    /// <summary>
    /// Expands every collapsed alert or details element that contains <paramref name="control"/>, so that it
    /// can be shown (e.g. a heading navigated to by its anchor). Returns <c>true</c> when one was expanded.
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
