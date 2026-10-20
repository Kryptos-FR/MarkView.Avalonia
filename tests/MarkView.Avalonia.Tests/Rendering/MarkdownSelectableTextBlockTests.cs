using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.Media;
using global::Avalonia.VisualTree;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class MarkdownSelectableTextBlockTests
{
    [Fact]
    public void Is_not_a_SelectableTextBlock()
    {
        Assert.False(typeof(MarkdownSelectableTextBlock).IsSubclassOf(typeof(SelectableTextBlock)));
    }

    [Fact]
    public void Is_a_TextBlock()
    {
        Assert.True(typeof(MarkdownSelectableTextBlock).IsSubclassOf(typeof(TextBlock)));
    }

    [Fact]
    public void ExtractPlainText_extracts_runs()
    {
        var inlines = new InlineCollection();
        inlines.Add(new Run("Hello "));
        inlines.Add(new Run("World"));
        var result = MarkdownSelectableTextBlock.ExtractPlainText(inlines);
        Assert.Equal("Hello World", result);
    }

    private static MarkdownSelectableTextBlock Arranged(Thickness border, double width = 100, double height = 40)
    {
        var block = new MarkdownSelectableTextBlock { Text = "Heading", BorderThickness = border };
        block.Measure(new Size(width, height));
        block.Arrange(new Rect(0, 0, width, height));
        return block;
    }

    [AvaloniaFact]
    public void Has_no_border_by_default()
    {
        var block = new MarkdownSelectableTextBlock();

        Assert.Null(block.BorderBrush);
        Assert.Equal(default, block.BorderThickness);
        Assert.Empty(Arranged(default).GetBorderEdges());
    }

    [AvaloniaFact]
    public void Border_edges_run_along_each_side_of_the_block()
    {
        var block = Arranged(new Thickness(1, 2, 3, 4));

        Assert.Equal(
            [new Rect(0, 0, 100, 2), new Rect(0, 36, 100, 4), new Rect(0, 2, 1, 34), new Rect(97, 2, 3, 34)],
            block.GetBorderEdges());
    }

    [AvaloniaTheory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 1, 0, 0)]
    [InlineData(1, 0, 0, 0)]
    [InlineData(0, 0, 1, 0)]
    public void Only_edges_with_a_width_are_drawn(double left, double top, double right, double bottom)
    {
        var edge = Assert.Single(Arranged(new Thickness(left, top, right, bottom)).GetBorderEdges());

        Assert.Equal(left + right > 0 ? 40 : 1, edge.Height);
        Assert.Equal(left + right > 0 ? 1 : 100, edge.Width);
    }

    [AvaloniaFact]
    public void Side_edges_are_empty_when_the_top_and_bottom_fill_the_block()
    {
        var edges = Arranged(new Thickness(1, 30, 1, 30)).GetBorderEdges();

        Assert.Equal([0, 0], edges.Skip(2).Select(e => e.Height));
    }

    [AvaloniaFact]
    public void Render_fills_the_edges_with_the_border_brush_before_the_text()
    {
        var block = Arranged(new Thickness(0, 0, 0, 1));
        block.BorderBrush = Brushes.Red;
        var drawing = new DrawingGroup();

        using (var context = drawing.Open())
            block.Render(context);

        var edge = Assert.IsType<GeometryDrawing>(drawing.Children[0]);
        Assert.Same(Brushes.Red, edge.Brush);
        Assert.Equal(new Rect(0, 39, 100, 1), edge.Geometry!.Bounds);
        Assert.True(drawing.Children.Count > 1, "Expected the text to be drawn after the border.");
    }

    [AvaloniaFact]
    public void Render_draws_no_border_without_a_brush()
    {
        var block = Arranged(new Thickness(0, 0, 0, 1));
        var drawing = new DrawingGroup();

        using (var context = drawing.Open())
            block.Render(context);

        Assert.DoesNotContain(drawing.Children, d => d is GeometryDrawing);
    }

    private static MarkdownSelectableTextBlock Laid(string text, double width = 100)
    {
        var block = new MarkdownSelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        block.Measure(new Size(width, double.PositiveInfinity));
        block.Arrange(new Rect(0, 0, width, block.DesiredSize.Height));
        return block;
    }

    [AvaloniaFact]
    public void Moving_the_block_keeps_its_text_layout()
    {
        var block = Laid("Some words that wrap over a few lines");
        var layout = block.TextLayout;

        block.Arrange(new Rect(0, 50, 100, block.DesiredSize.Height));

        Assert.Equal(50, block.Bounds.Y);
        Assert.Same(layout, block.TextLayout);
    }

    [AvaloniaFact]
    public void Arranging_the_block_at_another_size_rebuilds_its_text_layout()
    {
        var block = Laid("Some words that wrap over a few lines");
        var layout = block.TextLayout;

        block.Arrange(new Rect(0, 0, 60, block.DesiredSize.Height));

        Assert.NotSame(layout, block.TextLayout);
        Assert.Equal(60, block.TextLayout.MaxWidth);
    }

    [AvaloniaFact]
    public void A_text_change_rebuilds_the_text_layout_at_the_same_size()
    {
        var block = Laid("Before");
        var layout = block.TextLayout;

        block.Text = "After";
        block.Measure(new Size(100, double.PositiveInfinity));
        block.Arrange(new Rect(0, 0, 100, block.Bounds.Height));

        Assert.NotSame(layout, block.TextLayout);
        Assert.Equal(Laid("After").TextLayout.TextLines[0].Length, block.TextLayout.TextLines[0].Length);
    }

    [AvaloniaFact]
    public void Moving_the_block_keeps_its_inline_controls_in_place()
    {
        var control = new Border { Width = 20, Height = 10 };
        var block = new MarkdownSelectableTextBlock();
        block.Inlines!.Add(new Run("Press "));
        block.Inlines.Add(new InlineUIContainer(control));
        block.Measure(new Size(200, double.PositiveInfinity));
        block.Arrange(new Rect(0, 0, 200, block.DesiredSize.Height));
        var bounds = control.Bounds;

        block.Arrange(new Rect(0, 50, 200, block.DesiredSize.Height));

        Assert.Same(block, control.GetVisualParent());
        Assert.Equal(bounds, control.Bounds);
        Assert.True(bounds.X > 0);
    }

    [AvaloniaFact]
    public void An_inline_control_added_at_the_same_size_is_arranged()
    {
        var block = new MarkdownSelectableTextBlock();
        block.Inlines!.Add(new Run("Press "));
        block.Measure(new Size(200, 20));
        block.Arrange(new Rect(0, 0, 200, 20));
        var control = new Border { Width = 20, Height = 10 };

        block.Inlines.Add(new InlineUIContainer(control));
        block.Measure(new Size(200, 20));
        block.Arrange(new Rect(0, 0, 200, 20));

        Assert.Same(block, control.GetVisualParent());
        Assert.Equal(new Size(20, 10), control.Bounds.Size);
    }

    [AvaloniaFact]
    public void A_block_moved_down_by_a_taller_sibling_keeps_its_text_layout()
    {
        var above = new Border { Height = 10 };
        var block = new MarkdownSelectableTextBlock { Text = "Some words that wrap over a few lines", TextWrapping = TextWrapping.Wrap };
        var window = new Window { Width = 100, Height = 300, Content = new StackPanel { Children = { above, block } } };
        window.Show();
        var layout = block.TextLayout;

        above.Height = 60;
        window.UpdateLayout();

        Assert.Equal(60, block.Bounds.Y);
        Assert.Same(layout, block.TextLayout);
        window.Close();
    }
}
