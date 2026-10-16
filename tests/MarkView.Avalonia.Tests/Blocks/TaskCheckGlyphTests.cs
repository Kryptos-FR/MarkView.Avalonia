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
    public void Is_at_least_as_wide_as_the_box_and_its_padding()
    {
        // The box is 0.8 em: 22 px at 28 px.
        var glyph = Laid(false, padding: new Thickness(3, 0, 5, 0));
        Assert.True(glyph.DesiredSize.Width >= 22 + 3 + 5);
    }

    [AvaloniaFact]
    public void Unchecked_draws_a_rounded_box_in_the_foreground_centred_on_the_lowercase_middle()
    {
        var glyph = Laid(false);
        var drawing = Assert.Single(Draw(glyph));

        Assert.Null(drawing.Brush);
        Assert.Same(Brushes.Black, drawing.Pen!.Brush);
        Assert.Equal(2, drawing.Pen.Thickness);  // 28 / 14

        var left = (glyph.Bounds.Width - 22) / 2;
        var top = glyph.TextLayout.TextLines[0].Baseline - 28 * 0.3 - 11;
        AssertBounds(new Rect(left + 1, top + 1, 20, 20), drawing.Geometry!);
    }

    [AvaloniaFact]
    public void Checked_also_draws_a_round_tick_inside_the_box()
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

        var left = (glyph.Bounds.Width - 22) / 2;
        var top = glyph.TextLayout.TextLines[0].Baseline - 28 * 0.3 - 11;
        // The tick runs from (0.24, 0.52) down to (0.43, 0.70), then up to (0.76, 0.32), in box units.
        AssertBounds(new Rect(new Point(left + 22 * 0.24, top + 22 * 0.32), new Point(left + 22 * 0.76, top + 22 * 0.70)), tick.Geometry!);
    }

    [AvaloniaFact]
    public void Box_is_offset_by_the_padding()
    {
        var glyph = Laid(false, fontSize: 28, padding: new Thickness(4, 6, 0, 0));
        var box = Assert.Single(Draw(glyph)).Geometry!;

        var left = 4 + (glyph.Bounds.Width - 4 - 22) / 2;
        var top = 6 + Math.Max(0, glyph.TextLayout.TextLines[0].Baseline - 28 * 0.3 - 11);
        AssertBounds(new Rect(left + 1, top + 1, 20, 20), box);
    }

    [AvaloniaFact]
    public void Small_font_keeps_a_one_pixel_stroke()
    {
        var glyph = Laid(false, fontSize: 10);
        Assert.Equal(1, Assert.Single(Draw(glyph)).Pen!.Thickness);
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
