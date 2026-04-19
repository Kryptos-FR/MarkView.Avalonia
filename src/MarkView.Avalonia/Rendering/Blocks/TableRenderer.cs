// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Layout;

using Markdig.Extensions.Tables;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="Table"/> as an Avalonia <see cref="Grid"/>.
/// Supports pipe tables and grid tables (both share the same AST).
/// </summary>
public sealed class TableRenderer : AvaloniaObjectRenderer<Table>
{
    protected override void Write(AvaloniaRenderer renderer, Table obj)
    {
        var grid = new Grid();
        grid.Classes.Add("markdown-table");

        // Define columns
        for (int i = 0; i < obj.ColumnDefinitions.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        int rowIndex = 0;
        foreach (var rowObj in obj)
        {
            if (rowObj is not TableRow row)
                continue;

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int colIndex = 0;
            foreach (var cellObj in row)
            {
                if (cellObj is not TableCell cell)
                    continue;

                var cellPanel = new StackPanel { Spacing = 4 };

                var border = new Border
                {
                    Child = cellPanel,
                };
                border.Classes.Add("markdown-table-cell");
                if (row.IsHeader)
                    border.Classes.Add("markdown-table-header");

                // Apply text alignment from column definition
                if (colIndex < obj.ColumnDefinitions.Count)
                {
                    var alignment = obj.ColumnDefinitions[colIndex].Alignment;
                    if (alignment.HasValue)
                    {
                        cellPanel.HorizontalAlignment = alignment.Value switch
                        {
                            TableColumnAlign.Center => HorizontalAlignment.Center,
                            TableColumnAlign.Right => HorizontalAlignment.Right,
                            _ => HorizontalAlignment.Left,
                        };
                    }
                }

                Grid.SetRow(border, rowIndex);
                Grid.SetColumn(border, colIndex);

                if (cell.ColumnSpan > 1)
                    Grid.SetColumnSpan(border, cell.ColumnSpan);
                if (cell.RowSpan > 1)
                    Grid.SetRowSpan(border, cell.RowSpan);

                renderer.Push(cellPanel);
                renderer.WriteChildren(cell);
                renderer.Pop();

                grid.Children.Add(border);
                colIndex += cell.ColumnSpan;
            }

            rowIndex++;
        }

        renderer.WriteBlock(grid);
    }
}
