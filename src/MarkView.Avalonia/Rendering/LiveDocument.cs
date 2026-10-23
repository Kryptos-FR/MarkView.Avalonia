// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Markdig.Extensions.Abbreviations;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;

using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia.Rendering;

/// <summary>What <see cref="LiveDocument.Update"/> did.</summary>
/// <param name="ReusedBlocks">How many top-level blocks kept their controls.</param>
/// <param name="HeadingsChanged">Whether the heading list now differs from before the update.</param>
/// <param name="FocusDiscarded">Whether the element focused before the update sat in a block that was discarded.</param>
/// <param name="UnchangedBlocks">
/// How many top-level blocks have the same source as before, whether or not they kept their controls
/// (a changed link reference definition re-renders them all).
/// </param>
internal readonly record struct UpdateResult(int ReusedBlocks, bool HeadingsChanged, bool FocusDiscarded, int UnchangedBlocks = 0);

/// <summary>
/// A markdown document rendered incrementally. Each <see cref="Update"/> receives the whole re-parsed
/// document, keeps the controls of the top-level blocks that cannot have changed and renders only
/// the rest: for a text that only grows at its end, the blocks before the first one that changed;
/// for an edited text, also the blocks after the last one that changed.
/// </summary>
/// <remarks>
/// Rendering is append-only: every top-level block adds, in order, controls to the root panel,
/// entries to the selection layer, stops and targets to the link layer, anchors, headings and
/// slugs, and never touches what another block added. So the state each block produced is a range of
/// each of those collections, and counts are enough to find it: the document keeps, per block, how
/// many of each its rendering added. Discarding the tail means truncating every collection to the sum
/// over the blocks kept; replacing blocks in the middle splices the ranges of the blocks discarded with
/// what rendering the new ones added, and a block kept after a change keeps its ranges, moved by what
/// changed before it.
/// </remarks>
internal sealed class LiveDocument
{
    private List<BlockRecord> _records = [];
    private List<string> _invalidationKeys = [];
    // The text the last document was parsed from, against which the next one is compared.
    private string _text = "";
    private readonly Action<MarkdownHyperlink> _activateLink;

    /// <summary>
    /// Creates a live document that renders with <paramref name="renderer"/>, whose link targets call
    /// <paramref name="activateLink"/>. Begins the renderer's document.
    /// </summary>
    public LiveDocument(AvaloniaRenderer renderer, Action<MarkdownHyperlink> activateLink)
    {
        Renderer = renderer;
        renderer.BeginDocument();
        SelectionLayer = new DocumentSelectionLayer();
        LinkLayer = new LinkFocusLayer(activateLink);
        _activateLink = activateLink;
    }

    /// <summary>The renderer the blocks are written with.</summary>
    public AvaloniaRenderer Renderer { get; }

    /// <summary>The overlay that owns cross-block text selection.</summary>
    public DocumentSelectionLayer SelectionLayer { get; }

    /// <summary>The overlay that hosts the keyboard stand-ins for the document's links.</summary>
    public LinkFocusLayer LinkLayer { get; }

    /// <summary>The panel the top-level blocks are rendered into.</summary>
    public StackPanel RootPanel => Renderer.RootPanel;

    /// <summary>The last document passed to <see cref="Update"/>.</summary>
    public MarkdownDocument? Document { get; private set; }

    /// <summary>How many top-level blocks have a record, i.e. are currently rendered.</summary>
    public int BlockCount => _records.Count;

    /// <summary>
    /// Brings the rendering up to date with <paramref name="document"/>: keeps the controls of the
    /// longest prefix of blocks that cannot have changed (and, with <paramref name="reuseAfterChange"/>,
    /// of the longest suffix) and re-renders the rest.
    /// </summary>
    /// <param name="document">The re-parsed document.</param>
    /// <param name="text">The text <paramref name="document"/> was parsed from.</param>
    /// <param name="focusedElement">
    /// The element that has keyboard focus, if any; reported through
    /// <see cref="UpdateResult.FocusDiscarded"/> when it sits in a block that is re-rendered.
    /// </param>
    /// <param name="reuseAfterChange">
    /// Whether the blocks after the last changed one can be kept too, for a text edited anywhere.
    /// A streaming session's text only grows at its end, where nothing follows the change.
    /// </param>
    public UpdateResult Update(MarkdownDocument document, string text, IInputElement? focusedElement = null, bool reuseAfterChange = false)
    {
        var keys = CollectInvalidationKeys(document);
        // Before the first update there are no records, so both scans return 0 by themselves.
        var prefix = ReusablePrefix(_records, document, text);
        var suffix = reuseAfterChange ? MatchingSuffix(document, text, prefix) : 0;
        var unchanged = prefix + suffix;
        var keysUnchanged = keys.SequenceEqual(_invalidationKeys, StringComparer.Ordinal);
        if (!keysUnchanged)
            (prefix, suffix) = (0, 0);

        // What is recorded by source position follows the text that did not change.
        if (reuseAfterChange && text.Length != _text.Length)
            Renderer.ShiftSourcePositions(_text.Length - CommonSuffixLength(_text, text), text.Length - _text.Length);

        var headingsBefore = Renderer.HeadingEntries.ToList();
        var (reused, focusDiscarded) = suffix == 0
            ? Truncate(document, text, prefix, keysUnchanged, focusedElement)
            : Splice(document, text, prefix, suffix, focusedElement);

        // New blocks after a collapsed heading belong to its section, and a heading rendered again keeps the
        // state the user left it in.
        HeadingFold.Apply(RootPanel);
        Renderer.ApplyHeadingFolds();

        Document = document;
        _text = text;
        _invalidationKeys = keys;
        return new UpdateResult(reused, !headingsBefore.SequenceEqual(Renderer.HeadingEntries), focusDiscarded, unchanged);
    }

    /// <summary>
    /// Replaces <paramref name="length"/> characters of the last text from <paramref name="start"/>
    /// with <paramref name="replacement"/>, which has the same length, for a change the rendered controls
    /// already show (a toggled task), so that the next update finds that block unchanged.
    /// </summary>
    public void PatchText(int start, int length, string replacement)
    {
        if (start >= 0 && length == replacement.Length && start + length <= _text.Length)
            _text = string.Concat(_text.AsSpan(0, start), replacement, _text.AsSpan(start + length));
    }

    /// <summary>
    /// Keeps the first <paramref name="prefix"/> blocks and renders the rest after them. The last block
    /// may still be growing inside the control rendered for it (see <see cref="IGrowingBlockRenderer"/>):
    /// that control is kept, and only the children that changed are rendered again. Returns how many
    /// blocks kept their controls, and whether the focused element was in one that did not.
    /// </summary>
    private (int Reused, bool FocusDiscarded) Truncate(
        MarkdownDocument document, string text, int prefix, bool keysUnchanged, IInputElement? focusedElement)
    {
        var growing = keysUnchanged && prefix == _records.Count - 1 ? GrowingTailFor(_records[prefix], document, prefix) : null;
        var childrenReused = growing is null ? 0 : ReusablePrefix(growing.Children, (ContainerBlock)document[prefix], text);

        // The state the kept blocks produced: the sum of their counts.
        var kept = Sum(_records, prefix);
        var rootChildren = kept.Children;
        var panelChildren = 0;
        if (growing is not null)
        {
            var keptChildren = Sum(growing.Children, childrenReused);
            rootChildren += growing.Shell.Children;
            panelChildren = keptChildren.Children;
            kept += growing.Shell + keptChildren;
        }

        var focusDiscarded = IsInDiscarded(focusedElement,
            RootPanel.Children.Skip(rootChildren).Concat(growing?.Panel.Children.Skip(panelChildren) ?? []));

        RootPanel.Children.RemoveRange(rootChildren, RootPanel.Children.Count - rootChildren);
        growing?.Panel.Children.RemoveRange(panelChildren, growing.Panel.Children.Count - panelChildren);
        SelectionLayer.TruncateTo(kept.Entries);
        LinkLayer.TruncateTo(kept.Stops, kept.Targets);
        Renderer.TruncateAnchors(kept.Anchors);
        Renderer.TruncateHeadings(kept.Headings);
        Renderer.SlugGenerator.TruncateTo(kept.Slugs);

        var written = prefix;
        if (growing is not null)
        {
            var container = (ContainerBlock)document[prefix];
            growing.Children.RemoveRange(childrenReused, growing.Children.Count - childrenReused);
            WriteChildren(container, growing, childrenReused, text);
            _records[prefix] = TailRecord(document, prefix, text, growing);
            written++;
        }
        _records.RemoveRange(written, _records.Count - written);

        for (var i = written; i < document.Count; i++)
        {
            var block = document[i];
            // Only the last block can grow with appended text.
            _records.Add(i == document.Count - 1 && block is ContainerBlock container && GrowingRendererFor(container) is { } growingRenderer
                ? WriteGrowing(document, i, text, growingRenderer)
                : Write(document, i, text, RootPanel, SelectionLayer, LinkLayer, Renderer.WriteTopLevel));
        }
        return (prefix, focusDiscarded);
    }

    /// <summary>
    /// Keeps the first <paramref name="prefix"/> and the last <paramref name="suffix"/> blocks and renders
    /// the ones in between in place of those they replace. A kept block after the change is rendered again
    /// when its headings would get other slugs, and the footnote group always is. Returns how many blocks
    /// kept their controls, and whether the focused element was in one that did not.
    /// </summary>
    private (int Reused, bool FocusDiscarded) Splice(
        MarkdownDocument document, string text, int prefix, int suffix, IInputElement? focusedElement)
    {
        var splice = new Splicer(this, focusedElement);
        var records = new List<BlockRecord>(document.Count);
        // The prefix keeps its ranges where they are; everything the renderer recorded after them is
        // dropped, and the suffix records its own again as it is kept.
        for (var i = 0; i < prefix; i++)
        {
            splice.Skip(_records[i]);
            records.Add(_records[i]);
        }
        var reused = prefix;
        var oldSuffixStart = _records.Count - suffix;
        var saved = splice.DetachRendererState(_records, prefix, oldSuffixStart);
        var newSuffixStart = document.Count - suffix;
        for (var i = prefix; i < oldSuffixStart; i++)
            splice.Discard(_records[i]);
        for (var i = prefix; i < newSuffixStart; i++)
            records.Add(splice.Render(document, i, text));

        for (var s = 0; s < suffix; s++)
        {
            var record = _records[oldSuffixStart + s];
            // A footnote's definition is moved into the group, so the group's own source does not
            // show its changes: it is always rendered again (it is short and comes last).
            if (record.BlockType != typeof(FootnoteGroup) && splice.TryReattach(saved[s]))
            {
                splice.Keep(record);
                records.Add(Moved(record, document, newSuffixStart + s, text));
                reused++;
            }
            else
            {
                splice.Discard(record);
                records.Add(splice.Render(document, newSuffixStart + s, text));
            }
        }
        splice.Flush();

        _records = records;
        return (reused, splice.FocusDiscarded);
    }

    /// <summary>
    /// <paramref name="record"/>, kept for the block now at <paramref name="index"/>, at that block's
    /// position: the syntax tree its controls were rendered from, which they read source positions from
    /// later (a task's paragraph, a fold state's key, the source copied as Markdown), moves by the same
    /// distance.
    /// </summary>
    private static BlockRecord Moved(BlockRecord record, MarkdownDocument document, int index, string text)
    {
        var block = document[index];
        var delta = block.Span.Start - record.SpanStart;
        var lines = block.Line - record.Block.Line;
        if (delta != 0 || lines != 0)
        {
            // A growing container's children were rendered from the parses that saw them complete.
            var trees = new[] { record.Block }.Concat(record.Tail?.Children.Select(child => child.Block) ?? []);
            var moved = new HashSet<MarkdownObject>(ReferenceEqualityComparer.Instance);
            foreach (var obj in trees.SelectMany(tree => tree.Descendants().Prepend(tree)))
            {
                if (!moved.Add(obj))
                    continue;
                if (obj.Span.Start >= 0)
                    obj.Span = new SourceSpan(obj.Span.Start + delta, obj.Span.End + delta);
                obj.Line += lines;
            }
        }
        // A growing container is now followed by the blocks kept after it: it no longer grows.
        return record with
        {
            SpanStart = block.Span.Start, SpanEnd = block.Span.End, ExtentEnd = ExtentEnd(document, index, text), Tail = null,
        };
    }

    /// <summary>
    /// Writes block <paramref name="index"/> of <paramref name="parent"/> with <paramref name="write"/>, which adds
    /// its controls to the end of <paramref name="panel"/>, registers what they hold with
    /// <paramref name="selection"/> and <paramref name="links"/>, and returns the record of what the block added.
    /// </summary>
    private BlockRecord Write(ContainerBlock parent, int index, string text, Panel panel,
        DocumentSelectionLayer selection, LinkFocusLayer links, Action<Block> write)
    {
        var block = parent[index];
        var before = Snapshot(panel, selection, links);
        write(block);
        MarkdownViewer.RegisterBlocks(selection, panel, before.Children);
        for (var c = before.Children; c < panel.Children.Count; c++)
            links.Collect(panel.Children[c]);
        return new BlockRecord(block.GetType(), block.Span.Start, block.Span.End, ExtentEnd(parent, index, text),
            block.IsOpen, Renderer.IsProvisional(block), Snapshot(panel, selection, links) - before, block);
    }

    /// <summary>
    /// Writes the last block of the document, a container that may keep growing, as its shell (see
    /// <see cref="IGrowingBlockRenderer"/>) and then each child, keeping a record per child so that the next
    /// update renders only the children that changed.
    /// </summary>
    private BlockRecord WriteGrowing(MarkdownDocument document, int index, string text, IGrowingBlockRenderer growingRenderer)
    {
        var container = (ContainerBlock)document[index];
        Panel? panel = null;
        var shell = Write(document, index, text, RootPanel, SelectionLayer, LinkLayer,
            _ => panel = growingRenderer.WriteShell(Renderer, container));
        if (panel is null)
            return shell;

        var tail = new GrowingTail(growingRenderer, growingRenderer.ShellKey(Renderer, container), panel, shell.Counts, []);
        WriteChildren(container, tail, 0, text);
        return TailRecord(document, index, text, tail);
    }

    private void WriteChildren(ContainerBlock container, GrowingTail tail, int start, string text)
    {
        Renderer.Push(tail.Panel);
        for (var c = start; c < container.Count; c++)
            tail.Children.Add(Write(container, c, text, tail.Panel, SelectionLayer, LinkLayer, Renderer.Write));
        Renderer.Pop();
        HeadingFold.Apply(tail.Panel);
    }

    private BlockRecord TailRecord(MarkdownDocument document, int index, string text, GrowingTail tail)
    {
        var container = document[index];
        // The children are in the shell's panel: in the root panel, the block still adds its shell only.
        var counts = tail.Shell + Sum(tail.Children, tail.Children.Count);
        return new BlockRecord(container.GetType(), container.Span.Start, container.Span.End, ExtentEnd(document, index, text),
            container.IsOpen, Renderer.IsProvisional(container), counts with { Children = tail.Shell.Children }, container, tail);
    }

    /// <summary>
    /// The growing tail of <paramref name="record"/> when the block now at <paramref name="index"/> can still
    /// take its new children into the control rendered for it: same type and start, and the same shell.
    /// </summary>
    private GrowingTail? GrowingTailFor(BlockRecord record, MarkdownDocument document, int index)
    {
        if (record.Tail is not { } tail || index >= document.Count || document[index] is not ContainerBlock container)
            return null;
        if (record.BlockType != container.GetType() || record.SpanStart != container.Span.Start)
            return null;
        return Equals(tail.Renderer.ShellKey(Renderer, container), tail.Key) ? tail : null;
    }

    private IGrowingBlockRenderer? GrowingRendererFor(ContainerBlock container)
    {
        // The renderer Markdig dispatches to: the first that accepts the block's type.
        foreach (var renderer in Renderer.ObjectRenderers)
        {
            if (renderer.Accept(Renderer, container.GetType()))
                return renderer as IGrowingBlockRenderer;
        }
        return null;
    }

    private Counts Snapshot(Panel panel, DocumentSelectionLayer selection, LinkFocusLayer links) => new(
        panel.Children.Count, selection.EntryCount, links.StopCount, links.TargetCount,
        Renderer.AnchorCount, Renderer.HeadingCount, Renderer.SlugGenerator.Count);

    private static Counts Sum(List<BlockRecord> records, int count)
    {
        var sum = default(Counts);
        for (var i = 0; i < count; i++)
            sum += records[i].Counts;
        return sum;
    }

    /// <summary>
    /// What, anywhere in <paramref name="document"/>, can change how an earlier block renders, in
    /// order: each link reference definition's label, URL and title, each footnote's label, and each
    /// abbreviation's label and text. A change in this list invalidates every block.
    /// </summary>
    /// <remarks>
    /// A definition's label is complete before its URL and title are, so the label alone would keep a
    /// block whose link still points to the partial URL. A footnote's content is rendered in its group,
    /// which is re-rendered anyway, so its label is enough. Markdig removes abbreviation definitions
    /// from the block tree once it has rewritten every paragraph's inlines with them, so they are read
    /// from the document's abbreviation table instead.
    /// </remarks>
    private static List<string> CollectInvalidationKeys(MarkdownDocument document)
    {
        var keys = new List<string>();
        foreach (var block in document)
        {
            switch (block)
            {
                case LinkReferenceDefinitionGroup group:
                    foreach (var definition in group.OfType<LinkReferenceDefinition>())
                        keys.Add($"{definition.Label}\0{definition.Url}\0{definition.Title}");
                    break;
                case FootnoteGroup footnotes:
                    foreach (var footnote in footnotes.OfType<Footnote>())
                        keys.Add(footnote.Label ?? "");
                    break;
            }
        }
        if (document.GetAbbreviations() is { } abbreviations)
        {
            // The same text always fills the table in the same order.
            foreach (var (label, abbreviation) in abbreviations)
                keys.Add($"{label}\0{abbreviation.Text}");
        }
        return keys;
    }

    /// <summary>
    /// The number of leading blocks of <paramref name="blocks"/> (the document's, or those of a growing
    /// container) whose controls can be kept, by their <paramref name="records"/>: same type, same source
    /// span and same source text as when rendered, and not still growing then. Only the last top-level
    /// block can grow with appended text, so <see cref="BlockRecord.WasOpen"/> is consulted for that block
    /// alone: Markdig reports some earlier blocks open although they cannot change (a pipe table closed by
    /// a blank line, a <see cref="LinkReferenceDefinitionGroup"/>, which stays where its first definition
    /// appeared and collects later ones; a new definition changes the invalidation keys, which already
    /// invalidates everything). The span check catches a last block Markdig reports closed but that still
    /// grows (a list with several items). A block rendered as provisional (see
    /// <see cref="AvaloniaRenderer.IsProvisional"/>) is never kept, even when Markdig reports it closed (an
    /// ATX heading), so that it is rendered again once it no longer contains the end of the text.
    /// </summary>
    private int ReusablePrefix(List<BlockRecord> records, ContainerBlock blocks, string text)
    {
        var limit = Math.Min(records.Count, blocks.Count);
        var last = records.Count - 1;
        var k = 0;
        while (k < limit)
        {
            var record = records[k];
            var block = blocks[k];
            if (record.WasProvisional || (record.WasOpen && k == last))
                break;
            if (record.SpanEnd != block.Span.End)
                break;
            // Append-only text never changes a closed block's type, start or text without moving its
            // end; an edited text can ("xyz" → "***", "\n\nb" → "\nbb", "abc" → "abd").
            if (record.SpanStart != block.Span.Start || !SameSource(record, blocks, k, text))
                break;
            k++;
        }
        return k;
    }

    /// <summary>
    /// The number of trailing blocks of <paramref name="document"/>, after its first
    /// <paramref name="prefix"/>, that have the same type and source text as the trailing records,
    /// wherever that text now starts. A trailing <see cref="FootnoteGroup"/> on both sides is counted
    /// too, so that the blocks before it can be matched; it is rendered again all the same.
    /// </summary>
    /// <remarks>
    /// A top-level block starts with nothing open around it, so the same text parses to the same
    /// block wherever it sits; what else its rendering depends on is in the invalidation keys, or is a
    /// heading's slug, which <see cref="Splicer.TryReattach"/> checks.
    /// </remarks>
    private int MatchingSuffix(MarkdownDocument document, string text, int prefix)
    {
        var limit = Math.Min(_records.Count, document.Count) - prefix;
        var m = 0;
        while (m < limit)
        {
            var record = _records[_records.Count - 1 - m];
            var block = document[document.Count - 1 - m];
            if (record.WasProvisional)
                break;
            var footnotes = record.BlockType == typeof(FootnoteGroup) && block is FootnoteGroup;
            if (!footnotes && (record.SpanEnd - record.SpanStart != block.Span.End - block.Span.Start
                || !SameSource(record, document, document.Count - 1 - m, text)))
                break;
            m++;
        }
        return m;
    }

    /// <summary>
    /// Whether block <paramref name="index"/> of <paramref name="parent"/> has the type
    /// <paramref name="record"/> was rendered for, and the same source in <paramref name="text"/> as the
    /// record had in the last text: the same span text, and the same text up to the next block (see
    /// <see cref="ExtentEnd"/>), but for the blank lines around it unless it reaches the end of the text.
    /// </summary>
    private bool SameSource(BlockRecord record, ContainerBlock parent, int index, string text)
    {
        var block = parent[index];
        if (record.BlockType != block.GetType())
            return false;
        var start = block.Span.Start;
        var length = block.Span.Length;
        if (length != record.SpanEnd - record.SpanStart + 1)
            return false;
        // A block without a source span of its own (one a parser extension created) is judged by its
        // type and span alone.
        if (length <= 0 || record.SpanStart < 0 || start < 0
            || record.SpanStart + length > _text.Length || start + length > text.Length)
            return true;
        if (!_text.AsSpan(record.SpanStart, length).SequenceEqual(text.AsSpan(start, length)))
            return false;

        var oldGap = _text.AsSpan(record.SpanEnd + 1, Math.Max(0, Math.Min(record.ExtentEnd, _text.Length) - record.SpanEnd - 1));
        var extentEnd = ExtentEnd(parent, index, text);
        var newGap = text.AsSpan(block.Span.End + 1, Math.Max(0, extentEnd - block.Span.End - 1));
        // Up to the end of the text, the gap may be the rest of a block Markdig left open there, whose
        // blank lines count (an unclosed fence).
        return record.ExtentEnd >= _text.Length || extentEnd >= text.Length
            ? oldGap.SequenceEqual(newGap)
            : oldGap.Trim().SequenceEqual(newGap.Trim());
    }

    /// <summary>
    /// Where the source of block <paramref name="index"/> of <paramref name="parent"/> ends, exclusive: where
    /// the next block in source order starts, or the end of <paramref name="text"/> for the last one. Markdig
    /// leaves the span of a block still open at the end of the text on its first line (an unclosed
    /// fence), and moves some definitions out of the tree (abbreviations, footnotes), so the span alone
    /// does not cover all the text a block's rendering depends on; what lies between two blocks is
    /// taken as the first one's. A block placed out of source order (a footnote or definition group,
    /// which sits after or before the blocks around its definitions) does not end the one before it.
    /// </summary>
    private static int ExtentEnd(ContainerBlock parent, int index, string text)
    {
        var block = parent[index];
        for (var i = index + 1; i < parent.Count; i++)
        {
            if (parent[i].Span.Start > block.Span.Start)
                return Math.Max(block.Span.End + 1, parent[i].Span.Start);
        }
        return Math.Max(block.Span.End + 1, text.Length);
    }

    /// <summary>
    /// How many characters <paramref name="a"/> and <paramref name="b"/> have in common at their end, leaving
    /// out those they also have in common at their start.
    /// </summary>
    private static int CommonSuffixLength(string a, string b)
    {
        var prefix = a.AsSpan().CommonPrefixLength(b);
        var limit = Math.Min(a.Length, b.Length) - prefix;
        var n = 0;
        while (n < limit && a[a.Length - 1 - n] == b[b.Length - 1 - n])
            n++;
        return n;
    }

    /// <summary>
    /// Whether <paramref name="focused"/> sits in one of the <paramref name="discarded"/> controls. A link
    /// target lives in the link layer, so it is judged by the text block it stands over.
    /// </summary>
    private static bool IsInDiscarded(IInputElement? focused, IEnumerable<Control> discarded)
    {
        var visual = focused is LinkFocusTarget target ? target.TextBlock : focused as Visual;
        if (visual is null) return false;

        var set = discarded.ToHashSet();
        return visual.GetSelfAndVisualAncestors().Any(v => v is Control control && set.Contains(control));
    }

    /// <summary>
    /// One update's changes to the middle of the rendered document, made in reading order: blocks are kept
    /// (their ranges skipped), discarded (their ranges removed) or rendered (into scratch containers, whose
    /// contents replace the discarded ranges when the next kept block, or the end, is reached).
    /// </summary>
    private sealed class Splicer(LiveDocument live, IInputElement? focused)
    {
        // Where the next block's ranges start in each collection.
        private Counts _position;
        // The ranges of the blocks discarded since the last flush, from the position above.
        private Counts _removed;
        // What the blocks rendered since the last flush produced.
        private readonly StackPanel _panel = new();
        private DocumentSelectionLayer _selection = new();
        private LinkFocusLayer _links = new(live._activateLink);

        private AvaloniaRenderer Renderer => live.Renderer;

        public bool FocusDiscarded { get; private set; }

        /// <summary>Moves past a block of the kept prefix, whose renderer state stays where it is.</summary>
        public void Skip(BlockRecord record) => _position += record.Counts;

        /// <summary>
        /// Drops what the renderer recorded after the first <paramref name="prefix"/> records, and
        /// returns the part of it each record from <paramref name="suffixStart"/> on produced, for the
        /// kept ones to record again.
        /// </summary>
        public List<RendererState> DetachRendererState(List<BlockRecord> records, int prefix, int suffixStart)
        {
            var kept = Sum(records, prefix);
            var at = Sum(records, suffixStart);
            var states = new List<RendererState>(records.Count - suffixStart);
            for (var i = suffixStart; i < records.Count; i++)
            {
                var counts = records[i].Counts;
                states.Add(new RendererState(
                    Renderer.AnchorRegistrations.Skip(at.Anchors).Take(counts.Anchors).ToList(),
                    Renderer.HeadingEntries.Skip(at.Headings).Take(counts.Headings).ToList(),
                    Renderer.SlugGenerator.Generated.Skip(at.Slugs).Take(counts.Slugs).Select(s => (s.Base, s.Slug)).ToList()));
                at += counts;
            }

            Renderer.TruncateAnchors(kept.Anchors);
            Renderer.TruncateHeadings(kept.Headings);
            Renderer.SlugGenerator.TruncateTo(kept.Slugs);
            return states;
        }

        /// <summary>
        /// Records again the anchors and headings of a block kept after a change, if its headings get
        /// the same slugs as before; does nothing and returns <c>false</c> when one of them would not
        /// (a heading with the same text was added or removed before it), as the block then needs to
        /// be rendered again.
        /// </summary>
        public bool TryReattach(RendererState state)
        {
            var slugs = Renderer.SlugGenerator;
            var before = slugs.Count;
            foreach (var (slugBase, slug) in state.Slugs)
            {
                if (slugs.GenerateFromBase(slugBase) != slug)
                {
                    slugs.TruncateTo(before);
                    return false;
                }
            }
            foreach (var (id, control) in state.Anchors)
                Renderer.RegisterAnchor(id, control);
            foreach (var (level, text, slug) in state.Headings)
                Renderer.RegisterHeading(level, text, slug);
            return true;
        }

        /// <summary>Keeps a block after a change: what was rendered before it goes in first.</summary>
        public void Keep(BlockRecord record)
        {
            Flush();
            _position += record.Counts;
        }

        /// <summary>Marks a block's ranges for removal.</summary>
        public void Discard(BlockRecord record)
        {
            if (!FocusDiscarded && focused is not null)
            {
                var start = _position.Children + _removed.Children;
                FocusDiscarded = IsInDiscarded(focused, live.RootPanel.Children.Skip(start).Take(record.Counts.Children));
            }
            _removed += record.Counts;
        }

        /// <summary>Renders a top-level block into the scratch containers and returns its record.</summary>
        public BlockRecord Render(MarkdownDocument document, int index, string text) =>
            live.Write(document, index, text, _panel, _selection, _links, block => Renderer.WriteTopLevel(block, _panel));

        /// <summary>Replaces the ranges discarded since the last flush with what was rendered since.</summary>
        public void Flush()
        {
            var rendered = _panel.Children.ToList();
            if (rendered.Count == 0 && _removed.Children == 0 && _removed.Entries == 0 && _removed.Stops == 0 && _removed.Targets == 0)
                return;
            _panel.Children.Clear();

            var children = live.RootPanel.Children;
            children.RemoveRange(_position.Children, _removed.Children);
            children.InsertRange(_position.Children, rendered);
            var entries = _selection.Entries.ToList();
            live.SelectionLayer.Splice(_position.Entries, _removed.Entries, entries);
            var stops = _links.StopCount;
            var targets = _links.TargetCount;
            live.LinkLayer.Splice(_position.Stops, _removed.Stops, _position.Targets, _removed.Targets, _links);

            _position = _position with
            {
                Children = _position.Children + rendered.Count,
                Entries = _position.Entries + entries.Count,
                Stops = _position.Stops + stops,
                Targets = _position.Targets + targets,
            };
            _removed = default;
            _selection = new DocumentSelectionLayer();
            _links = new LinkFocusLayer(live._activateLink);
        }
    }

    /// <summary>
    /// What rendering blocks added: controls to their panel (the root panel, or a growing container's), entries to
    /// the selection layer, stops and targets to the link layer, anchors, headings and slugs.
    /// </summary>
    private readonly record struct Counts(int Children, int Entries, int Stops, int Targets, int Anchors, int Headings, int Slugs)
    {
        public static Counts operator +(Counts a, Counts b) => new(
            a.Children + b.Children, a.Entries + b.Entries, a.Stops + b.Stops, a.Targets + b.Targets,
            a.Anchors + b.Anchors, a.Headings + b.Headings, a.Slugs + b.Slugs);

        public static Counts operator -(Counts a, Counts b) => new(
            a.Children - b.Children, a.Entries - b.Entries, a.Stops - b.Stops, a.Targets - b.Targets,
            a.Anchors - b.Anchors, a.Headings - b.Headings, a.Slugs - b.Slugs);
    }

    /// <summary>The anchors, headings and slugs a block's rendering recorded, in order.</summary>
    private sealed record RendererState(
        List<(string Id, Control Control)> Anchors,
        List<(int Level, string Text, string Slug)> Headings,
        List<(string Base, string Slug)> Slugs);

    /// <summary>
    /// What rendering a block produced: where its source was, where the text it depends on ends (see
    /// <see cref="ExtentEnd"/>), how many of each of the document's collections it added to, and the block
    /// itself, which its controls read source positions from.
    /// </summary>
    /// <param name="Tail">For the last block, when it was written as a growing container: its shell and children.</param>
    private readonly record struct BlockRecord(
        Type BlockType, int SpanStart, int SpanEnd, int ExtentEnd, bool WasOpen, bool WasProvisional, Counts Counts,
        Block Block, GrowingTail? Tail = null);

    /// <summary>
    /// A growing container written as a shell and then child by child.
    /// </summary>
    /// <param name="Renderer">The renderer that wrote the shell.</param>
    /// <param name="Key">The shell's key (see <see cref="IGrowingBlockRenderer.ShellKey"/>) when it was written.</param>
    /// <param name="Panel">The panel of the shell the children are written into.</param>
    /// <param name="Shell">What writing the shell added, before any child.</param>
    /// <param name="Children">A record per child, in order.</param>
    private sealed record GrowingTail(IGrowingBlockRenderer Renderer, object? Key, Panel Panel, Counts Shell, List<BlockRecord> Children);
}
