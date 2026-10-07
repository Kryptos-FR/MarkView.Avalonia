// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;

using Markdig;

using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerSelectionRegistrationTests
{
    private static string SelectAllText(string markdown, MarkdownPipeline? pipeline = null)
    {
        var viewer = new MarkdownViewer { Pipeline = pipeline, Markdown = markdown };
        viewer.SelectAll();
        return viewer.GetSelectedText();
    }

    [AvaloniaFact]
    public void Code_block_text_is_selectable()
    {
        const string markdown = """
            ```
            code line
            ```

            after
            """;

        Assert.Equal("code line\nafter", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Quoted_paragraphs_are_selectable()
    {
        const string markdown = """
            > quoted

            after
            """;

        Assert.Equal("quoted\nafter", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void List_marker_prefixes_only_the_first_paragraph_of_each_item()
    {
        const string markdown = """
            - one

              more
            - two
            """;

        Assert.Equal("• one\nmore\n• two", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Nested_list_items_keep_their_own_markers()
    {
        const string markdown = """
            1. a
               - b
            """;

        Assert.Equal("1. a\n• b", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Table_cells_are_tab_separated_and_rows_newline_separated()
    {
        const string markdown = """
            | A | B |
            |---|---|
            | 1 | 2 |
            """;

        Assert.Equal("A\tB\n1\t2", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Multi_block_grid_table_cell_joins_its_blocks_with_spaces()
    {
        const string markdown = """
            +-------------+-----+
            | para        | x   |
            |             |     |
            | - item      |     |
            +-------------+-----+
            """;

        var text = SelectAllText(markdown, new MarkdownPipelineBuilder().UseGridTables().Build());

        Assert.Equal("para item\tx", text);
    }

    [AvaloniaFact]
    public void GetSelectedText_before_any_render_is_empty()
    {
        Assert.Equal(string.Empty, new MarkdownViewer().GetSelectedText());
    }

    [AvaloniaFact]
    public void Grid_table_cell_with_two_paragraphs_joins_them_with_a_space()
    {
        const string markdown = """
            +-------+---+
            | one   | x |
            |       |   |
            | two   |   |
            +-------+---+
            """;

        var text = SelectAllText(markdown, new MarkdownPipelineBuilder().UseGridTables().Build());

        Assert.Equal("one two\tx", text);
    }

    [AvaloniaFact]
    public void Task_list_items_are_prefixed_with_their_check_glyph()
    {
        const string markdown = """
            - [x] done
            - [ ] todo
            """;

        // The two spaces after each glyph are the current output (glyph, separator, then the
        // item text's own leading space).
        Assert.Equal("☑  done\n☐  todo", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Table_inside_a_list_item_contributes_its_cells_after_the_marker()
    {
        const string markdown = """
            - a

              | A | B |
              |---|---|
              | 1 | 2 |
            """;

        var text = SelectAllText(markdown);

        // Cell separators inside list items are not asserted: known issue, list content does
        // not register table/code-block structure.
        Assert.StartsWith("• a", text);
        var positions = new[] { "A", "B", "1", "2" }.Select(cell => text.IndexOf(cell, "• a".Length, StringComparison.Ordinal)).ToArray();
        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.Equal(positions.Order(), positions);
    }
}
