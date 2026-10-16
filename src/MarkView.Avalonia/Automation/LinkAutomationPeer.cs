// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Automation;

/// <summary>
/// Exposes a link to screen readers as a hyperlink that can be invoked, named by its text and
/// reported as a child of the paragraph it is in.
/// </summary>
internal sealed class LinkAutomationPeer(LinkFocusTarget owner) : ControlAutomationPeer(owner), IInvokeProvider
{
    public new LinkFocusTarget Owner => (LinkFocusTarget)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

    protected override string? GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? Owner.AccessibleName : name;
    }

    protected override string? GetHelpTextCore()
    {
        var helpText = base.GetHelpTextCore();
        return string.IsNullOrWhiteSpace(helpText) ? Owner.Link.Title : helpText;
    }

    // The target lives in the focus layer, but belongs to its paragraph, whose peer lists it as a child.
    protected override AutomationPeer? GetParentCore() => CreatePeerForElement(Owner.TextBlock);

    public void Invoke() => Owner.Activate();
}
