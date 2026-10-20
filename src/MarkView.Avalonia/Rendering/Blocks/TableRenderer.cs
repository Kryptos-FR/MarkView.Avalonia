// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Markdig.Extensions.Tables;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="Table"/> as an Avalonia <see cref="Grid"/>.
/// Supports pipe tables and grid tables (both share the same AST). The cells of every second body row
/// (the 2nd, 4th, … row below the header) also get the <c>markdown-table-row-even</c> class, for themes
/// that stripe the rows.
/// </summary>
public sealed class TableRenderer : AvaloniaObjectRenderer<Table>
{
    protected override void Write(AvaloniaRenderer renderer, Table obj)
    {
        var grid = new Grid();
        grid.Classes.Add("markdown-table");
        renderer.ApplyTheme(grid, "MarkdownTableTheme");
        AutomationRoles.Set(grid, AutomationControlType.Table);

        // Define columns
        for (int i = 0; i < obj.ColumnDefinitions.Count; i++)
        {
            var width = obj.ColumnDefinitions[i].Width;
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width > 0 ? width : 1, GridUnitType.Star) });
        }

        // A right-to-left table is mirrored, first column on the right; a column aligned left or right
        // stays on that side, as text-align does in a browser.
        var isMirrored = renderer.ResolveFlowDirection(obj) == FlowDirection.RightToLeft;

        int rowIndex = 0;
        int bodyRowCount = 0;
        foreach (var row in obj.OfType<TableRow>())
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var isEvenBodyRow = !row.IsHeader && ++bodyRowCount % 2 == 0;

            int colIndex = 0;
            foreach (var cell in row.OfType<TableCell>())
            {
                var cellPanel = new StackPanel { Spacing = 4 };

                var border = new Border
                {
                    Child = cellPanel,
                };
                border.Classes.Add("markdown-table-cell");
                renderer.ApplyTheme(border, (rowIndex, colIndex) switch
                {
                    (0, 0) => "MarkdownTableCornerCellTheme",
                    (0, _) => "MarkdownTableFirstRowCellTheme",
                    (_, 0) => "MarkdownTableFirstColumnCellTheme",
                    _ => "MarkdownTableCellTheme",
                });
                if (row.IsHeader)
                {
                    border.Classes.Add("markdown-table-header");
                    renderer.ApplyTheme(cellPanel, "MarkdownTableHeaderContentTheme");
                    AutomationRoles.Set(border, AutomationControlType.HeaderItem);
                }
                else
                {
                    if (isEvenBodyRow)
                        border.Classes.Add("markdown-table-row-even");
                    AutomationRoles.Set(border, AutomationControlType.DataItem);
                }

                // Apply text alignment from column definition
                if (colIndex < obj.ColumnDefinitions.Count)
                {
                    var alignment = obj.ColumnDefinitions[colIndex].Alignment;
                    if (alignment.HasValue)
                    {
                        cellPanel.HorizontalAlignment = alignment.Value switch
                        {
                            TableColumnAlign.Center => HorizontalAlignment.Center,
                            TableColumnAlign.Right => isMirrored ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                            _ => isMirrored ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                        };
                    }
                }

                Grid.SetRow(border, rowIndex);
                Grid.SetColumn(border, colIndex);

                Grid.SetColumnSpan(border, cell.ColumnSpan);
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
