// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A match found by <see cref="DocumentSelectionLayer.Find"/>: <paramref name="Length"/> characters
/// from <paramref name="Start"/> in the <see cref="IndexEntry.PlainText"/> of <paramref name="Entry"/>,
/// an entry with a <see cref="IndexEntry.TextBlock"/>.
/// </summary>
internal readonly record struct FindMatch(IndexEntry Entry, int Start, int Length)
{
    /// <summary>
    /// Returns the area the match covers in its text block's coordinate space, from its first character to
    /// its last on every line it is laid out on; <c>null</c> when the text block has not been laid out.
    /// </summary>
    public Rect? GetBounds()
    {
        var textBlock = Entry.TextBlock!;
        Rect? bounds = null;
        foreach (var rect in textBlock.TextLayout.HitTestTextRange(Start, Length))
            bounds = bounds?.Union(rect) ?? rect;
        return bounds?.Translate(new Vector(textBlock.Padding.Left, textBlock.Padding.Top));
    }
}
