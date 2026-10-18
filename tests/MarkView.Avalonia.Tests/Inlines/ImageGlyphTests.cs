// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

/// <summary>
/// The symbols drawn for images: the broken-image glyph of a failed image, the lightbox's close cross
/// and the zoom-in cursor.
/// </summary>
public class ImageGlyphTests
{
    private static T Styled<T>(T control, double fontSize = 20) where T : Control
    {
        control.SetValue(TextElement.FontSizeProperty, fontSize);
        control.SetValue(TextElement.ForegroundProperty, Brushes.Red);
        return control;
    }

    private static DrawingGroup Draw(Control control)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            control.Render(context);
        return group;
    }

    [AvaloniaTheory]
    [InlineData(14, 16)]
    [InlineData(20, 23)]
    public void The_broken_image_glyph_is_a_square_a_little_taller_than_the_font_size(double fontSize, double size)
    {
        var glyph = Styled(new BrokenImageGlyph(), fontSize);

        glyph.Measure(Size.Infinity);

        Assert.Equal(new Size(size, size), glyph.DesiredSize);
    }

    [AvaloniaFact]
    public void The_broken_image_glyph_is_measured_again_when_the_font_size_changes()
    {
        var glyph = Styled(new BrokenImageGlyph());
        glyph.Measure(Size.Infinity);

        glyph.SetValue(TextElement.FontSizeProperty, 10);

        Assert.False(glyph.IsMeasureValid);
    }

    [AvaloniaFact]
    public void The_broken_image_glyph_strokes_its_outline_in_the_foreground_scaled_to_its_size()
    {
        var glyph = Styled(new BrokenImageGlyph(), fontSize: 14);

        var group = Draw(glyph);

        var scaled = Assert.IsType<DrawingGroup>(Assert.Single(group.Children));
        Assert.Equal(Matrix.CreateScale(1, 1), scaled.Transform!.Value);
        var drawing = Assert.IsType<GeometryDrawing>(Assert.Single(scaled.Children));
        Assert.Null(drawing.Brush);
        Assert.Same(Brushes.Red, drawing.Pen!.Brush);
        Assert.Equal(1.25, drawing.Pen.Thickness);
        Assert.Equal(new Rect(2, 2, 12, 12), drawing.Geometry!.Bounds);
        Assert.Same(glyph.Geometry, glyph.Geometry);
        Assert.Equal(new Rect(2, 2, 12, 12), glyph.Geometry.Bounds);
    }

    [AvaloniaFact]
    public void The_broken_image_glyph_scales_its_16_unit_drawing_to_its_size()
    {
        var glyph = Styled(new BrokenImageGlyph(), fontSize: 28);

        var scaled = Assert.IsType<DrawingGroup>(Assert.Single(Draw(glyph).Children));

        // 28 × 1.15 rounds to 32: twice the drawing's 16 units.
        Assert.Equal(Matrix.CreateScale(2, 2), scaled.Transform!.Value);
    }

    [AvaloniaFact]
    public void The_close_cross_is_a_square_of_the_font_size_drawn_with_two_round_capped_strokes()
    {
        var cross = Styled(new ImageLightbox.CloseGlyph(), fontSize: 20);
        cross.Measure(Size.Infinity);
        Assert.Equal(new Size(20, 20), cross.DesiredSize);

        var lines = Draw(cross).Children.Cast<GeometryDrawing>().ToList();

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line =>
        {
            Assert.Same(Brushes.Red, line.Pen!.Brush);
            Assert.Equal(2.5, line.Pen.Thickness);
            Assert.Equal(PenLineCap.Round, line.Pen.LineCap);
        });
        Assert.All(lines, line => Assert.Equal(new Rect(4, 4, 12, 12), line.Geometry!.Bounds));
        Assert.Equal(
            [(new Point(4, 4), new Point(16, 16)), (new Point(16, 4), new Point(4, 16))],
            cross.GetStrokes());
    }

    [AvaloniaFact]
    public void The_close_cross_rounds_its_size_to_whole_pixels()
    {
        var cross = Styled(new ImageLightbox.CloseGlyph(), fontSize: 20.6);

        cross.Measure(Size.Infinity);

        Assert.Equal(new Size(21, 21), cross.DesiredSize);
    }

    [AvaloniaFact]
    public void The_close_cross_is_measured_again_when_the_font_size_changes()
    {
        var cross = Styled(new ImageLightbox.CloseGlyph());
        cross.Measure(Size.Infinity);

        cross.SetValue(TextElement.FontSizeProperty, 10);

        Assert.False(cross.IsMeasureValid);
    }

    [AvaloniaFact]
    public void The_zoom_glyphs_are_a_plus_a_minus_and_four_corners()
    {
        var zoomIn = Styled(new ImageLightbox.ZoomGlyph(ImageLightbox.ZoomGlyphKind.ZoomIn), fontSize: 20);
        var zoomOut = Styled(new ImageLightbox.ZoomGlyph(ImageLightbox.ZoomGlyphKind.ZoomOut), fontSize: 20);
        var fit = Styled(new ImageLightbox.ZoomGlyph(ImageLightbox.ZoomGlyphKind.Fit), fontSize: 20);
        zoomIn.Measure(Size.Infinity);
        Assert.Equal(new Size(20, 20), zoomIn.DesiredSize);

        Assert.Equal([(new Point(4, 10), new Point(16, 10)), (new Point(10, 4), new Point(10, 16))], zoomIn.GetStrokes());
        Assert.Equal([(new Point(4, 10), new Point(16, 10))], zoomOut.GetStrokes());
        Assert.Equal(
            [
                (new Point(3, 8), new Point(3, 3)), (new Point(3, 3), new Point(8, 3)),
                (new Point(12, 3), new Point(17, 3)), (new Point(17, 3), new Point(17, 8)),
                (new Point(17, 12), new Point(17, 17)), (new Point(17, 17), new Point(12, 17)),
                (new Point(8, 17), new Point(3, 17)), (new Point(3, 17), new Point(3, 12)),
            ],
            fit.GetStrokes().Select(s => (Round(s.Start), Round(s.End))));
        var lines = Draw(fit).Children.Cast<GeometryDrawing>().ToList();
        Assert.Equal(8, lines.Count);
        Assert.All(lines, line => Assert.Same(Brushes.Red, line.Pen!.Brush));

        static Point Round(Point p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));
    }

    [AvaloniaFact]
    public void The_zoom_cursor_is_drawn_from_its_pixel_map()
    {
        Assert.All(ZoomCursor.Pixels, row => Assert.Equal(ZoomCursor.Pixels.Length, row.Length));
        Assert.Equal(new PixelPoint(8, 8), ZoomCursor.HotSpot);
        // The hot spot is the centre of the plus, inside the lens.
        Assert.Equal('#', ZoomCursor.Pixels[8][8]);

        var pixels = ZoomCursor.GetPixels();
        uint At(int x, int y) => unchecked((uint)pixels[y * 22 + x]);
        Assert.Equal(22 * 22, pixels.Length);
        Assert.Equal(0xFF000000, At(8, 8));
        Assert.Equal(0xFFFFFFFF, At(5, 1));
        Assert.Equal(0u, At(0, 0));
        Assert.Equal(0xFF000000, At(19, 18));
        Assert.Equal(0xFFFFFFFF, At(20, 20));
        Assert.Equal(0u, At(21, 21));

        // The headless platform keeps neither the pixels nor the size of a bitmap.
        using var bitmap = ZoomCursor.CreateBitmap();
        Assert.NotNull(bitmap);
    }

    [AvaloniaFact]
    public void The_zoom_cursor_is_created_once()
    {
        Assert.Same(ZoomCursor.Instance, ZoomCursor.Instance);
    }
}
