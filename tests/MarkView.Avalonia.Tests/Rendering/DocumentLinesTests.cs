// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// How <see cref="DocumentLines"/> counts the lines of a laid-out document for
/// <see cref="MarkdownViewer.MaxLines"/>.
/// </summary>
public class DocumentLinesTests : RenderTestBase
{
    private static StackPanel Lay(StackPanel root)
    {
        var window = new Window { Width = 400, Height = 2000, Content = root };
        window.Show();
        window.UpdateLayout();
        return root;
    }

    private static StackPanel LayRendered(string markdown, MarkdownPipeline? pipeline = null)
    {
        var root = Render(markdown, pipeline);
        return Lay(root);
    }

    private static int CountLines(StackPanel root) => DocumentLines.GetLines(root, root).Count();

    private static Border Box(double height) => new() { Height = height, [MarkdownSelection.CopyTextProperty] = "box" };

    [AvaloniaFact]
    public void Each_paragraph_line_is_a_line()
    {
        var root = LayRendered("One\n\nTwo  \nTwo b\n\nThree");

        Assert.Equal(4, CountLines(root));
    }

    [AvaloniaFact]
    public void The_cut_is_the_bottom_of_the_last_line_shown()
    {
        var root = Lay(new StackPanel { Children = { Box(10), Box(20), Box(30) } });

        Assert.Equal(10, DocumentLines.FindCut(root, root, 1));
        Assert.Equal(30, DocumentLines.FindCut(root, root, 2));
        Assert.Null(DocumentLines.FindCut(root, root, 3));
    }

    [AvaloniaFact]
    public void The_cut_is_given_in_the_coordinates_asked_for()
    {
        var root = Lay(new StackPanel { Margin = new Thickness(0, 7, 0, 0), Children = { Box(10), Box(20) } });
        var window = (Window)root.Parent!;

        Assert.Equal(17, DocumentLines.FindCut(root, window, 1));
    }

    [AvaloniaFact]
    public void The_cut_takes_in_the_padding_and_border_of_the_blocks_that_end_before_the_next_line()
    {
        var inner = new Border { Padding = new Thickness(0, 0, 0, 4), BorderThickness = new Thickness(0, 0, 0, 1), Child = Box(10) };
        var outer = new Border { Padding = new Thickness(0, 0, 0, 20), Child = inner };
        var root = Lay(new StackPanel { Children = { outer, Box(10) } });

        // The inner border ends at 15, before the next line at 35; the outer one ends with it.
        Assert.Equal(35, DocumentLines.FindCut(root, root, 1));

        // A margin below the outer border puts the next line further down, but is not part of a block.
        outer.Margin = new Thickness(0, 0, 0, 5);
        root.UpdateLayout();
        Assert.Equal(35, DocumentLines.FindCut(root, root, 1));
    }

    [AvaloniaFact]
    public void The_cut_stops_at_a_block_that_goes_on_past_the_next_line()
    {
        var text = new TextBlock { Text = "a\nb", Padding = new Thickness(0, 0, 0, 30) };
        var root = Lay(new StackPanel { Children = { new Border { Padding = new Thickness(0, 0, 0, 5), Child = text } } });
        var first = DocumentLines.GetLines(root, root).First();

        Assert.Equal(first.Bottom, DocumentLines.FindCut(root, root, 1));
    }

    [AvaloniaFact]
    public void A_table_row_is_cut_below_its_cells()
    {
        var root = LayRendered("| A | B |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |", new MarkdownPipelineBuilder().UsePipeTables().Build());
        var cells = root.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("markdown-table-cell")).ToList();
        var headerBottom = cells[0].TranslatePoint(new Point(0, cells[0].Bounds.Height), root)!.Value.Y;

        Assert.Equal(headerBottom, DocumentLines.FindCut(root, root, 1)!.Value, 1);
    }

    [AvaloniaFact]
    public void A_list_marker_and_its_text_are_one_line()
    {
        var root = LayRendered("- a\n- b\n- c");

        Assert.Equal(3, CountLines(root));
    }

    [AvaloniaFact]
    public void The_cells_of_a_table_row_are_one_line()
    {
        var root = LayRendered("| A | B |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |", new MarkdownPipelineBuilder().UsePipeTables().Build());

        Assert.Equal(3, CountLines(root));
    }

    [AvaloniaFact]
    public void Each_line_of_a_code_block_is_a_line()
    {
        var root = LayRendered("```\na\n\nc\nd\n```");

        Assert.Equal(4, CountLines(root));
    }

    [AvaloniaFact]
    public void A_control_standing_for_content_is_one_line_whatever_its_height()
    {
        var root = Lay(new StackPanel { Children = { Box(200), new TextBlock { Text = "after" } } });

        Assert.Equal(2, CountLines(root));
        Assert.Equal(200, DocumentLines.FindCut(root, root, 1));
    }

    [AvaloniaFact]
    public void Controls_side_by_side_are_one_line()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { Box(10), Box(30) } };
        var root = Lay(new StackPanel { Children = { row, Box(5) } });

        Assert.Equal(2, CountLines(root));
        Assert.Equal(30, DocumentLines.GetLines(root, root).First().Bottom);
        Assert.Equal(30, DocumentLines.FindCut(root, root, 1));
    }

    [AvaloniaFact]
    public void Lines_are_counted_from_top_to_bottom_whatever_the_order_of_the_controls()
    {
        var below = Box(10);
        Grid.SetRow(below, 1);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), Children = { below, Box(10) } };
        var root = Lay(new StackPanel { Children = { grid } });

        Assert.Equal(2, CountLines(root));
    }

    [AvaloniaFact]
    public void Spans_that_overlap_by_half_a_pixel_or_less_are_separate_lines()
    {
        var root = Lay(new StackPanel
        {
            UseLayoutRounding = false,
            Children = { Box(10), new Border { Margin = new Thickness(0, -0.5, 0, 0), Child = Box(10) }, new Border { Margin = new Thickness(0, -0.6, 0, 0), Child = Box(10) } },
        });

        // The second box starts half a pixel into the first, the third more than that into the second.
        Assert.Equal(2, CountLines(root));
    }

    [AvaloniaFact]
    public void A_span_half_a_pixel_high_or_less_is_not_a_line()
    {
        var root = Lay(new StackPanel { UseLayoutRounding = false, Children = { Box(10), Box(0.5), Box(0.6) } });

        Assert.Equal(2, CountLines(root));
    }

    [AvaloniaFact]
    public void Content_without_text_or_copy_text_is_not_a_line()
    {
        var root = Lay(new StackPanel { Children = { Box(10), new Border { Height = 50 }, new Separator(), Box(10) } });

        Assert.Equal(2, CountLines(root));
    }

    [AvaloniaFact]
    public void Hidden_content_is_not_a_line()
    {
        var root = Lay(new StackPanel
        {
            Children = { Box(10), new Border { Child = Box(10), IsVisible = false }, new TextBlock { Text = "x", IsVisible = false }, Box(10) },
        });

        Assert.Equal(2, CountLines(root));
    }

    [AvaloniaFact]
    public void An_empty_text_block_is_not_a_line()
    {
        var root = Lay(new StackPanel { Children = { Box(10), new TextBlock(), new TextBlock { Text = "x" } } });

        Assert.Equal(2, CountLines(root));
    }

    [AvaloniaFact]
    public void Lines_clipped_away_by_a_scroll_viewer_are_not_lines()
    {
        var code = new TextBlock { Text = "1\n2\n3\n4\n5\n6", FontSize = 10 };
        var root = Lay(new StackPanel { Children = { new ScrollViewer { Height = 15, Content = code } } });
        var lineHeight = code.TextLayout.TextLines[0].Height;

        Assert.True(lineHeight is > 7.5 and < 15, $"line height {lineHeight}");
        // One whole line and the visible part of the next.
        Assert.Equal(2, CountLines(root));
        Assert.Equal(15, DocumentLines.GetLines(root, root).Last().Bottom, 1);
        Assert.Equal(lineHeight, DocumentLines.FindCut(root, root, 1)!.Value, 1);
    }

    [AvaloniaFact]
    public void Text_lines_follow_the_padding_and_line_spacing_of_their_text_block()
    {
        var text = new TextBlock { Text = "a\nb\nc", Padding = new Thickness(0, 6, 0, 0), LineSpacing = 10 };
        var root = Lay(new StackPanel { Children = { text } });

        var lines = DocumentLines.GetLines(root, root).ToList();

        Assert.Equal(3, lines.Count);
        // Each line starts where the layout puts its first character.
        Assert.Equal(6, lines[0].Top, 1);
        Assert.Equal(6 + text.TextLayout.HitTestTextPosition(2).Top, lines[1].Top, 1);
        Assert.Equal(6 + text.TextLayout.HitTestTextPosition(4).Top, lines[2].Top, 1);
    }
}
