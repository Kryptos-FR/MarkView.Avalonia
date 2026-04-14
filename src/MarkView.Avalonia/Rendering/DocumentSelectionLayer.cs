// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A transparent overlay control that owns document-wide text selection.
/// Placed as a sibling of the markdown <see cref="StackPanel"/> in a single-cell
/// <see cref="Grid"/>, it receives coordinate-translated pointer events from
/// <see cref="MarkdownViewer"/> and paints selection highlights via <see cref="Render"/>.
/// Selection is expressed as two absolute char offsets in the flat document char space.
/// </summary>
internal sealed class DocumentSelectionLayer : Control
{
    private static readonly ImmutableSolidColorBrush SelectionBrush =
        new(Colors.CornflowerBlue, 0.35);

    private readonly List<IndexEntry> _entries = new();
    private int _totalLength;

    // Anchor and focus as absolute char offsets.
    private int? _anchor;
    private int? _focus;

    public DocumentSelectionLayer()
    {
        IsHitTestVisible = false;
    }

    // ── Registration ─────────────────────────────────────────────────────────

    /// <summary>
    /// Registers an entry in document order.
    /// Stamps <paramref name="entry"/>.AbsStart and advances the running offset.
    /// Call after render, before user interaction.
    /// </summary>
    public void Register(IndexEntry entry)
    {
        entry.AbsStart = _totalLength;
        _entries.Add(entry);
        _totalLength += entry.PlainText.Length + entry.Separator.Length;
    }

    // ── Public selection API ──────────────────────────────────────────────────

    /// <summary>Selects all text in all registered entries.</summary>
    public void SelectAll()
    {
        if (_entries.Count == 0) return;
        _anchor = 0;
        _focus = _entries[^1].AbsEnd;   // up to (not including) trailing separator
        InvalidateVisual();
    }

    /// <summary>Clears selection.</summary>
    public void ClearSelection()
    {
        _anchor = null;
        _focus = null;
        InvalidateVisual();
    }

    /// <summary>Returns selected plain text in reading order with separators between entries.</summary>
    public string GetSelectedText()
    {
        if (_anchor is null || _focus is null) return string.Empty;
        int selStart = Math.Min(_anchor.Value, _focus.Value);
        int selEnd = Math.Max(_anchor.Value, _focus.Value);
        if (selStart == selEnd) return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var entry in _entries)
        {
            if (entry.AbsEndWithSep <= selStart) continue;  // entirely before selection (including separator)
            if (entry.AbsStart >= selEnd) break;              // entirely after selection

            int localStart = Math.Max(0, selStart - entry.AbsStart);
            int localEnd = Math.Min(entry.PlainText.Length, selEnd - entry.AbsStart);
            if (localEnd > localStart)
                sb.Append(entry.PlainText.AsSpan(localStart, localEnd - localStart));

            // Append separator if selection extends into or past the separator gap
            if (selEnd > entry.AbsEnd && entry.Separator.Length > 0)
                sb.Append(entry.Separator);
        }
        return sb.ToString();
    }

    /// <summary>Copies selected text to the clipboard.</summary>
    public async Task CopyToClipboardAsync(TopLevel topLevel)
    {
        var text = GetSelectedText();
        if (!string.IsNullOrEmpty(text) && topLevel.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    // ── Pointer event integration ─────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="MarkdownViewer"/> when a left-button press is detected.
    /// <paramref name="posInLayer"/> is the pointer position in this control's coordinate space.
    /// Clears previous selection and records the new anchor.
    /// </summary>
    public void OnPointerPressed(Point posInLayer)
    {
        ClearSelection();
        var offset = HitTestOffset(posInLayer);
        if (offset is null) return;
        _anchor = offset;
        _focus = offset;
    }

    /// <summary>
    /// Updates the focus end of selection during a drag.
    /// <paramref name="posInLayer"/> is the pointer position in this control's coordinate space.
    /// </summary>
    public void OnPointerMoved(Point posInLayer)
    {
        if (_anchor is null) return;
        var offset = HitTestOffset(posInLayer);
        if (offset is null) return;
        _focus = offset;
        InvalidateVisual();
    }

    // ── Hit testing ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the absolute char offset at <paramref name="posInLayer"/>, or null if no entry
    /// was hit. Used for selection anchor/focus.
    /// </summary>
    public int? HitTestOffset(Point posInLayer)
    {
        foreach (var entry in _entries)
        {
            var localPos = ToLocalPos(entry.TextBlock, posInLayer);
            if (localPos is null) continue;

            var textPos = new Point(localPos.Value.X - entry.TextBlock.Padding.Left,
                                    localPos.Value.Y - entry.TextBlock.Padding.Top);
            var hit = entry.TextBlock.TextLayout.HitTestPoint(textPos);
            return entry.AbsStart + hit.TextPosition;
        }
        return null;
    }

    /// <summary>
    /// Returns the <see cref="IndexEntry"/> whose TextBlock bounds contain
    /// <paramref name="posInLayer"/>, or null. Used for hyperlink hit-testing.
    /// </summary>
    public IndexEntry? HitTestEntry(Point posInLayer)
    {
        foreach (var entry in _entries)
        {
            if (ToLocalPos(entry.TextBlock, posInLayer) is not null)
                return entry;
        }
        return null;
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        if (_anchor is null || _focus is null) return;
        int selStart = Math.Min(_anchor.Value, _focus.Value);
        int selEnd = Math.Max(_anchor.Value, _focus.Value);
        if (selStart == selEnd) return;

        foreach (var entry in _entries)
        {
            if (entry.AbsEnd <= selStart) continue;
            if (entry.AbsStart >= selEnd) break;

            var origin = entry.TextBlock.TranslatePoint(new Point(0, 0), this);
            if (origin is null) continue;

            var textOrigin = origin.Value + new Vector(entry.TextBlock.Padding.Left,
                                                         entry.TextBlock.Padding.Top);
            int localStart = Math.Max(0, selStart - entry.AbsStart);
            int localEnd = Math.Min(entry.PlainText.Length, selEnd - entry.AbsStart);
            int length = localEnd - localStart;
            if (length <= 0) continue;

            foreach (var rect in entry.TextBlock.TextLayout.HitTestTextRange(localStart, length))
                context.DrawRectangle(SelectionBrush, null, rect.Translate(textOrigin));
        }
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>Directly sets anchor and focus as absolute offsets. For unit tests only.</summary>
    internal void SetSelectionForTest(int start, int end)
    {
        _anchor = start;
        _focus = end;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns <paramref name="posInLayer"/> in the TextBlock's local coordinate space,
    /// or null if the point is outside the TextBlock's bounds.
    /// </summary>
    private Point? ToLocalPos(TextBlock tb, Point posInLayer)
    {
        var origin = tb.TranslatePoint(new Point(0, 0), this);
        if (origin is null) return null;

        var local = posInLayer - origin.Value;
        if (local.X < 0 || local.Y < 0 ||
            local.X > tb.Bounds.Width || local.Y > tb.Bounds.Height)
            return null;

        return local;
    }
}
