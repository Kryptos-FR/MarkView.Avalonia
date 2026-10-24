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
/// <param name="ReusedBlocks">How many leading top-level blocks kept their controls.</param>
/// <param name="HeadingsChanged">Whether the heading list now differs from before the update.</param>
/// <param name="FocusDiscarded">Whether the element focused before the update sat in a block that was discarded.</param>
internal readonly record struct UpdateResult(int ReusedBlocks, bool HeadingsChanged, bool FocusDiscarded);

/// <summary>
/// A markdown document rendered incrementally, for a text that only grows at its end. Each
/// <see cref="Update"/> receives the whole re-parsed document, keeps the controls of the leading
/// top-level blocks that cannot have changed and renders only the rest.
/// </summary>
/// <remarks>
/// Rendering is append-only: every top-level block adds, in order, controls to the root panel,
/// entries to the selection layer, stops and targets to the link layer, anchors, headings and
/// slugs, and never touches what an earlier block added. So the state a prefix of blocks produced
/// is exactly the first N of each of those collections, and counts are enough to cut back to it:
/// the document keeps, per block, how many of each its rendering added, and discarding the tail
/// means truncating every collection to the sum over the blocks kept.
/// </remarks>
internal sealed class LiveDocument
{
    private readonly List<BlockRecord> _records = [];
    private List<string> _invalidationKeys = [];

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
    /// longest prefix of blocks that cannot have changed and re-renders the rest.
    /// </summary>
    /// <param name="document">The re-parsed document.</param>
    /// <param name="focusedElement">
    /// The element that has keyboard focus, if any; reported through
    /// <see cref="UpdateResult.FocusDiscarded"/> when it sits in a block that is re-rendered.
    /// </param>
    public UpdateResult Update(MarkdownDocument document, IInputElement? focusedElement = null)
    {
        var keys = CollectInvalidationKeys(document);
        // Before the first update there are no records, so ReusablePrefix returns 0 by itself.
        var keysUnchanged = keys.SequenceEqual(_invalidationKeys, StringComparer.Ordinal);
        var reused = keysUnchanged ? ReusablePrefix(_records, document) : 0;

        // The last block may still be growing inside the control rendered for it (see IGrowingBlockRenderer):
        // that control is kept, and only the children that changed are rendered again.
        var growing = keysUnchanged && reused == _records.Count - 1 ? GrowingTailFor(_records[reused], document, reused) : null;
        var childrenReused = growing is null ? 0 : ReusablePrefix(growing.Children, (ContainerBlock)document[reused]);
        // Or the last block is a code block that still ends the text, whose kept chunks need no new layout (see GrowingCodeBlock).
        var growingCode = keysUnchanged && reused == _records.Count - 1 ? GrowingCodeFor(_records[reused], document, reused) : null;

        // The state the kept blocks produced: the sum of their counts.
        var kept = Sum(_records, reused);
        var rootChildren = kept.Children;
        var panelChildren = 0;
        if (growing is not null)
        {
            var keptChildren = Sum(growing.Children, childrenReused);
            rootChildren += growing.Shell.Children;
            panelChildren = keptChildren.Children;
            kept += growing.Shell + keptChildren;
        }
        else if (growingCode is not null)
        {
            // The code block keeps its shell; its selection entries, one per chunk, are registered again once it grew.
            rootChildren += _records[reused].Counts.Children;
            kept += _records[reused].Counts with { Entries = 0 };
        }

        var focusDiscarded = IsInDiscarded(focusedElement,
            RootPanel.Children.Skip(rootChildren).Concat(growing?.Panel.Children.Skip(panelChildren) ?? []));
        var removedHeadings = Renderer.HeadingCount - kept.Headings;

        RootPanel.Children.RemoveRange(rootChildren, RootPanel.Children.Count - rootChildren);
        growing?.Panel.Children.RemoveRange(panelChildren, growing.Panel.Children.Count - panelChildren);
        SelectionLayer.TruncateTo(kept.Entries);
        LinkLayer.TruncateTo(kept.Stops, kept.Targets);
        Renderer.TruncateAnchors(kept.Anchors);
        Renderer.TruncateHeadings(kept.Headings);
        Renderer.SlugGenerator.TruncateTo(kept.Slugs);

        if (growing is not null)
        {
            var container = (ContainerBlock)document[reused];
            growing.Children.RemoveRange(childrenReused, growing.Children.Count - childrenReused);
            WriteChildren(container, growing, childrenReused);
            _records[reused] = TailRecord(container, growing);
            reused++;
        }
        else if (growingCode is not null)
        {
            var block = (CodeBlock)document[reused];
            growingCode.Grow(block);
            var shellStart = rootChildren - _records[reused].Counts.Children;
            for (var c = shellStart; c < rootChildren; c++)
                AvaloniaRenderer.SetTopLevelBlock(RootPanel.Children[c], block);
            var entries = SelectionLayer.EntryCount;
            MarkdownViewer.RegisterBlocks(SelectionLayer, RootPanel, shellStart);
            _records[reused] = _records[reused] with
            {
                SpanEnd = block.Span.End,
                WasOpen = block.IsOpen,
                Counts = _records[reused].Counts with { Entries = SelectionLayer.EntryCount - entries },
            };
            reused++;
        }
        _records.RemoveRange(reused, _records.Count - reused);

        for (var i = reused; i < document.Count; i++)
        {
            var block = document[i];
            // Only the last block can grow with appended text.
            _records.Add(i == document.Count - 1 && block is ContainerBlock container && GrowingRendererFor(container) is { } growingRenderer
                ? WriteGrowing(container, growingRenderer)
                : i == document.Count - 1 && block is CodeBlock code && Renderer.IsProvisional(code)
                    ? WriteGrowingCode(code)
                    : Write(block, RootPanel, Renderer.WriteTopLevel));
        }

        // New blocks after a collapsed heading belong to its section, and a heading rendered again keeps the
        // state the user left it in.
        HeadingFold.Apply(RootPanel);
        Renderer.ApplyHeadingFolds();

        Document = document;
        _invalidationKeys = keys;
        return new UpdateResult(reused - (growing is null && growingCode is null ? 0 : 1), removedHeadings > 0 || Renderer.HeadingCount != kept.Headings, focusDiscarded);
    }

    /// <summary>
    /// Writes <paramref name="block"/> with <paramref name="write"/>, which adds its controls to the end of
    /// <paramref name="panel"/>, registers what they hold with the selection and link layers, and returns the
    /// record of what the block added.
    /// </summary>
    private BlockRecord Write(Block block, Panel panel, Action<Block> write)
    {
        var before = Snapshot(panel);
        write(block);
        MarkdownViewer.RegisterBlocks(SelectionLayer, panel, before.Children);
        for (var c = before.Children; c < panel.Children.Count; c++)
            LinkLayer.Collect(panel.Children[c]);
        return new BlockRecord(block.GetType(), block.Span.Start, block.Span.End, block.IsOpen, Renderer.IsProvisional(block),
            Snapshot(panel) - before);
    }

    /// <summary>
    /// Writes the last block of the document, a container that may keep growing, as its shell (see
    /// <see cref="IGrowingBlockRenderer"/>) and then each child, keeping a record per child so that the next
    /// update renders only the children that changed.
    /// </summary>
    private BlockRecord WriteGrowing(ContainerBlock container, IGrowingBlockRenderer growingRenderer)
    {
        Panel? panel = null;
        var shell = Write(container, RootPanel, _ => panel = growingRenderer.WriteShell(Renderer, container));
        if (panel is null)
            return shell;

        var tail = new GrowingTail(growingRenderer, growingRenderer.ShellKey(Renderer, container), panel, shell.Counts, []);
        WriteChildren(container, tail, 0);
        return TailRecord(container, tail);
    }

    /// <summary>
    /// Writes the last block of the document, a code block that ends the text so far, asking its renderer for a
    /// <see cref="GrowingCodeBlock"/>; a renderer that does not write one writes the block as usual.
    /// </summary>
    private BlockRecord WriteGrowingCode(CodeBlock block)
    {
        Renderer.GrowingCodeTarget = block;
        try
        {
            var record = Write(block, RootPanel, Renderer.WriteTopLevel);
            return record with { Code = Renderer.WrittenGrowingCode };
        }
        finally
        {
            Renderer.GrowingCodeTarget = null;
            Renderer.WrittenGrowingCode = null;
        }
    }

    /// <summary>
    /// The growing code block of <paramref name="record"/> when the block now at <paramref name="index"/>, the last one,
    /// is the same code block, still ending the text: same type, start and language.
    /// </summary>
    private GrowingCodeBlock? GrowingCodeFor(BlockRecord record, MarkdownDocument document, int index)
    {
        if (record.Code is not { } code || index != document.Count - 1 || document[index] is not CodeBlock block)
            return null;
        if (record.BlockType != block.GetType() || record.SpanStart != block.Span.Start || !Renderer.IsProvisional(block))
            return null;
        return code.CanGrow(block) ? code : null;
    }

    private void WriteChildren(ContainerBlock container, GrowingTail tail, int start)
    {
        Renderer.Push(tail.Panel);
        for (var c = start; c < container.Count; c++)
            tail.Children.Add(Write(container[c], tail.Panel, Renderer.Write));
        Renderer.Pop();
        HeadingFold.Apply(tail.Panel);
    }

    private BlockRecord TailRecord(ContainerBlock container, GrowingTail tail)
    {
        // The children are in the shell's panel: in the root panel, the block still adds its shell only.
        var counts = tail.Shell + Sum(tail.Children, tail.Children.Count);
        return new BlockRecord(container.GetType(), container.Span.Start, container.Span.End, container.IsOpen,
            Renderer.IsProvisional(container), counts with { Children = tail.Shell.Children }, tail);
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

    private Counts Snapshot(Panel panel) => new(
        panel.Children.Count, SelectionLayer.EntryCount, LinkLayer.StopCount, LinkLayer.TargetCount,
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
    /// container) whose controls can be kept, by their <paramref name="records"/>: same
    /// type and same source span as when rendered, and not still growing then. Only the last
    /// top-level block can grow with appended text, so <see cref="BlockRecord.WasOpen"/> is consulted
    /// for that block alone: Markdig reports some earlier blocks open although they cannot change (a
    /// pipe table closed by a blank line, a <see cref="LinkReferenceDefinitionGroup"/>, which stays
    /// where its first definition appeared and collects later ones; a new definition changes the
    /// invalidation keys, which already invalidates everything). The span check catches a last block
    /// Markdig reports closed but that still grows (a list with several items). A block rendered as
    /// provisional (see <see cref="AvaloniaRenderer.IsProvisional"/>) is never kept, even when Markdig
    /// reports it closed (an ATX heading), so that it is rendered again once it no longer contains
    /// the end of the text.
    /// </summary>
    private static int ReusablePrefix(List<BlockRecord> records, ContainerBlock blocks)
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
            // Append-only text never changes a closed block's type or start without moving its end; a
            // patched or replaced text can ("xyz" → "***", "\n\nb" → "\nbb"), so both are checked.
            if (record.BlockType != block.GetType() || record.SpanStart != block.Span.Start)
                break;
            k++;
        }
        return k;
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

    /// <param name="Tail">For the last block, when it was written as a growing container: its shell and children.</param>
    /// <param name="Code">For the last block, when it was written as a growing code block: that block.</param>
    private readonly record struct BlockRecord(
        Type BlockType, int SpanStart, int SpanEnd, bool WasOpen, bool WasProvisional, Counts Counts, GrowingTail? Tail = null,
        GrowingCodeBlock? Code = null);

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
