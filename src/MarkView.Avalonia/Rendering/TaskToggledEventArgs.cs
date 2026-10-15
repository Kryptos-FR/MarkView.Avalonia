// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Interactivity;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Event args for a task-list checkbox toggled by the user. Carries the new state and where the
/// item's <c>[ ]</c> / <c>[x]</c> marker sits in the markdown source, so the host can write the
/// change back.
/// </summary>
/// <remarks>
/// Raised through <see cref="MarkdownViewer.TaskToggled"/>, positions refer to the markdown text
/// the viewer rendered: the <see cref="MarkdownViewer.Markdown"/> string as set, or the document
/// loaded from <see cref="MarkdownViewer.Source"/>. Raised through
/// <see cref="AvaloniaRenderer.TaskToggled"/>, they refer to the text the rendered document was
/// parsed from.
/// </remarks>
public sealed class TaskToggledEventArgs(bool isChecked, int sourceStart, int sourceLength, int line) : RoutedEventArgs
{
    /// <summary>
    /// The item's new state: <c>true</c> when it is now checked.
    /// </summary>
    public bool IsChecked { get; } = isChecked;

    /// <summary>
    /// The zero-based index of the marker's opening <c>[</c> in the markdown source.
    /// </summary>
    public int SourceStart { get; } = sourceStart;

    /// <summary>
    /// The length of the marker in the markdown source (<c>3</c> for <c>[ ]</c>, <c>[x]</c> or <c>[X]</c>).
    /// </summary>
    public int SourceLength { get; } = sourceLength;

    /// <summary>
    /// The zero-based line of the markdown source the marker is on.
    /// </summary>
    public int Line { get; } = line;
}
