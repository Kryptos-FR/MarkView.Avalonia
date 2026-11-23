// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Specialized;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// The root panel of a document shown with <see cref="MarkdownViewer.IsVirtualizationEnabled"/>. It stacks the
/// top-level blocks as a <see cref="StackPanel"/> does, and its <see cref="Panel.Children"/> holds every one of
/// them, so that whatever reads the document (selection, find, links, folding, streaming) sees all of it. Only
/// the blocks within one viewport height of the visible area are attached to it, though: styled, laid out and
/// drawn. The others take the height they had when last laid out, or one estimated from their source, and are
/// attached as they come near; once more than three viewport heights away, they are detached again.
/// </summary>
/// <remarks>
/// The block that holds the focus stays attached wherever it is, and so does the one <see cref="Realize"/> last
/// attached (the target of a jump), until it comes into view. Child indexes are those of
/// <see cref="Panel.Children"/>, attached or not, so a theme's <c>:nth-child</c> selectors see the document's
/// order.
/// <para>
/// The document's last blocks may not be built yet (see <see cref="Pending"/>): they take a height estimated from
/// their source after the children, and once a measure finds some of them within the band it attaches, the
/// arrange that follows has them built, which adds them to the children and has the panel measured again in the
/// same layout pass, before anything is drawn.
/// </para>
/// </remarks>
internal sealed class VirtualizingBlockPanel : StackPanel, IChildIndexProvider
{
    // Estimated pixels per unit of weight (see Weight) before any block has been laid out.
    private const double DefaultPixelsPerUnit = 20;

    // Characters per line assumed for a paragraph: one per this many pixels of width.
    private const double CharacterWidth = 8;

    private readonly Dictionary<Control, Slot> _slots = new(ReferenceEqualityComparer.Instance);

    // The attached blocks, in document order: the panel's visual and logical children.
    private readonly List<Control> _attached = [];

    // What the heights of the blocks laid out so far say about their weight, per type of top-level block and
    // in all, to estimate the blocks never laid out.
    private readonly Dictionary<Type, (double Height, double Weight)> _samples = [];
    private (double Height, double Weight) _allSamples;

    private EventHandler<ChildIndexChangedEventArgs>? _childIndexChanged;
    private Rect? _viewport;
    private double _width = double.NaN;
    private double _desiredWidth;

    // The band, in the panel, whose blocks the last measure attached: a scroll that keeps half a viewport of
    // it beyond the visible area needs no new measure.
    private double _bandTop;
    private double _bandBottom = double.NegativeInfinity;
    private Control? _pinned;

    // How many pending blocks the last measure found within the band, for the arrange that follows to build.
    private int _buildCount;

    // The weight (see Weight) of each pending block at _weightsWidth.
    private readonly Dictionary<Block, double> _pendingWeights = new(ReferenceEqualityComparer.Instance);
    private double _weightsWidth = double.NaN;

    public VirtualizingBlockPanel()
    {
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    /// <summary>
    /// The top-level blocks that follow the children but are not built yet, in document order, and the means to build
    /// them, or <c>null</c> when every block is built.
    /// </summary>
    internal IPendingBlocks? Pending
    {
        get;
        set
        {
            field = value;
            _pendingWeights.Clear();
            InvalidateMeasure();
        }
    }

    /// <summary>The attached blocks, in document order.</summary>
    internal IReadOnlyList<Control> AttachedChildren => _attached;

    /// <summary>The blocks that are not attached, in document order.</summary>
    internal IEnumerable<Control> DetachedChildren => Children.Where(child => !_slots[child].IsAttached);

    /// <summary>Whether <paramref name="block"/>, one of <see cref="Panel.Children"/>, is attached.</summary>
    internal bool IsAttached(Control block) => _slots.TryGetValue(block, out var slot) && slot.IsAttached;

    /// <summary>
    /// The child of the panel that holds <paramref name="visual"/>, attached or not, or <c>null</c> when it
    /// is not part of the document.
    /// </summary>
    internal Control? BlockOf(Visual visual)
    {
        var block = visual;
        while (block.GetVisualParent() is { } parent && parent != this)
            block = parent;
        return block is Control control && _slots.ContainsKey(control) ? control : null;
    }

    /// <summary>
    /// Attaches the block that holds <paramref name="visual"/> now, wherever it is, and keeps it attached until
    /// it has come into view, so that it can be laid out and scrolled to. Returns <c>true</c> when it was not
    /// attached.
    /// </summary>
    internal bool Realize(Visual visual)
    {
        if (BlockOf(visual) is not { } block)
            return false;
        _pinned = block;
        if (_slots[block].IsAttached)
            return false;

        var index = Children.IndexOf(block);
        var position = 0;
        while (position < _attached.Count && Children.IndexOf(_attached[position]) < index)
            position++;
        Attach(block, position);
        InvalidateMeasure();
        return true;
    }

    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The children are the document; only the attached ones are visual and logical children. A Controls
        // collection raises no Reset: Clear removes its items.
        foreach (Control child in e.OldItems ?? Array.Empty<Control>())
        {
            if (_slots[child].IsAttached)
                Detach(child);
            _slots.Remove(child);
        }
        foreach (Control child in e.NewItems ?? Array.Empty<Control>())
            _slots[child] = new Slot();

        _childIndexChanged?.Invoke(this, ChildIndexChangedEventArgs.ChildIndexesReset);
        InvalidateMeasure();
    }

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var viewport = e.EffectiveViewport;
        // Out of view (e.g. a hidden tab): what is attached stays as it is.
        if (viewport.Height <= 0)
            return;
        var previous = _viewport;
        _viewport = viewport;
        if (previous is not { } last || last.Height != viewport.Height
            || viewport.Top - viewport.Height / 2 < _bandTop || viewport.Bottom + viewport.Height / 2 > _bandBottom)
            InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        if (width != _width)
        {
            _width = width;
            _desiredWidth = 0;
        }

        // Until the panel is told what is in view, the top of the document is.
        var viewport = _viewport ?? new Rect(0, 0, 0, TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 1000);
        var lead = viewport.Height;
        double attachTop = viewport.Top - lead, attachBottom = viewport.Bottom + lead;
        double keepTop = viewport.Top - 3 * lead, keepBottom = viewport.Bottom + 3 * lead;
        _bandTop = attachTop;
        _bandBottom = attachBottom;

        var focused = FocusedBlock();
        var constraint = new Size(width, double.PositiveInfinity);
        var spacing = Spacing;
        double y = 0;
        var hasVisible = false;
        var position = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var slot = _slots[child];
            var isVisible = child.IsVisible;
            var top = y + (hasVisible && isVisible ? spacing : 0);
            var estimate = isVisible ? EstimatedHeight(child, slot, width) : 0;
            var near = isVisible && top + estimate >= attachTop && top <= attachBottom;
            if (near && child == _pinned && top + estimate >= viewport.Top && top <= viewport.Bottom)
                _pinned = null;

            var attach = near || child == _pinned || child == focused
                || (slot.IsAttached && isVisible && top + estimate >= keepTop && top <= keepBottom);
            var height = estimate;
            if (attach)
            {
                if (!slot.IsAttached)
                    Attach(child, position);
                position++;
                child.Measure(constraint);
                height = isVisible ? child.DesiredSize.Height : 0;
                if (isVisible)
                {
                    Learn(child, slot, width, height);
                    _desiredWidth = Math.Max(_desiredWidth, child.DesiredSize.Width);
                }
            }
            else if (slot.IsAttached)
            {
                Detach(child);
            }

            slot.Top = top;
            if (isVisible)
            {
                y = top + height;
                hasVisible = true;
            }
        }

        // The blocks not built yet: those that start within the band are built by the arrange that follows.
        _buildCount = 0;
        if (Pending is { Count: > 0 } pending)
        {
            if (_weightsWidth != width)
            {
                _weightsWidth = width;
                _pendingWeights.Clear();
            }
            for (var p = 0; p < pending.Count; p++)
            {
                var block = pending[p];
                var top = y + (hasVisible ? spacing : 0);
                if (top <= attachBottom)
                    _buildCount = p + 1;
                y = top + PendingWeight(block, width) * PixelsPerUnit(block.GetType());
                hasVisible = true;
            }
        }
        return new Size(_desiredWidth, y);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in _attached)
        {
            if (!child.IsVisible)
                continue;
            var size = child.DesiredSize;
            child.Arrange(new Rect(0, _slots[child].Top, Math.Max(finalSize.Width, size.Width), size.Height));
        }

        // Built now, the blocks are added to the children, which has the panel measured again within this layout pass.
        if (_buildCount > 0 && Pending is { } pending)
        {
            var count = _buildCount;
            _buildCount = 0;
            pending.Build(count);
        }
        return finalSize;
    }

    // The attached block that holds the focus: a link's focus target stands for its text.
    private Control? FocusedBlock()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var visual = focused is LinkFocusTarget target ? target.TextBlock : focused as Visual;
        return visual is not null && this.IsVisualAncestorOf(visual) ? BlockOf(visual) : null;
    }

    private void Attach(Control child, int position)
    {
        _slots[child].IsAttached = true;
        _attached.Insert(position, child);
        LogicalChildren.Insert(position, child);
        VisualChildren.Insert(position, child);
    }

    private void Detach(Control child)
    {
        var position = _attached.IndexOf(child);
        _slots[child].IsAttached = false;
        _attached.RemoveAt(position);
        VisualChildren.RemoveAt(position);
        LogicalChildren.RemoveAt(position);
    }

    // The height a block not laid out at this width is taken to have: the height it had last time, or what the
    // blocks laid out so far say about its weight.
    private double EstimatedHeight(Control child, Slot slot, double width)
    {
        if (!double.IsNaN(slot.Height))
            return slot.Height;
        return WeightOf(child, slot, width) * PixelsPerUnit(TypeOf(child));
    }

    // How tall a unit of weight is in a block of type type, by what the blocks laid out so far say.
    private double PixelsPerUnit(Type type)
    {
        var samples = _samples.TryGetValue(type, out var ofType) ? ofType : _allSamples;
        return samples.Weight > 0 ? samples.Height / samples.Weight : DefaultPixelsPerUnit;
    }

    private double PendingWeight(Block block, double width)
    {
        if (!_pendingWeights.TryGetValue(block, out var weight))
            _pendingWeights[block] = weight = Weight(block, CharactersPerLine(width));
        return weight;
    }

    private static double CharactersPerLine(double width) => Math.Max(20, width / CharacterWidth);

    // Records the height a block was laid out with; the first at a width also tells how tall a unit of weight is.
    private void Learn(Control child, Slot slot, double width, double height)
    {
        var first = slot.Width != width;
        slot.Width = width;
        slot.Height = height;
        if (!first)
            return;
        var weight = WeightOf(child, slot, width);
        var type = TypeOf(child);
        _samples.TryGetValue(type, out var samples);
        _samples[type] = (samples.Height + height, samples.Weight + weight);
        _allSamples = (_allSamples.Height + height, _allSamples.Weight + weight);
    }

    private static double WeightOf(Control child, Slot slot, double width)
    {
        if (slot.WeightWidth != width)
        {
            slot.Weight = AvaloniaRenderer.GetTopLevelBlock(child) is { } block
                ? Weight(block, CharactersPerLine(width))
                : 1;
            slot.WeightWidth = width;
        }
        return slot.Weight;
    }

    private static Type TypeOf(Control child) => AvaloniaRenderer.GetTopLevelBlock(child)?.GetType() ?? child.GetType();

    /// <summary>
    /// How tall <paramref name="block"/> is, in a unit whose size in pixels the blocks laid out tell: a line of
    /// code or of wrapped text (<paramref name="charactersPerLine"/> to a line) is one, and so is the space
    /// around each block. A table row counts as its tallest cell.
    /// </summary>
    internal static double Weight(Block block, double charactersPerLine) => block switch
    {
        CodeBlock code => 1 + code.Lines.Count,
        LeafBlock leaf => 1 + Math.Max(1, Math.Ceiling(leaf.Span.Length / charactersPerLine)),
        TableRow row => row.Count == 0 ? 1 : row.Max(cell => Weight(cell, charactersPerLine)),
        ContainerBlock container => 1 + container.Sum(child => Weight(child, charactersPerLine)),
        _ => 1,
    };

    int IChildIndexProvider.GetChildIndex(ILogical child) => child is Control control ? Children.IndexOf(control) : -1;

    bool IChildIndexProvider.TryGetTotalCount(out int count)
    {
        count = Children.Count;
        return true;
    }

    event EventHandler<ChildIndexChangedEventArgs>? IChildIndexProvider.ChildIndexChanged
    {
        add => _childIndexChanged += value;
        remove => _childIndexChanged -= value;
    }

    /// <summary>The top-level blocks of a document that are not built yet, in document order.</summary>
    internal interface IPendingBlocks
    {
        /// <summary>How many blocks are not built yet.</summary>
        int Count { get; }

        /// <summary>The block at <paramref name="index"/> among those not built yet.</summary>
        Block this[int index] { get; }

        /// <summary>Builds the first <paramref name="count"/> blocks, adding their controls to the panel's children.</summary>
        void Build(int count);
    }

    private sealed class Slot
    {
        public bool IsAttached;

        // Where the block starts, as of the last measure.
        public double Top;

        // The height the block had when last laid out, and the width it was laid out at (NaN: never).
        public double Height = double.NaN;
        public double Width = double.NaN;

        // The block's weight (see Weight) at a width, computed once per width.
        public double Weight;
        public double WeightWidth = double.NaN;
    }
}
