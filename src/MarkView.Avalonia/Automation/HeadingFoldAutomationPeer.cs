// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

using MarkView.Avalonia.Rendering.Blocks;

namespace MarkView.Avalonia.Automation;

/// <summary>
/// Exposes a foldable heading's fold button to screen readers as a button named by the heading's text,
/// which reports whether the section is expanded and can expand or collapse it.
/// </summary>
internal sealed class HeadingFoldAutomationPeer(HeadingFoldToggle owner) : ControlAutomationPeer(owner), IExpandCollapseProvider
{
    public new HeadingFoldToggle Owner => (HeadingFoldToggle)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string? GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? Owner.AccessibleName : name;
    }

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public ExpandCollapseState ExpandCollapseState =>
        Owner.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => false;

    public void Expand()
    {
        if (!Owner.IsExpanded) Owner.RaiseToggled();
    }

    public void Collapse()
    {
        if (Owner.IsExpanded) Owner.RaiseToggled();
    }

    internal void RaiseExpandCollapseStateChanged(bool wasExpanded, bool isExpanded) =>
        RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            wasExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            isExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
}
