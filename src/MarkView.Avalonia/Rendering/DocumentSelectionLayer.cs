// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A transparent overlay control that owns document-wide text selection.
/// Placed as a sibling of the markdown <see cref="StackPanel"/> in a single-cell
/// <see cref="Grid"/>, it receives coordinate-translated pointer events from
/// <see cref="MarkdownViewer"/> and paints selection highlights via <see cref="Render"/>.
/// </summary>
internal sealed class DocumentSelectionLayer : Control
{
    private readonly List<DocumentBlock> _blocks = new();

    // Anchor and focus in (blockIndex, charOffset) space.
    private (int BlockIdx, int Offset)? _anchor;
    private (int BlockIdx, int Offset)? _focus;

    public DocumentSelectionLayer()
    {
        IsHitTestVisible = false;
    }

    // ── Registration ─────────────────────────────────────────────────────────

    /// <summary>Registers a block in document order. Call after render, before user interaction.</summary>
    public void Register(DocumentBlock block) => _blocks.Add(block);

    /// <summary>Removes all registered blocks. Call before each re-render.</summary>
    public void ClearBlocks() => _blocks.Clear();

    /// <summary>Exposes registered blocks for hyperlink hit-testing from <see cref="MarkdownViewer"/>.</summary>
    internal IReadOnlyList<DocumentBlock> Blocks => _blocks;

    // ── Public selection API ──────────────────────────────────────────────────

    /// <summary>Selects all text in all registered blocks.</summary>
    public void SelectAll()
    {
        if (_blocks.Count == 0) return;
        _anchor = (0, 0);
        _focus = (_blocks.Count - 1, _blocks[^1].PlainText.Length);
        InvalidateVisual();
    }

    /// <summary>Clears selection.</summary>
    public void ClearSelection()
    {
        _anchor = null;
        _focus = null;
        InvalidateVisual();
    }

    /// <summary>Returns selected plain text, with newlines between blocks.</summary>
    public string GetSelectedText()
    {
        if (_anchor is null || _focus is null) return string.Empty;
        var (start, end) = Ordered(_anchor.Value, _focus.Value);

        if (start.BlockIdx == end.BlockIdx)
            return Slice(_blocks[start.BlockIdx].PlainText, start.Offset, end.Offset);

        var parts = new List<string>();
        parts.Add(Slice(_blocks[start.BlockIdx].PlainText, start.Offset, _blocks[start.BlockIdx].PlainText.Length));
        for (int i = start.BlockIdx + 1; i < end.BlockIdx; i++)
            parts.Add(_blocks[i].PlainText);
        parts.Add(Slice(_blocks[end.BlockIdx].PlainText, 0, end.Offset));
        return string.Join('\n', parts);
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
        var hit = HitTestBlocks(posInLayer);
        if (hit is null) return;
        _anchor = hit;
        _focus = hit;
    }

    /// <summary>
    /// Updates the focus end of selection during a drag.
    /// <paramref name="posInLayer"/> is the pointer position in this control's coordinate space.
    /// </summary>
    public void OnPointerMoved(Point posInLayer)
    {
        if (_anchor is null) return;
        var hit = HitTestBlocks(posInLayer);
        if (hit is null) return;
        _focus = hit;
        InvalidateVisual();
    }

    /// <summary>
    /// Performs a hit-test against all registered blocks.
    /// Returns (blockIndex, charOffset) of the closest hit, or null if nothing is hit.
    /// Uses <c>TranslatePoint</c> to convert <paramref name="posInLayer"/> to each block's local space.
    /// </summary>
    public (int BlockIdx, int Offset)? HitTestBlocks(Point posInLayer)
    {
        for (int i = 0; i < _blocks.Count; i++)
        {
            var tb = _blocks[i].TextBlock;
            var origin = tb.TranslatePoint(new Point(0, 0), this);
            if (origin is null) continue;

            var localPos = posInLayer - origin.Value;

            // Skip if clearly outside the block's bounds
            if (localPos.X < 0 || localPos.Y < 0 ||
                localPos.X > tb.Bounds.Width || localPos.Y > tb.Bounds.Height)
                continue;

            var textPos = new Point(localPos.X - tb.Padding.Left, localPos.Y - tb.Padding.Top);
            var hit = tb.TextLayout.HitTestPoint(textPos);
            return (i, hit.TextPosition);
        }
        return null;
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        if (_anchor is null || _focus is null) return;
        var brush = new SolidColorBrush(Colors.CornflowerBlue, 0.35);
        var (start, end) = Ordered(_anchor.Value, _focus.Value);

        for (int i = start.BlockIdx; i <= end.BlockIdx && i < _blocks.Count; i++)
        {
            var tb = _blocks[i].TextBlock;
            var origin = tb.TranslatePoint(new Point(0, 0), this);
            if (origin is null) continue;

            var textOrigin = origin.Value + new Vector(tb.Padding.Left, tb.Padding.Top);
            int charStart = (i == start.BlockIdx) ? start.Offset : 0;
            int charEnd   = (i == end.BlockIdx)   ? end.Offset   : _blocks[i].PlainText.Length;
            int length = charEnd - charStart;
            if (length <= 0) continue;

            foreach (var rect in tb.TextLayout.HitTestTextRange(charStart, length))
                context.DrawRectangle(brush, null, rect.Translate(textOrigin));
        }
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    internal void SetSelectionForTest(int blockIdx, int startOffset, int endOffset)
    {
        _anchor = (blockIdx, startOffset);
        _focus = (blockIdx, endOffset);
    }

    internal void SetSelectionForTest(int startBlock, int startOffset, int endBlock, int endOffset)
    {
        _anchor = (startBlock, startOffset);
        _focus = (endBlock, endOffset);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string Slice(string text, int start, int end)
    {
        start = Math.Clamp(start, 0, text.Length);
        end   = Math.Clamp(end, start, text.Length);
        return text[start..end];
    }

    private static ((int BlockIdx, int Offset) start, (int BlockIdx, int Offset) end)
        Ordered((int BlockIdx, int Offset) a, (int BlockIdx, int Offset) b)
    {
        if (a.BlockIdx < b.BlockIdx) return (a, b);
        if (a.BlockIdx > b.BlockIdx) return (b, a);
        return a.Offset <= b.Offset ? (a, b) : (b, a);
    }
}
