// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using MarkView.Avalonia.Automation;

namespace MarkView.Avalonia;

public partial class MarkdownViewer : ICustomKeyboardNavigation
{
    /// <summary>The text of the document's first heading, if it has one.</summary>
    internal string? FirstHeadingText => _headingEntries.Count > 0 ? _headingEntries[0].Text : null;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new MarkdownViewerAutomationPeer(this);

    /// <summary>
    /// Tab order through the document: the viewer itself, then its links and focusable controls in
    /// reading order, then whatever follows the viewer. Shift+Tab walks the same stops backwards,
    /// and entering the document backwards lands on its last stop.
    /// </summary>
    (bool handled, IInputElement? next) ICustomKeyboardNavigation.GetNext(IInputElement element, NavigationDirection direction)
    {
        if (_linkFocusLayer is null || direction is not (NavigationDirection.Next or NavigationDirection.Previous))
            return (false, null);

        var stops = _linkFocusLayer.GetActiveStops();
        var forward = direction == NavigationDirection.Next;
        if (element == this)
            return forward && stops.Count > 0 ? (true, stops[0]) : (false, null);

        var index = stops.IndexOf(element);
        if (index < 0)
        {
            // Default navigation from outside the viewer just landed on it, going backwards.
            var fromOutside = element is not Visual visual || !this.IsVisualAncestorOf(visual);
            return !forward && fromOutside && stops.Count > 0 ? (true, stops[^1]) : (false, null);
        }

        if (forward)
            // Past the last stop, (true, null) continues default navigation after the viewer.
            return (true, index + 1 < stops.Count ? stops[index + 1] : null);
        return (true, index > 0 ? stops[index - 1] : this);
    }
}
