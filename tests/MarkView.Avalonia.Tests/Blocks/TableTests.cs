// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Markdig;
using Markdig.Extensions.Tables;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class TableTests : RenderTestBase
{
    private static StackPanel RenderWithTables(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();
        return Render(markdown, pipeline);
    }

    private static StackPanel RenderWithTables(string markdown, PipeTableOptions options)
    {
        var pipeline = new MarkdownPipelineBuilder().UsePipeTables(options).Build();
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

    [AvaloniaFact]
    public void Column_alignment_maps_to_horizontal_alignment()
    {
        var result = RenderWithTables("| L | C | R |\n|:--|:-:|--:|\n| a | b | c |");

        var grid = Assert.IsType<Grid>(Assert.Single(result.Children));
        var dataCells = grid.Children.OfType<Border>().Where(b => Grid.GetRow(b) == 1).ToList();

        var leftPanel = Assert.IsType<StackPanel>(dataCells[0].Child);
        var centerPanel = Assert.IsType<StackPanel>(dataCells[1].Child);
        var rightPanel = Assert.IsType<StackPanel>(dataCells[2].Child);

        Assert.Equal(HorizontalAlignment.Left, leftPanel.HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Center, centerPanel.HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Right, rightPanel.HorizontalAlignment);
    }

    [AvaloniaFact]
    public void Column_without_alignment_marker_defaults_to_unset_horizontal_alignment()
    {
        var result = RenderWithTables("| A | B |\n|---|---|\n| 1 | 2 |");

        var grid = Assert.IsType<Grid>(Assert.Single(result.Children));
        var headerCell = grid.Children.OfType<Border>().First(b => Grid.GetRow(b) == 0);
        var panel = Assert.IsType<StackPanel>(headerCell.Child);

        Assert.Equal(HorizontalAlignment.Stretch, panel.HorizontalAlignment);
    }

    [AvaloniaFact]
    public void Inferred_column_widths_are_proportional_to_separator_dash_counts()
    {
        var options = new PipeTableOptions { InferColumnWidthsFromSeparator = true };
        var result = RenderWithTables("| A | B |\n|--|------|\n| 1 | 2 |", options);

        var grid = Assert.IsType<Grid>(Assert.Single(result.Children));
        Assert.Equal(2, grid.ColumnDefinitions.Count);

        var firstWidth = grid.ColumnDefinitions[0].Width;
        var secondWidth = grid.ColumnDefinitions[1].Width;

        Assert.Equal(GridUnitType.Star, firstWidth.GridUnitType);
        Assert.Equal(GridUnitType.Star, secondWidth.GridUnitType);
        Assert.True(secondWidth.Value > firstWidth.Value);
    }

    [AvaloniaFact]
    public void Without_width_inference_columns_are_equal_star_width()
    {
        var result = RenderWithTables("| A | B |\n|--|------|\n| 1 | 2 |");

        var grid = Assert.IsType<Grid>(Assert.Single(result.Children));
        Assert.All(grid.ColumnDefinitions, c => Assert.Equal(new GridLength(1, GridUnitType.Star), c.Width));
    }
}
