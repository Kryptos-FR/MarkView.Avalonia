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

    // null for a list marker's entry, which reads its text block's text instead.
    private readonly string? _plainText;

    /// <summary>
    /// The entry's text. A list marker's entry (see <see cref="ForMarker"/>) reads its text block's
    /// current text, so a toggled task-list glyph is copied in its new state.
    /// </summary>
    public string PlainText => _plainText ?? TextBlock!.Text!;
    public string Separator { get; }

    /// <summary>
    /// The top-level block of the document the entry renders part of; <c>null</c> when it is not known.
    /// Set by <see cref="MarkdownViewer.RegisterBlocks(DocumentSelectionLayer, Avalonia.Controls.Panel, int)"/>.
    /// </summary>
    public Markdig.Syntax.Block? TopLevelBlock { get; set; }

    /// <summary>Whether the entry is a list item's marker (see <see cref="ForMarker"/>).</summary>
    public bool IsMarker => _plainText is null;

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

    // The bounds and clip last cached, and the layer and its layout pass they were cached in.
    private Rect _bounds;
    private Rect _clip;
    private DocumentSelectionLayer? _boundsLayer;
    private int _boundsLayout;

    private bool HasCurrentBounds => _boundsLayer is { } layer && _boundsLayout == layer.LayoutCount;

    /// <summary>
    /// Bounding rect in the <see cref="DocumentSelectionLayer"/>'s coordinate space.
    /// Populated by <c>DocumentSelectionLayer.EnsureBounds()</c>; <c>null</c> once the layer has been laid out again.
    /// </summary>
    public Rect? CachedBounds => HasCurrentBounds ? _bounds : null;

    /// <summary>
    /// The visible part of <see cref="CachedBounds"/>: what is left once the ancestors inside the
    /// document that clip their content (e.g. the scroll viewer of a code block with a maximum
    /// height) have cut it. Equal to <see cref="CachedBounds"/> when nothing clips the entry.
    /// Populated and invalidated together with it.
    /// </summary>
    public Rect? CachedClip => HasCurrentBounds ? _clip : null;

    /// <summary>Caches <see cref="CachedBounds"/> and <see cref="CachedClip"/> until the next layout pass of <paramref name="layer"/>.</summary>
    internal void CacheBounds(DocumentSelectionLayer layer, Rect bounds, Rect clip)
    {
        _bounds = bounds;
        _clip = clip;
        _boundsLayer = layer;
        _boundsLayout = layer.LayoutCount;
    }

    /// <summary>
    /// The index, among the children of the layer's <see cref="DocumentSelectionLayer.Document"/>, of the
    /// top-level control the entry is part of; <c>-1</c> until the layer has found it.
    /// </summary>
    internal int DocumentIndex { get; set; } = -1;

    public IndexEntry(TextBlock textBlock, string plainText, string separator = "\n",
        IReadOnlyDictionary<int, string>? embeddedText = null)
        : this(textBlock, textBlock, plainText, separator, embeddedText)
    {
    }

    private IndexEntry(Control element, TextBlock? textBlock, string? plainText, string separator,
        IReadOnlyDictionary<int, string>? embeddedText)
    {
        Element = element;
        TextBlock = textBlock;
        _plainText = plainText;
        Separator = separator;
        EmbeddedText = embeddedText;
    }

    /// <summary>
    /// Creates an entry for a list item's marker (bullet, number or task-list check glyph), followed by
    /// a space, whose text is read from <paramref name="marker"/> when needed. The text may change but
    /// must keep its length, since the offsets of later entries were stamped from it at registration.
    /// </summary>
    public static IndexEntry ForMarker(TextBlock marker) => new(marker, marker, null, " ", null);

    /// <summary>
    /// Creates an entry for a non-text block control: a single position, highlighted as a
    /// whole and copied as <paramref name="copyText"/>.
    /// </summary>
    public static IndexEntry ForBlockControl(Control control, string copyText, string separator = "\n") =>
        new(control, null, MarkdownSelectableTextBlock.ObjectReplacementCharacter.ToString(), separator,
            new Dictionary<int, string> { [0] = copyText });
}
