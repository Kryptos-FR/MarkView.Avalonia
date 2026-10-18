// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;

using MarkView.Avalonia.Automation;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The header of a foldable block (see <see cref="CollapsibleAlert"/>): a foldable alert's title, a details
/// element's summary or a thinking block's header. Styled as a plain <c>TextBlock</c>; screen readers see a
/// button, named by its block's title, that reports whether the block is expanded and expands or collapses it.
/// </summary>
internal sealed class FoldHeader : StyledInlinesTextBlock, IFoldButton
{
    /// <summary>The fold state this header toggles, set by <see cref="CollapsibleAlert.Attach"/>.</summary>
    internal CollapsibleAlert? Fold { get; set; }

    public bool IsExpanded => Fold?.IsExpanded ?? true;

    /// <summary>The title of the block, as its group is named.</summary>
    public string AccessibleName => Fold is { } fold ? AutomationProperties.GetName(fold.Border) ?? string.Empty : string.Empty;

    public void Toggle() => Fold?.Toggle();

    protected override AutomationPeer OnCreateAutomationPeer() => new FoldButtonAutomationPeer(this);
}
