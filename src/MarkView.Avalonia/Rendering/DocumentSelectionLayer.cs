// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A transparent overlay control that owns document-wide text selection.
/// Placed as a sibling of the markdown <see cref="StackPanel"/> in a single-cell
/// <see cref="Grid"/>, it receives coordinate-translated pointer events from
/// <see cref="MarkdownViewer"/> and paints selection highlights via <see cref="Render"/>.
/// Selection is expressed as two absolute char offsets in the flat document char space.
/// The layer also finds text in its entries and paints the matches under the selection.
/// </summary>
internal sealed class DocumentSelectionLayer : Control
{
    private static readonly ImmutableSolidColorBrush SelectionBrush =
        new(Colors.CornflowerBlue, 0.35);

    // Used when the theme does not define MarkdownFindMatchBackground / MarkdownFindCurrentMatchBackground,
    // with the same values as MarkdownTheme.axaml.
    private static readonly ImmutableSolidColorBrush LightFindMatchBrush = new(Color.FromRgb(0xFF, 0xF1, 0x76));
    private static readonly ImmutableSolidColorBrush LightCurrentFindMatchBrush = new(Color.FromRgb(0xFF, 0xC5, 0x84));
    private static readonly ImmutableSolidColorBrush DarkFindMatchBrush = new(Color.FromRgb(0x45, 0x3A, 0x00));
    private static readonly ImmutableSolidColorBrush DarkCurrentFindMatchBrush = new(Color.FromRgb(0x65, 0x2A, 0x00));

    /// <summary>Defines the <see cref="FindMatchBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FindMatchBrushProperty =
        AvaloniaProperty.Register<DocumentSelectionLayer, IBrush?>(nameof(FindMatchBrush));

    /// <summary>Defines the <see cref="CurrentFindMatchBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CurrentFindMatchBrushProperty =
        AvaloniaProperty.Register<DocumentSelectionLayer, IBrush?>(nameof(CurrentFindMatchBrush));

    private readonly List<IndexEntry> _entries = [];
    private int _totalLength;

    // Anchor and focus as absolute char offsets.
    private int? _anchor;
    private int? _focus;

    // The range last reported through SelectionChanged; null when it was empty.
    private (int Start, int End)? _reportedRange;

    private readonly List<FindMatch> _findMatches = [];

    static DocumentSelectionLayer()
    {
        AffectsRender<DocumentSelectionLayer>(FindMatchBrushProperty, CurrentFindMatchBrushProperty);
    }

    public DocumentSelectionLayer()
    {
        IsHitTestVisible = false;
        Bind(FindMatchBrushProperty, this.GetResourceObservable("MarkdownFindMatchBackground"));
        Bind(CurrentFindMatchBrushProperty, this.GetResourceObservable("MarkdownFindCurrentMatchBackground"));
    }

    /// <summary>
    /// The background of every find match but the current one, from the <c>MarkdownFindMatchBackground</c>
    /// resource; <c>null</c> when no such resource is found, a built-in colour is then used.
    /// </summary>
    public IBrush? FindMatchBrush
    {
        get => GetValue(FindMatchBrushProperty);
        set => SetValue(FindMatchBrushProperty, value);
    }

    /// <summary>
    /// The background of the current find match, from the <c>MarkdownFindCurrentMatchBackground</c>
    /// resource; <c>null</c> when no such resource is found, a built-in colour is then used.
    /// </summary>
    public IBrush? CurrentFindMatchBrush
    {
        get => GetValue(CurrentFindMatchBrushProperty);
        set => SetValue(CurrentFindMatchBrushProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // Invalidate cached bounds so they're recomputed on the next pointer event.
        foreach (var entry in _entries)
            entry.CachedBounds = null;

        // Text can move without the layer itself being laid out again, as when a code block with a
        // maximum height scrolls inside itself, so the highlight is drawn again where it now is.
        // Find matches move the same way, and with the text when it wraps differently.
        // Stryker disable once Equality,Logical : drawing again without any match draws the same nothing
        if (_anchor is not null || _findMatches.Count > 0)
            InvalidateVisual();
    }

    /// <summary>
    /// Raised when the selected range changes: each drag step that moves its end, <see cref="SelectAll"/>,
    /// <see cref="ClearSelection"/>, a press that clears it, a <see cref="TruncateTo"/> that shortens or
    /// clears it. Not raised when the range stays the same.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Whether a non-empty range is selected.</summary>
    public bool HasSelection => SelectedRange is not null;

    /// <summary>The selected range in reading order, <c>null</c> when it is empty.</summary>
    private (int Start, int End)? SelectedRange =>
        _anchor is { } anchor && _focus is { } focus && anchor != focus
            ? (Math.Min(anchor, focus), Math.Max(anchor, focus))
            : null;

    /// <summary>Raises <see cref="SelectionChanged"/> when the selected range differs from the one last reported.</summary>
    private void ReportSelectionChange()
    {
        var range = SelectedRange;
        if (range == _reportedRange) return;
        _reportedRange = range;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>The number of registered entries.</summary>
    public int EntryCount => _entries.Count;

    /// <summary>
    /// Removes every entry after the first <paramref name="entryCount"/>, so later registrations continue
    /// from the end of the last kept entry. A selection starting beyond the last kept entry's text is cleared,
    /// one ending beyond it ends at the end of that text (a selection starting exactly there keeps its
    /// anchor, as a drag in progress does), and the find matches in removed entries
    /// are dropped, along with the current match when it was one of them.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="entryCount"/> is negative or greater than <see cref="EntryCount"/>.
    /// </exception>
    public void TruncateTo(int entryCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(entryCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(entryCount, _entries.Count);

        var removed = _entries.GetRange(entryCount, _entries.Count - entryCount);
        _entries.RemoveRange(entryCount, removed.Count);
        _totalLength = entryCount == 0 ? 0 : _entries[^1].AbsEndWithSep;

        if (_anchor is { } anchor && _focus is { } focus)
        {
            var keptEnd = entryCount == 0 ? 0 : _entries[^1].AbsEnd;
            if (Math.Min(anchor, focus) > keptEnd)
            {
                _anchor = null;
                _focus = null;
            }
            else
            {
                _anchor = Math.Min(anchor, keptEnd);
                _focus = Math.Min(focus, keptEnd);
            }
        }

        // Matches are in entry order, so those in removed entries come last.
        var removedEntries = new HashSet<IndexEntry>(removed);
        var firstRemoved = _findMatches.Count;
        while (firstRemoved > 0 && removedEntries.Contains(_findMatches[firstRemoved - 1].Entry))
            firstRemoved--;
        _findMatches.RemoveRange(firstRemoved, _findMatches.Count - firstRemoved);
        if (CurrentFindMatch >= _findMatches.Count)
            CurrentFindMatch = -1;

        InvalidateVisual();
        ReportSelectionChange();
    }

    // ── Public selection API ──────────────────────────────────────────────────

    /// <summary>Selects all text in all registered entries.</summary>
    public void SelectAll()
    {
        if (_entries.Count == 0) return;
        _anchor = 0;
        _focus = _entries[^1].AbsEnd;   // up to (not including) trailing separator
        InvalidateVisual();
        ReportSelectionChange();
    }

    /// <summary>Clears selection.</summary>
    public void ClearSelection()
    {
        _anchor = null;
        _focus = null;
        InvalidateVisual();
        ReportSelectionChange();
    }

    /// <summary>
    /// Returns selected plain text in reading order with separators between entries. Lines are
    /// separated by <c>'\n'</c>, whatever line endings the text and copy texts it is made of use.
    /// </summary>
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
            if (IsHidden(entry)) continue;                    // e.g. the body of a collapsed alert

            int localStart = Math.Max(0, selStart - entry.AbsStart);
            int localEnd = Math.Min(entry.PlainText.Length, selEnd - entry.AbsStart);
            if (localEnd > localStart)
                AppendText(sb, entry, localStart, localEnd);

            // Append separator if selection extends into or past the separator gap
            if (selEnd > entry.AbsEnd && entry.Separator.Length > 0)
                sb.Append(entry.Separator);
        }
        // Code block lines are written with the platform's line ending (LineBreak inlines), and a
        // copy text keeps whatever its renderer gave it.
        return sb.ToString().ReplaceLineEndings("\n");
    }

    /// <summary>
    /// Appends <paramref name="entry"/>'s text between the local offsets, substituting each
    /// embedded control with its copy text.
    /// </summary>
    private static void AppendText(System.Text.StringBuilder sb, IndexEntry entry, int start, int end)
    {
        if (entry.EmbeddedText is not { } embedded)
        {
            sb.Append(entry.PlainText.AsSpan(start, end - start));
            return;
        }

        for (int i = start; i < end; i++)
        {
            if (embedded.TryGetValue(i, out var text)) sb.Append(text);
            else sb.Append(entry.PlainText[i]);
        }
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
        ReportSelectionChange();
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
            // A hidden entry keeps the bounds of its last layout, which may lie anywhere.
            if (IsHidden(entry)) continue;

            // Entries are in document order (top to bottom). Once cached bounds show
            // we've passed the pointer's Y, no further entry can match.
            if (entry.CachedBounds is { } b && b.Top > posInLayer.Y)
                break;

            var localPos = ToLocalPos(entry, posInLayer);
            if (localPos is null) continue;

            // A block control is a single position: its top half is before it, its bottom half after.
            if (entry.TextBlock is not { } textBlock)
                // Stryker disable once Equality : which side the exact middle falls on is arbitrary
                return entry.AbsStart + (localPos.Value.Y < entry.Element.Bounds.Height / 2 ? 0 : 1);

            var textPos = new Point(localPos.Value.X - textBlock.Padding.Left,
                                    localPos.Value.Y - textBlock.Padding.Top);
            var hit = textBlock.TextLayout.HitTestPoint(textPos);
            return entry.AbsStart + hit.TextPosition;
        }
        return null;
    }

    /// <summary>
    /// Returns the <see cref="IndexEntry"/> whose element bounds contain
    /// <paramref name="posInLayer"/>, or null. Used for hyperlink hit-testing.
    /// </summary>
    public IndexEntry? HitTestEntry(Point posInLayer)
    {
        foreach (var entry in _entries)
        {
            if (IsHidden(entry)) continue;

            if (entry.CachedBounds is { } b && b.Top > posInLayer.Y)
                break;

            if (ToLocalPos(entry, posInLayer) is not null)
                return entry;
        }
        return null;
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        // Matches first, so a selection over a match still shows.
        RenderFindMatches(context);
        RenderSelection(context);
    }

    private void RenderSelection(DrawingContext context)
    {
        if (_anchor is null || _focus is null) return;
        int selStart = Math.Min(_anchor.Value, _focus.Value);
        int selEnd = Math.Max(_anchor.Value, _focus.Value);
        if (selStart == selEnd) return;

        foreach (var entry in _entries)
        {
            if (entry.AbsEnd <= selStart) continue;
            if (entry.AbsStart >= selEnd) break;
            if (IsHidden(entry)) continue;

            if (!EnsureBounds(entry)) continue;
            var bounds = entry.CachedBounds!.Value;

            // Only the visible part of an entry is highlighted, e.g. not the lines of a code block
            // scrolled out of its maximum height, which would otherwise paint over its neighbours.
            using var clip = entry.CachedClip != bounds
                ? context.PushClip(entry.CachedClip!.Value)
                : (DrawingContext.PushedState?)null;

            if (entry.TextBlock is not { } textBlock)
            {
                context.DrawRectangle(SelectionBrush, null, bounds);
                continue;
            }

            var textOrigin = bounds.TopLeft + new Vector(textBlock.Padding.Left, textBlock.Padding.Top);
            int localStart = Math.Max(0, selStart - entry.AbsStart);
            int localEnd = Math.Min(entry.PlainText.Length, selEnd - entry.AbsStart);
            int length = localEnd - localStart;
            if (length <= 0) continue;

            foreach (var rect in textBlock.TextLayout.HitTestTextRange(localStart, length))
                context.DrawRectangle(SelectionBrush, null, rect.Translate(textOrigin));
        }
    }

    private void RenderFindMatches(DrawingContext context)
    {
        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var matchBrush = FindMatchBrush ?? (isDark ? DarkFindMatchBrush : LightFindMatchBrush);
        var currentBrush = CurrentFindMatchBrush ?? (isDark ? DarkCurrentFindMatchBrush : LightCurrentFindMatchBrush);

        for (int i = 0; i < _findMatches.Count; i++)
        {
            var (entry, start, length) = _findMatches[i];
            // A match in hidden content (e.g. the body of a collapsed alert) is counted, not drawn.
            if (IsHidden(entry) || !EnsureBounds(entry)) continue;
            var bounds = entry.CachedBounds!.Value;

            using var clip = entry.CachedClip != bounds
                ? context.PushClip(entry.CachedClip!.Value)
                : (DrawingContext.PushedState?)null;

            var textBlock = entry.TextBlock!;
            var textOrigin = bounds.TopLeft + new Vector(textBlock.Padding.Left, textBlock.Padding.Top);
            var brush = i == CurrentFindMatch ? currentBrush : matchBrush;
            foreach (var rect in textBlock.TextLayout.HitTestTextRange(start, length))
            {
                var highlight = rect.Translate(textOrigin);
                context.DrawRectangle(brush, null, highlight);

                // The highlight is opaque, so the text it covers is drawn again on top of it, in its own colours.
                using (context.PushClip(highlight))
                    DrawLines(context, textBlock.TextLayout, textOrigin, highlight);
            }
        }
    }

    /// <summary>
    /// Draws the lines of <paramref name="layout"/> that cross <paramref name="area"/>, where
    /// <see cref="TextLayout.Draw"/> would draw them from <paramref name="origin"/>.
    /// </summary>
    private static void DrawLines(DrawingContext context, TextLayout layout, Point origin, Rect area)
    {
        var y = origin.Y;
        foreach (var line in layout.TextLines)
        {
            if (y < area.Bottom && y + line.Height > area.Top)
                line.Draw(context, new Point(origin.X, y));
            y += line.Height;
        }
    }

    // ── Find ──────────────────────────────────────────────────────────────────

    /// <summary>The matches of the last <see cref="Find"/>, in document order.</summary>
    public IReadOnlyList<FindMatch> FindMatches => _findMatches;

    /// <summary>
    /// The index in <see cref="FindMatches"/> of the current match, drawn with
    /// <see cref="CurrentFindMatchBrush"/>; -1 when there is none.
    /// </summary>
    public int CurrentFindMatch
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    } = -1;

    /// <summary>
    /// Finds every occurrence of <paramref name="text"/>, which must not be empty, in the registered
    /// text, replacing the previous matches, and leaves no current match. Returns the number of matches.
    /// A match never spans two entries. Neither a block control nor a list item's marker is searched, and
    /// an embedded control is a U+FFFC placeholder in its entry's text, so copy texts are never matched.
    /// Hidden entries (e.g. the body of a collapsed alert) are searched like the others.
    /// </summary>
    public int Find(string text, MarkdownFindOptions options)
    {
        _findMatches.Clear();
        var comparison = options.HasFlag(MarkdownFindOptions.MatchCase)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var wholeWord = options.HasFlag(MarkdownFindOptions.WholeWord);

        foreach (var entry in _entries)
        {
            if (entry.TextBlock is null || entry.IsMarker) continue;

            var plainText = entry.PlainText;
            var start = plainText.IndexOf(text, comparison);
            while (start >= 0)
            {
                var end = start + text.Length;
                if (wholeWord && !IsWholeWord(plainText, start, end))
                {
                    // A later occurrence may overlap this one, e.g. "a-a" at the end of "xa-a-a".
                    start = plainText.IndexOf(text, start + 1, comparison);
                    continue;
                }
                _findMatches.Add(new FindMatch(entry, start, text.Length));
                start = plainText.IndexOf(text, end, comparison);
            }
        }

        CurrentFindMatch = -1;
        return _findMatches.Count;
    }

    /// <summary>Removes every find match.</summary>
    public void ClearFind()
    {
        _findMatches.Clear();
        CurrentFindMatch = -1;
    }

    /// <summary>
    /// Returns <c>true</c> when the characters just before <paramref name="start"/> and at
    /// <paramref name="end"/> in <paramref name="text"/> are not word characters.
    /// </summary>
    private static bool IsWholeWord(string text, int start, int end)
    {
        // Nothing to decode, at either end of the text, gives the replacement character, which is not a
        // word character, so the ends of the text are word boundaries.
        Rune.DecodeLastFromUtf16(text.AsSpan(0, start), out var before, out _);
        Rune.DecodeFromUtf16(text.AsSpan(end), out var after, out _);
        return !IsWordCharacter(before) && !IsWordCharacter(after);
    }

    private static bool IsWordCharacter(Rune rune) => Rune.IsLetterOrDigit(rune) || rune.Value == '_';

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>Directly sets anchor and focus as absolute offsets. For unit tests only.</summary>
    internal void SetSelectionForTest(int start, int end)
    {
        _anchor = start;
        _focus = end;
        ReportSelectionChange();
    }

    /// <summary>The anchor and focus as absolute offsets, <c>null</c> when nothing is selected. For unit tests only.</summary>
    internal (int? Anchor, int? Focus) SelectionForTest => (_anchor, _focus);

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when the entry's element, or one of its ancestors in the document, is hidden
    /// (<see cref="Visual.IsVisible"/> off), as the body of a collapsed alert is. Hidden entries are neither
    /// hit-tested, highlighted nor copied. Ancestors above the document (the viewer itself, e.g. on an
    /// inactive tab) are not considered, so a hidden viewer can still copy its selection.
    /// </summary>
    private bool IsHidden(IndexEntry entry)
    {
        var documentRoot = this.GetVisualParent();
        for (Visual? visual = entry.Element; visual is not null && visual != documentRoot; visual = visual.GetVisualParent())
        {
            if (!visual.IsVisible)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns <paramref name="posInLayer"/> in the entry's element local coordinate space,
    /// or null if the point is outside the element's visible bounds.
    /// </summary>
    private Point? ToLocalPos(IndexEntry entry, Point posInLayer)
    {
        if (!EnsureBounds(entry)) return null;
        if (!entry.CachedClip!.Value.Contains(posInLayer)) return null;
        return posInLayer - entry.CachedBounds!.Value.TopLeft;
    }

    /// <summary>
    /// Fills <see cref="IndexEntry.CachedBounds"/> and <see cref="IndexEntry.CachedClip"/> when
    /// they are not cached yet, so subsequent calls avoid a full visual-tree <c>TranslatePoint</c>
    /// walk. Returns <c>false</c> when the entry's element is not in the layer's visual tree.
    /// </summary>
    private bool EnsureBounds(IndexEntry entry)
    {
        if (entry.CachedBounds is not null) return true;

        var origin = entry.Element.TranslatePoint(new Point(0, 0), this);
        if (origin is null) return false;
        var bounds = new Rect(origin.Value, entry.Element.Bounds.Size);

        var clip = bounds;
        var documentRoot = this.GetVisualParent();
        for (var visual = entry.Element.GetVisualParent(); visual is not null && visual != documentRoot; visual = visual.GetVisualParent())
        {
            if (visual.ClipToBounds && visual.TranslatePoint(new Point(0, 0), this) is { } visualOrigin)
                clip = clip.Intersect(new Rect(visualOrigin, visual.Bounds.Size));
        }

        entry.CachedBounds = bounds;
        entry.CachedClip = clip;
        return true;
    }
}
