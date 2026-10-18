// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// The icon of an image that could not be loaded: the two halves of a torn picture, drawn in the
/// text colour and scaled with the font size.
/// </summary>
/// <remarks>
/// Drawn rather than taken from a font, because a browser build only has the fonts the application
/// embeds, which usually lack such symbols.
/// </remarks>
internal sealed class BrokenImageGlyph : Control
{
    // In a 16-unit square: the left half with a hill, then the right half, torn along the same zigzag.
    internal const string PathData = "M2,2 H9 L7,7 L10,10 L8,14 H2 Z M3.5,12 L5.5,9 L7,11 M11,2 H14 V14 H10 L12,10 L9,7 Z";
    internal const double DesignSize = 16;
    internal const double StrokeThickness = 1.25;

    // A little taller than a capital letter, like an icon set in the text.
    private const double BoxSizeRatio = 1.15;

    private Geometry? _geometry;

    static BrokenImageGlyph()
    {
        AffectsRender<BrokenImageGlyph>(TextElement.ForegroundProperty);
        AffectsMeasure<BrokenImageGlyph>(TextElement.FontSizeProperty);
    }

    internal double BoxSize => Math.Round(GetValue(TextElement.FontSizeProperty) * BoxSizeRatio);

    internal Geometry Geometry => _geometry ??= Geometry.Parse(PathData);

    protected override Size MeasureOverride(Size availableSize) => new(BoxSize, BoxSize);

    // A picture, like an image: not mirrored in right-to-left text.
    protected override bool BypassFlowDirectionPolicies => true;

    public override void Render(DrawingContext context)
    {
        var scale = BoxSize / DesignSize;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            context.DrawGeometry(null, new Pen(GetValue(TextElement.ForegroundProperty), StrokeThickness), Geometry);
    }
}
