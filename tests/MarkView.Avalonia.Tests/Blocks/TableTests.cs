using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Markdig;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class TableTests : RenderTestBase
{
    private static StackPanel RenderWithTables(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();
        return Render(markdown, pipeline);
    }

    [AvaloniaFact]
    public void Pipe_table_renders_as_Grid()
    {
        var result = RenderWithTables("| A | B |\n|---|---|\n| 1 | 2 |");

        var grid = Assert.IsType<Grid>(Assert.Single(result.Children));
        Assert.Contains("markdown-table", grid.Classes);
    }

    [AvaloniaFact]
    public void Table_has_correct_column_count()
    {
        var result = RenderWithTables("| A | B | C |\n|---|---|---|\n| 1 | 2 | 3 |");

        var grid = Assert.IsType<Grid>(Assert.Single(result.Children));
        Assert.Equal(3, grid.ColumnDefinitions.Count);
    }

    [AvaloniaFact]
    public void Table_header_row_has_header_class()
    {
        var result = RenderWithTables("| H1 | H2 |\n|----|----|\n| a  | b  |");

        var grid = Assert.IsType<Grid>(Assert.Single(result.Children));
        // First row cells should have the header class
        var headerCell = grid.Children.OfType<Border>().First();
        Assert.Contains("markdown-table-header", headerCell.Classes);
    }

    [AvaloniaFact]
    public void Table_data_row_does_not_have_header_class()
    {
        var result = RenderWithTables("| H1 | H2 |\n|----|----|\n| a  | b  |");

        var grid = Assert.IsType<Grid>(Assert.Single(result.Children));
        // Data cells (row index 1) should not have header class
        var dataCells = grid.Children.OfType<Border>()
            .Where(b => Grid.GetRow(b) == 1);
        Assert.All(dataCells, cell => Assert.DoesNotContain("markdown-table-header", cell.Classes));
    }

    [AvaloniaFact]
    public void Table_without_header_renders()
    {
        // Some pipe table implementations allow headerless tables
        var result = RenderWithTables("| a | b |\n| c | d |");

        // Should still render something (exact behavior depends on Markdig parsing)
        Assert.NotEmpty(result.Children);
    }
}
