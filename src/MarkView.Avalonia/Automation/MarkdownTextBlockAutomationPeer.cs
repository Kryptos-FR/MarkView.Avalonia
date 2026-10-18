// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation.Peers;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Automation;

/// <summary>
/// A paragraph or heading, read as text, with each of its links as a hyperlink child after its
/// embedded controls (images, video embeds).
/// </summary>
internal sealed class MarkdownTextBlockAutomationPeer(MarkdownSelectableTextBlock owner) : TextBlockAutomationPeer(owner)
{
    public new MarkdownSelectableTextBlock Owner => (MarkdownSelectableTextBlock)base.Owner;

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
    {
        var children = base.GetChildrenCore();
        if (Owner.LinkTargets is not { } links) return children;

        var all = new List<AutomationPeer>(children ?? []);
        foreach (var link in links)
            all.Add(CreatePeerForElement(link));
        return all;
    }
}
