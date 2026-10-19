// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>
    /// Scrolls the document for a key nothing else handled, pressed while the viewer itself has the focus, as it has
    /// after a click in the document, or a link or a control of the document has it, and returns whether it did.
    /// <c>PART_ScrollViewer</c> only handles Page Up and Page Down, and only for a key pressed inside it: never for
    /// one pressed on the viewer, its parent.
    /// The keys are a browser's: the arrows scroll by a line, Page Up and Page Down by a page, Space and Shift+Space
    /// too, and Home and End (with or without Ctrl) to the top and the bottom. A key for a direction the document
    /// cannot scroll in, e.g. any of them on a document shorter than the viewer, is left to the viewer's parents.
    /// </summary>
    private bool HandleScrollKey(KeyEventArgs e)
    {
        if (_scrollViewer is not { } scrollViewer
            || e.Source is not Visual source || (source != this && !scrollViewer.IsVisualAncestorOf(source)))
            return false;

        var vertical = scrollViewer.Extent.Height > scrollViewer.Viewport.Height;
        var horizontal = scrollViewer.Extent.Width > scrollViewer.Viewport.Width;
        Action? scroll = (e.Key, e.KeyModifiers) switch
        {
            (Key.Up, KeyModifiers.None) when vertical => scrollViewer.LineUp,
            (Key.Down, KeyModifiers.None) when vertical => scrollViewer.LineDown,
            (Key.Left, KeyModifiers.None) when horizontal => scrollViewer.LineLeft,
            (Key.Right, KeyModifiers.None) when horizontal => scrollViewer.LineRight,
            (Key.PageUp, KeyModifiers.None) or (Key.Space, KeyModifiers.Shift) when vertical => scrollViewer.PageUp,
            (Key.PageDown, KeyModifiers.None) or (Key.Space, KeyModifiers.None) when vertical => scrollViewer.PageDown,
            (Key.Home, KeyModifiers.None or KeyModifiers.Control) when vertical => scrollViewer.ScrollToHome,
            (Key.End, KeyModifiers.None or KeyModifiers.Control) when vertical => scrollViewer.ScrollToEnd,
            _ => null,
        };
        scroll?.Invoke();
        return scroll is not null;
    }
}
