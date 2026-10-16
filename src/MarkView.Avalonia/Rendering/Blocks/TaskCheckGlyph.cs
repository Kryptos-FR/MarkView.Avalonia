// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The check glyph of a task-list item. Its text stays ☑ or ☐, which is what selection copies,
/// but it draws a box (ticked when checked) instead of the font's glyph.
/// </summary>
/// <remarks>
/// A browser build only has the fonts the application embeds, and those usually lack these
/// symbols: both states would show as the same missing-glyph box. Drawing the box keeps the two
/// states apart whatever the fonts. It is styled like any <see cref="TextBlock"/>: the box takes
/// the <see cref="TextBlock.Foreground"/> and scales with the <see cref="TextBlock.FontSize"/>.
/// </remarks>
internal sealed class TaskCheckGlyph : TextBlock
{
    public const string CheckedGlyph = "☑";
    public const string UncheckedGlyph = "☐";

    // The box spans about the height of a capital letter, and is centred on the middle of a
    // lowercase letter.
    private const double BoxSizeRatio = 0.8;
    private const double CenterAboveBaselineRatio = 0.3;

    public TaskCheckGlyph(bool isChecked)
    {
        IsChecked = isChecked;
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public bool IsChecked
    {
        get => Text == CheckedGlyph;
        set => Text = value ? CheckedGlyph : UncheckedGlyph;
    }

    private double BoxSize => Math.Round(FontSize * BoxSizeRatio);

    protected override Size MeasureOverride(Size availableSize)
    {
        // The font's glyph (or its missing-glyph box) may be narrower than the drawn box.
        var size = base.MeasureOverride(availableSize);
        return size.WithWidth(Math.Max(size.Width, BoxSize + Padding.Left + Padding.Right));
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        if (Foreground is not { } brush)
            return;

        var size = BoxSize;
        var thickness = Math.Max(1, Math.Round(FontSize / 14));
        var baseline = TextLayout.TextLines.Count > 0 ? TextLayout.TextLines[0].Baseline : TextLayout.Baseline;
        var width = Bounds.Width - Padding.Left - Padding.Right;
        var left = origin.X + Math.Max(0, (width - size) / 2);
        var top = origin.Y + Math.Max(0, baseline - FontSize * CenterAboveBaselineRatio - size / 2);

        // Inset by half the pen so the stroke stays inside the box.
        var box = new Rect(left, top, size, size).Deflate(thickness / 2);
        var radius = size * 0.2;
        context.DrawRectangle(null, new Pen(brush, thickness), box, radius, radius);

        if (!IsChecked)
            return;

        var tick = new PolylineGeometry(
            [
                new Point(left + size * 0.24, top + size * 0.52),
                new Point(left + size * 0.43, top + size * 0.70),
                new Point(left + size * 0.76, top + size * 0.32),
            ],
            isFilled: false);
        context.DrawGeometry(null, new Pen(brush, thickness * 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), tick);
    }
}
