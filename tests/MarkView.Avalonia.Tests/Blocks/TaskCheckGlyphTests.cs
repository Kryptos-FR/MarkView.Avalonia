// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class TaskCheckGlyphTests
{
    private static TaskCheckGlyph Laid(bool isChecked, double fontSize = 28, IBrush? foreground = null, Thickness padding = default)
    {
        var glyph = new TaskCheckGlyph(isChecked) { FontSize = fontSize, Foreground = foreground ?? Brushes.Black, Padding = padding };
        glyph.Measure(Size.Infinity);
        glyph.Arrange(new Rect(glyph.DesiredSize));
        return glyph;
    }

    private static List<GeometryDrawing> Draw(TaskCheckGlyph glyph)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            glyph.Render(context);
        return Flatten(group).ToList();
    }

    private static IEnumerable<GeometryDrawing> Flatten(Drawing drawing) => drawing switch
    {
        DrawingGroup g => g.Children.SelectMany(Flatten),
        GeometryDrawing d => [d],
        _ => [],
    };

    private static void AssertBounds(Rect expected, Geometry geometry)
    {
        var actual = geometry.Bounds;
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Width, actual.Width, 4);
        Assert.Equal(expected.Height, actual.Height, 4);
    }

    [AvaloniaFact]
    public void Text_is_the_check_glyph_of_its_state()
    {
        var glyph = new TaskCheckGlyph(true);
        Assert.True(glyph.IsChecked);
        Assert.Equal("☑", glyph.Text);

        glyph.IsChecked = false;
        Assert.False(glyph.IsChecked);
        Assert.Equal("☐", glyph.Text);
    }

    [AvaloniaFact]
    public void Is_styled_as_a_TextBlock()
    {
        var root = new StackPanel();
        root.Styles.Add(new Style(x => x.OfType<TextBlock>().Class("marker"))
        {
            Setters = { new Setter(TextBlock.FontSizeProperty, 31.0) },
        });
        var glyph = new TaskCheckGlyph(false);
        glyph.Classes.Add("marker");
        root.Children.Add(glyph);
        var window = new Window { Content = root };
        window.Show();
        try
        {
            Assert.Equal(31.0, glyph.FontSize);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Is_as_wide_as_the_box_and_its_padding_when_the_text_is_narrower()
    {
        // The box is 0.8 em: 22 px at 28 px.
        var glyph = new TaskCheckGlyph(false) { FontSize = 28, Padding = new Thickness(3, 0, 5, 0) };
        glyph.Text = "";
        glyph.Measure(Size.Infinity);
        Assert.Equal(22 + 3 + 5, glyph.DesiredSize.Width);
    }

    [AvaloniaFact]
    public void Is_as_wide_as_the_text_and_its_padding_when_the_text_is_wider()
    {
        var reference = new TextBlock { FontSize = 28, Padding = new Thickness(3, 0, 5, 0), Text = "☐☐☐☐" };
        reference.Measure(Size.Infinity);
        var glyph = new TaskCheckGlyph(false) { FontSize = 28, Padding = new Thickness(3, 0, 5, 0) };
        glyph.Text = "☐☐☐☐";
        glyph.Measure(Size.Infinity);

        Assert.True(reference.DesiredSize.Width > 22 + 3 + 5);
        Assert.Equal(reference.DesiredSize.Width, glyph.DesiredSize.Width);
    }

    [AvaloniaFact]
    public void Box_is_centred_on_the_lowercase_middle_and_inset_by_half_the_pen()
    {
        var glyph = Laid(false, padding: new Thickness(4, 6, 2, 0));
        var shape = glyph.GetShape(new Point(4, 6));

        // 28 px: a 22 px box, a 2 px pen, a 4.4 px corner radius.
        var left = 4 + (glyph.Bounds.Width - 4 - 2 - 22) / 2;
        var top = 6 + glyph.TextLayout.Baseline - 28 * 0.3 - 11;
        Assert.Equal(2, shape.Thickness);
        Assert.Equal(22 * 0.2, shape.CornerRadius, 6);
        Assert.Equal(new Rect(left + 1, top + 1, 20, 20), shape.Outline);
        Assert.Equal(
            [
                new Point(left + 22 * 0.24, top + 22 * 0.52),
                new Point(left + 22 * 0.43, top + 22 * 0.70),
                new Point(left + 22 * 0.76, top + 22 * 0.32),
            ],
            shape.Tick);
    }

    [AvaloniaFact]
    public void Box_starts_at_the_origin_when_arranged_narrower_than_it()
    {
        var glyph = Laid(false);
        glyph.Arrange(new Rect(0, 0, 10, glyph.DesiredSize.Height));
        Assert.Equal(1, glyph.GetShape(default).Outline.X);
    }

    [AvaloniaFact]
    public void Unchecked_draws_only_the_box_with_the_foreground_pen()
    {
        var glyph = Laid(false);
        var drawing = Assert.Single(Draw(glyph));

        Assert.Null(drawing.Brush);
        Assert.Same(Brushes.Black, drawing.Pen!.Brush);
        Assert.Equal(2, drawing.Pen.Thickness);
        AssertBounds(glyph.GetShape(default).Outline, drawing.Geometry!);
    }

    [AvaloniaFact]
    public void Checked_also_draws_the_tick_with_a_thicker_round_pen()
    {
        var glyph = Laid(true);
        var drawings = Draw(glyph);
        Assert.Equal(2, drawings.Count);

        var tick = drawings[1];
        Assert.Null(tick.Brush);
        Assert.Same(Brushes.Black, tick.Pen!.Brush);
        Assert.Equal(3, tick.Pen.Thickness);
        Assert.Equal(PenLineCap.Round, tick.Pen.LineCap);
        Assert.Equal(PenLineJoin.Round, tick.Pen.LineJoin);

        var points = glyph.GetShape(default).Tick;
        AssertBounds(new Rect(new Point(points[0].X, points[2].Y), new Point(points[2].X, points[1].Y)), tick.Geometry!);
    }

    [AvaloniaFact]
    public void Small_font_keeps_a_one_pixel_stroke()
    {
        var glyph = Laid(false, fontSize: 10);
        Assert.Equal(1, glyph.GetShape(default).Thickness);
    }

    [AvaloniaFact]
    public void Draws_nothing_without_a_foreground()
    {
        var glyph = new TaskCheckGlyph(true) { FontSize = 28 };
        glyph.Foreground = null;
        glyph.Measure(Size.Infinity);
        glyph.Arrange(new Rect(glyph.DesiredSize));
        Assert.Empty(Draw(glyph));
    }
}
