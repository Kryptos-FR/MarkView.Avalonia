using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Headless.XUnit;
using global::Avalonia.Media;
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
}
