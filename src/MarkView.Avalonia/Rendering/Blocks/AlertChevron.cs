// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The fold indicator of a foldable alert's header, and of an HTML details element's summary: a triangle pointing right when collapsed and down
/// when expanded, drawn in the header's text colour and scaled with its font size.
/// </summary>
/// <remarks>
/// Drawn rather than taken from a font (▸/▾), because a browser build only has the fonts the
/// application embeds, which usually lack these symbols.
/// </remarks>
internal sealed class AlertChevron : Control
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AlertChevron, bool>(nameof(IsExpanded));

    // The triangle sits in a square about the height of a capital letter, on the text's baseline.
    private const double BoxSizeRatio = 0.7;

    static AlertChevron()
    {
        AffectsRender<AlertChevron>(IsExpandedProperty, TextElement.ForegroundProperty);
        AffectsMeasure<AlertChevron>(TextElement.FontSizeProperty);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private double BoxSize => Math.Round(GetValue(TextElement.FontSizeProperty) * BoxSizeRatio);

    protected override Size MeasureOverride(Size availableSize) => new(BoxSize, BoxSize);

    public override void Render(DrawingContext context)
    {
        // Without a foreground, the triangle has no brush and nothing is drawn.
        context.DrawGeometry(GetValue(TextElement.ForegroundProperty), null, GetTriangle());
    }

    /// <summary>The triangle, in the control's coordinates.</summary>
    internal PolylineGeometry GetTriangle()
    {
        var size = BoxSize;
        Point[] points = IsExpanded
            ? [new(size * 0.1, size * 0.25), new(size * 0.9, size * 0.25), new(size * 0.5, size * 0.85)]
            : [new(size * 0.25, size * 0.1), new(size * 0.85, size * 0.5), new(size * 0.25, size * 0.9)];
        return new PolylineGeometry(points, isFilled: true);
    }
}
