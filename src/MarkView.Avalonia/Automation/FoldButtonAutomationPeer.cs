// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace MarkView.Avalonia.Automation;

/// <summary>
/// A control that shows or hides part of the document: a foldable heading's fold button, or the header of a
/// foldable alert, a details element or a thinking block.
/// </summary>
internal interface IFoldButton
{
    /// <summary>Whether the part it folds is shown.</summary>
    bool IsExpanded { get; }

    /// <summary>The text screen readers announce for the button, when no automation name is set.</summary>
    string AccessibleName { get; }

    /// <summary>Shows the part if hidden, hides it otherwise, as a click would.</summary>
    void Toggle();
}

/// <summary>
/// Exposes an <see cref="IFoldButton"/> to screen readers as a button named by what it folds (a heading's
/// text, an alert's title), which reports whether that part is expanded and can expand or collapse it,
/// as browsers expose a <c>&lt;summary&gt;</c>.
/// </summary>
internal sealed class FoldButtonAutomationPeer(Control owner) : ControlAutomationPeer(owner), IExpandCollapseProvider
{
    private IFoldButton Button => (IFoldButton)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string? GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? Button.AccessibleName : name;
    }

    // The name already reads the header's text, and its chevron is decoration.
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public ExpandCollapseState ExpandCollapseState =>
        Button.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => false;

    public void Expand()
    {
        if (!Button.IsExpanded) Button.Toggle();
    }

    public void Collapse()
    {
        if (Button.IsExpanded) Button.Toggle();
    }

    /// <summary>Tells screen readers that the fold state of <paramref name="owner"/> changed, if one is listening.</summary>
    internal static void RaiseExpandCollapseStateChanged(Control owner, bool wasExpanded, bool isExpanded) =>
        (FromElement(owner) as FoldButtonAutomationPeer)?.RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            wasExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            isExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
}
