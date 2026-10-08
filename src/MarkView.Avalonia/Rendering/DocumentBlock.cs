// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Associates a selectable element in the rendered document with its plain text,
/// its absolute start offset in the flat document char space, and the separator that
/// follows this entry in reading order (tab between table cells, newline between blocks).
/// The element is either a <see cref="TextBlock"/> or a non-text block control that is
/// selected as a whole (see <see cref="ForBlockControl"/>).
/// Registered with <see cref="DocumentSelectionLayer"/> in document order.
/// </summary>
internal sealed class IndexEntry
{
    /// <summary>The control whose bounds the entry occupies.</summary>
    public Control Element { get; }

    /// <summary>The text block laying out <see cref="PlainText"/>; <c>null</c> for a block control.</summary>
    public TextBlock? TextBlock { get; }

    public string PlainText { get; }
    public string Separator { get; }

    /// <summary>
    /// Text copied in place of each embedded control (e.g. an image's alt text), keyed by its
    /// position in <see cref="PlainText"/>, which holds U+FFFC there to stay aligned with the
    /// <see cref="TextBlock.TextLayout"/> offsets. <c>null</c> when the entry embeds no control.
    /// </summary>
    public IReadOnlyDictionary<int, string>? EmbeddedText { get; }

    /// <summary>
    /// Absolute start offset in the document's flat char space.
    /// Stamped by <see cref="DocumentSelectionLayer.Register"/> at registration time.
    /// </summary>
    public int AbsStart { get; internal set; }

    public int AbsEnd => AbsStart + PlainText.Length;
    public int AbsEndWithSep => AbsEnd + Separator.Length;

    /// <summary>
    /// Bounding rect in the <see cref="DocumentSelectionLayer"/>'s coordinate space.
    /// Populated by <c>DocumentSelectionLayer.EnsureBounds()</c> and invalidated on layout.
    /// </summary>
    public Rect? CachedBounds { get; internal set; }

    public IndexEntry(TextBlock textBlock, string plainText, string separator = "\n",
        IReadOnlyDictionary<int, string>? embeddedText = null)
        : this(textBlock, textBlock, plainText, separator, embeddedText)
    {
    }

    private IndexEntry(Control element, TextBlock? textBlock, string plainText, string separator,
        IReadOnlyDictionary<int, string>? embeddedText)
    {
        Element = element;
        TextBlock = textBlock;
        PlainText = plainText;
        Separator = separator;
        EmbeddedText = embeddedText;
    }

    /// <summary>
    /// Creates an entry for a non-text block control: a single position, highlighted as a
    /// whole and copied as <paramref name="copyText"/>.
    /// </summary>
    public static IndexEntry ForBlockControl(Control control, string copyText, string separator = "\n") =>
        new(control, null, MarkdownSelectableTextBlock.ObjectReplacementCharacter.ToString(), separator,
            new Dictionary<int, string> { [0] = copyText });
}
