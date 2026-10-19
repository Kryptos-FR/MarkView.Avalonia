// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Interactivity;

namespace MarkView.Avalonia;

/// <summary>
/// Event args for <see cref="MarkdownViewer.SelectionChanged"/>. They carry no text: read
/// <see cref="MarkdownViewer.SelectedText"/> when it is needed.
/// </summary>
public sealed class MarkdownSelectionChangedEventArgs(MarkdownViewer source, bool isSelecting)
    : RoutedEventArgs(MarkdownViewer.SelectionChangedEvent, source)
{
    /// <summary>
    /// Whether the user is still dragging the end of the selection. When the drag ends, the event is raised
    /// once more with <c>false</c>, so a handler that only needs the final selection can skip the events
    /// where this is <c>true</c>.
    /// </summary>
    public bool IsSelecting { get; } = isSelecting;
}
