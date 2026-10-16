// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class AlertChevronTests
{
    private static AlertChevron Chevron(bool isExpanded, double fontSize = 20)
    {
        var chevron = new AlertChevron { IsExpanded = isExpanded };
        chevron.SetValue(TextElement.FontSizeProperty, fontSize);
        chevron.SetValue(TextElement.ForegroundProperty, Brushes.Red);
        return chevron;
    }

    private static List<GeometryDrawing> Draw(Control control)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            control.Render(context);
        return group.Children.OfType<GeometryDrawing>().ToList();
    }

    [AvaloniaFact]
    public void Is_a_square_of_seven_tenths_of_the_font_size()
    {
        var chevron = Chevron(false, fontSize: 20);
        chevron.Measure(Size.Infinity);
        Assert.Equal(new Size(14, 14), chevron.DesiredSize);
    }

    [AvaloniaFact]
    public void Collapsed_points_right()
    {
        // 20 px: a 14 px square.
        Assert.Equal(
            [new Point(14 * 0.25, 14 * 0.1), new Point(14 * 0.85, 14 * 0.5), new Point(14 * 0.25, 14 * 0.9)],
            Chevron(false).GetTriangle().Points);
    }

    [AvaloniaFact]
    public void Expanded_points_down()
    {
        Assert.Equal(
            [new Point(14 * 0.1, 14 * 0.25), new Point(14 * 0.9, 14 * 0.25), new Point(14 * 0.5, 14 * 0.85)],
            Chevron(true).GetTriangle().Points);
    }

    [AvaloniaFact]
    public void Triangle_is_filled_with_the_foreground_and_has_no_outline()
    {
        var chevron = Chevron(false);
        Assert.True(chevron.GetTriangle().IsFilled);

        var drawing = Assert.Single(Draw(chevron));
        Assert.Same(Brushes.Red, drawing.Brush);
        Assert.Null(drawing.Pen);
    }

    [AvaloniaFact]
    public void Draws_nothing_without_a_foreground()
    {
        var chevron = Chevron(false);
        chevron.SetValue(TextElement.ForegroundProperty, null);
        Assert.Empty(Draw(chevron));
    }

    [AvaloniaFact]
    public void Takes_the_font_size_and_foreground_of_the_header_it_is_in()
    {
        var chevron = new AlertChevron();
        var header = new TextBlock { FontSize = 30, Foreground = Brushes.Green };
        header.Inlines!.Add(new InlineUIContainer(chevron));
        var window = new Window { Content = header };
        window.Show();
        try
        {
            Assert.Equal(new Size(21, 21), chevron.DesiredSize);
            Assert.Same(Brushes.Green, Assert.Single(Draw(chevron)).Brush);
        }
        finally
        {
            window.Close();
        }
    }
}
