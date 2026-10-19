// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

using MarkView.Avalonia.Rendering.Blocks;

namespace MarkView.Avalonia.Automation;

/// <summary>
/// Exposes the header of a foldable alert, <c>&lt;details&gt;</c> element or thinking block to screen readers
/// as a button named by the block's title, which reports whether the block is expanded and can expand or
/// collapse it.
/// </summary>
internal sealed class FoldHeaderAutomationPeer(FoldHeader owner) : ControlAutomationPeer(owner), IExpandCollapseProvider
{
    public new FoldHeader Owner => (FoldHeader)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string? GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? Owner.State!.Title : name;
    }

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public ExpandCollapseState ExpandCollapseState =>
        Owner.State!.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => false;

    public void Expand() => Owner.State!.IsExpanded = true;

    public void Collapse() => Owner.State!.IsExpanded = false;

    internal void RaiseExpandCollapseStateChanged(bool wasExpanded, bool isExpanded) =>
        RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            wasExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            isExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
}
