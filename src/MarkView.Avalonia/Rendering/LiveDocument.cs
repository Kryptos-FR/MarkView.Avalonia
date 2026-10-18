// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Markdig.Extensions.Footnotes;
using Markdig.Syntax;

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
    private List<string> _labels = [];

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
        var labels = CollectLabels(document);
        var reused = Document is null || !labels.SequenceEqual(_labels, StringComparer.Ordinal)
            ? 0
            : ReusablePrefix(document);

        // The state the first `reused` blocks produced: the sum of their counts.
        int children = 0, entries = 0, stops = 0, targets = 0, anchors = 0, headings = 0, slugs = 0;
        for (var i = 0; i < reused; i++)
        {
            var record = _records[i];
            children += record.ChildCount;
            entries += record.SelectionEntries;
            stops += record.LinkStops;
            targets += record.LinkTargets;
            anchors += record.Anchors;
            headings += record.Headings;
            slugs += record.Slugs;
        }

        var focusDiscarded = IsInDiscardedChildren(focusedElement, children);
        var removedHeadings = Renderer.HeadingCount - headings;

        RootPanel.Children.RemoveRange(children, RootPanel.Children.Count - children);
        SelectionLayer.TruncateTo(entries);
        LinkLayer.TruncateTo(stops, targets);
        Renderer.TruncateAnchors(anchors);
        Renderer.TruncateHeadings(headings);
        Renderer.SlugGenerator.TruncateTo(slugs);
        _records.RemoveRange(reused, _records.Count - reused);

        for (var i = reused; i < document.Count; i++)
        {
            var block = document[i];
            var childrenBefore = RootPanel.Children.Count;
            var entriesBefore = SelectionLayer.EntryCount;
            var stopsBefore = LinkLayer.StopCount;
            var targetsBefore = LinkLayer.TargetCount;
            var anchorsBefore = Renderer.AnchorCount;
            var headingsBefore = Renderer.HeadingCount;
            var slugsBefore = Renderer.SlugGenerator.Count;

            Renderer.WriteTopLevel(block);
            MarkdownViewer.RegisterBlocks(SelectionLayer, RootPanel, childrenBefore);
            for (var c = childrenBefore; c < RootPanel.Children.Count; c++)
                LinkLayer.Collect((Control)RootPanel.Children[c]);

            _records.Add(new BlockRecord(
                block.GetType(), block.Span.Start, block.Span.End, block.IsOpen,
                RootPanel.Children.Count - childrenBefore,
                SelectionLayer.EntryCount - entriesBefore,
                LinkLayer.StopCount - stopsBefore,
                LinkLayer.TargetCount - targetsBefore,
                Renderer.AnchorCount - anchorsBefore,
                Renderer.HeadingCount - headingsBefore,
                Renderer.SlugGenerator.Count - slugsBefore));
        }

        Document = document;
        _labels = labels;
        return new UpdateResult(reused, removedHeadings > 0 || Renderer.HeadingCount != headings, focusDiscarded);
    }

    /// <summary>
    /// The labels of every link reference definition and footnote of <paramref name="document"/>, in
    /// order. A definition anywhere can change how an earlier block renders, so a change in this list
    /// invalidates every block.
    /// </summary>
    private static List<string> CollectLabels(MarkdownDocument document)
    {
        var labels = new List<string>();
        foreach (var block in document)
        {
            switch (block)
            {
                case LinkReferenceDefinitionGroup group:
                    foreach (var definition in group.OfType<LinkReferenceDefinition>())
                        labels.Add(definition.Label ?? "");
                    break;
                case FootnoteGroup footnotes:
                    foreach (var footnote in footnotes.OfType<Footnote>())
                        labels.Add(footnote.Label ?? "");
                    break;
            }
        }
        return labels;
    }

    /// <summary>
    /// The number of leading blocks of <paramref name="document"/> whose controls can be kept: same
    /// type and same source span as when rendered, and closed then. Markdig keeps a
    /// <see cref="LinkReferenceDefinitionGroup"/> open for ever, at the place its first definition
    /// appeared, and adds later definitions to it, so it is exempt from the closed test: it renders no
    /// control, and a new definition changes the labels, which already invalidates everything.
    /// </summary>
    private int ReusablePrefix(MarkdownDocument document)
    {
        var limit = Math.Min(_records.Count, document.Count);
        var k = 0;
        while (k < limit)
        {
            var record = _records[k];
            var block = document[k];
            if ((record.WasOpen && block is not LinkReferenceDefinitionGroup)
                || record.BlockType != block.GetType()
                || record.SpanStart != block.Span.Start
                || record.SpanEnd != block.Span.End)
                break;
            k++;
        }
        return k;
    }

    /// <summary>
    /// Whether <paramref name="focused"/> sits in <c>RootPanel.Children[firstDiscarded..]</c>. A link
    /// target lives in the link layer, so it is judged by the text block it stands over.
    /// </summary>
    private bool IsInDiscardedChildren(IInputElement? focused, int firstDiscarded)
    {
        var visual = focused is LinkFocusTarget target ? target.TextBlock : focused as Visual;
        if (visual is null) return false;

        var discarded = RootPanel.Children.Skip(firstDiscarded).ToHashSet();
        return visual.GetSelfAndVisualAncestors().Any(v => v is Control control && discarded.Contains(control));
    }

    private readonly record struct BlockRecord(
        Type BlockType, int SpanStart, int SpanEnd, bool WasOpen,
        int ChildCount, int SelectionEntries, int LinkStops, int LinkTargets,
        int Anchors, int Headings, int Slugs);
}
