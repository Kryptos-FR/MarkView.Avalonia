// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Associates a <see cref="TextBlock"/> in the rendered document with its plain text,
/// its absolute start offset in the flat document char space, and the separator that
/// follows this entry in reading order (tab between table cells, newline between blocks).
/// Registered with <see cref="DocumentSelectionLayer"/> in document order.
/// </summary>
internal sealed class IndexEntry
{
    public TextBlock TextBlock { get; }
    public string PlainText { get; }
    public string Separator { get; }

    /// <summary>
    /// Absolute start offset in the document's flat char space.
    /// Stamped by <see cref="DocumentSelectionLayer.Register"/> at registration time.
    /// </summary>
    public int AbsStart { get; internal set; }

    public int AbsEnd => AbsStart + PlainText.Length;
    public int AbsEndWithSep => AbsEnd + Separator.Length;

    public IndexEntry(TextBlock textBlock, string plainText, string separator = "\n")
    {
        TextBlock = textBlock;
        PlainText = plainText;
        Separator = separator;
    }
}
