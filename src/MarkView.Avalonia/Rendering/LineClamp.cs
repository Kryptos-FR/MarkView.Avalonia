// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Keeps the grid that holds a rendered document (its root panel, then the selection and link focus
/// layers) as tall as the document's first <see cref="MaxLines"/> lines (see
/// <see cref="DocumentLines"/>), and clips the rest, unless <see cref="IsExpanded"/>.
/// </summary>
/// <remarks>
/// The cut is found after each layout pass, once the lines are in place, and applied as the grid's
/// <see cref="Layoutable.MaxHeight"/>. The layout pass this queues runs before the frame is rendered,
/// so no frame shows the document uncut. The content keeps its own layout: a grid arranged shorter
/// than its content still places the content's blocks at their full height, so the lines do not move
/// whatever is cut.
/// </remarks>
internal sealed class LineClamp : IDisposable
{
    private readonly Grid _grid;
    private bool _listening;

    public LineClamp(Grid grid) => _grid = grid;

    /// <summary>The number of lines shown, or 0 to show the whole document.</summary>
    public int MaxLines
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Listen(value > 0);
            if (value == 0)
                Apply(exceeds: false, cut: null);
            // The lines are counted again after the next layout pass.
            _grid.InvalidateMeasure();
        }
    }

    /// <summary>Whether the whole document is shown even when it is longer than <see cref="MaxLines"/>.</summary>
    public bool IsExpanded
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            // The cut is known already, so it is applied (or lifted) right away.
            Apply(ExceedsMaxLines, Cut);
        }
    }

    /// <summary>
    /// Whether the document, as last laid out, has more lines than <see cref="MaxLines"/> (whether or
    /// not <see cref="IsExpanded"/> shows them).
    /// </summary>
    public bool ExceedsMaxLines { get; private set; }

    /// <summary>Raised when <see cref="ExceedsMaxLines"/> changes.</summary>
    public event EventHandler? ExceedsMaxLinesChanged;

    /// <summary>The bottom of the last line shown when the document exceeds <see cref="MaxLines"/>, in the grid.</summary>
    private double? Cut { get; set; }

    /// <summary>
    /// How far down the document shows, from the top of the grid: the bottom of the last line shown
    /// while lines are hidden, otherwise infinity.
    /// </summary>
    public double VisibleHeight => ExceedsMaxLines && !IsExpanded && Cut is { } cut ? cut : double.PositiveInfinity;

    /// <summary>
    /// Whether any part of <paramref name="bounds"/>, a rectangle in <paramref name="visual"/>'s
    /// coordinates, is below the lines shown.
    /// </summary>
    public bool Hides(Visual visual, Rect bounds) =>
        !double.IsPositiveInfinity(VisibleHeight)
        && visual.TranslatePoint(bounds.BottomLeft, _grid) is { } bottom
        && bottom.Y > VisibleHeight + 0.5;

    public void Dispose() => Listen(false);

    private void Listen(bool listen)
    {
        if (listen == _listening) return;
        _listening = listen;
        if (listen)
            _grid.LayoutUpdated += OnLayoutUpdated;
        else
            _grid.LayoutUpdated -= OnLayoutUpdated;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var cut = _grid.Children is [Panel root, ..] ? DocumentLines.FindCut(root, _grid, MaxLines) : null;
        Apply(cut is not null, cut);
    }

    private void Apply(bool exceeds, double? cut)
    {
        Cut = cut;
        var exceedsChanged = exceeds != ExceedsMaxLines;
        ExceedsMaxLines = exceeds;
        var visibleHeight = VisibleHeight;
        var clamped = !double.IsPositiveInfinity(visibleHeight);
        // Set only when it changes: a new value queues another layout pass.
        if (!clamped)
            _grid.ClearValue(Layoutable.MaxHeightProperty);
        else if (!_grid.MaxHeight.Equals(visibleHeight))
            _grid.MaxHeight = visibleHeight;
        _grid.ClipToBounds = clamped;
        foreach (var child in _grid.Children)
        {
            if (child is LinkFocusLayer linkLayer)
                linkLayer.VisibleHeight = visibleHeight;
        }

        if (exceedsChanged)
            ExceedsMaxLinesChanged?.Invoke(this, EventArgs.Empty);
    }
}
