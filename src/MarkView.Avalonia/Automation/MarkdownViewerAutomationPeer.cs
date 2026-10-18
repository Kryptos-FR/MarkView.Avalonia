// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation.Peers;

namespace MarkView.Avalonia.Automation;

/// <summary>
/// Exposes a <see cref="MarkdownViewer"/> to screen readers as a document, named by
/// <c>AutomationProperties.Name</c> or, failing that, by the document's first heading.
/// </summary>
internal sealed class MarkdownViewerAutomationPeer(MarkdownViewer owner) : ControlAutomationPeer(owner)
{
    public new MarkdownViewer Owner => (MarkdownViewer)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

    protected override string? GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? Owner.FirstHeadingText : name;
    }

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    /// <summary>
    /// Tells assistive technology that the document changed as a whole (a streaming session completed),
    /// so it reads the viewer's children again.
    /// </summary>
    internal void NotifyDocumentChanged() => InvalidateChildren();
}
