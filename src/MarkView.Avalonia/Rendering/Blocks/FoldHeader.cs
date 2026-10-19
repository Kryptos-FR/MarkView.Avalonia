// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation.Peers;

using MarkView.Avalonia.Automation;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The header that folds a foldable alert, a <c>&lt;details&gt;</c> element or a thinking block (see
/// <see cref="CollapsibleAlert"/>). Screen readers see it as a button that expands or collapses the block,
/// like a foldable heading's fold button.
/// </summary>
internal sealed class FoldHeader : StyledInlinesTextBlock
{
    /// <summary>The fold state this header toggles, set by <see cref="CollapsibleAlert.Attach"/>.</summary>
    internal CollapsibleAlert? State { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new FoldHeaderAutomationPeer(this);
}
