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

        Assert.Equal("☑ done\n☐ todo", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Table_inside_a_list_item_is_tab_separated_after_the_marker()
    {
        const string markdown = """
            - a

              | A | B |
              |---|---|
              | 1 | 2 |
            """;

        Assert.Equal("• a\nA\tB\n1\t2", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Code_block_inside_a_list_item_is_selectable()
    {
        const string markdown = """
            - step one

              ```
              dotnet build
              ```
            """;

        Assert.Equal("• step one\ndotnet build", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Code_block_opening_a_list_item_is_prefixed_with_the_marker()
    {
        const string markdown = """
            - ```
              dotnet build
              ```
            """;

        Assert.Equal("• dotnet build", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Table_opening_a_list_item_prefixes_its_first_cell_with_the_marker()
    {
        const string markdown = """
            - | A | B |
              |---|---|
              | 1 | 2 |
            """;

        Assert.Equal("• A\tB\n1\t2", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Inline_image_is_copied_as_its_alt_text()
    {
        Assert.Equal("before a picture after", SelectAllText("before ![a picture](x.png) after"));
    }

    [AvaloniaFact]
    public void Image_without_alt_text_copies_nothing_in_its_place()
    {
        Assert.Equal("a  b", SelectAllText("a ![](x.png) b"));
    }

    [AvaloniaFact]
    public void Linked_image_is_copied_as_its_alt_text()
    {
        Assert.Equal("badge", SelectAllText("[![badge](b.png)](https://example.com)"));
    }

    [AvaloniaFact]
    public void YouTube_thumbnail_is_copied_as_its_alt_text()
    {
        Assert.Equal("demo video", SelectAllText("![demo video](https://www.youtube.com/watch?v=dQw4w9WgXcQ)"));
    }

    [AvaloniaFact]
    public void Image_in_a_table_cell_is_copied_as_its_alt_text()
    {
        const string markdown = """
            | A |
            |---|
            | x ![pic](p.png) y |
            """;

        Assert.Equal("A\nx pic y", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Image_in_the_second_paragraph_of_a_grid_table_cell_is_copied_as_its_alt_text()
    {
        const string markdown = """
            +-------------------+---+
            | one               | x |
            |                   |   |
            | two ![pic](p.png) |   |
            +-------------------+---+
            """;

        var text = SelectAllText(markdown, new MarkdownPipelineBuilder().UseGridTables().Build());

        Assert.Equal("one two pic\tx", text);
    }

    [AvaloniaFact]
    public void Images_in_two_paragraphs_of_a_grid_table_cell_are_both_copied_as_their_alt_text()
    {
        const string markdown = """
            +--------------+---+
            | ![a](a.png)  | x |
            |              |   |
            | ![b](b.png)  |   |
            +--------------+---+
            """;

        var text = SelectAllText(markdown, new MarkdownPipelineBuilder().UseGridTables().Build());

        Assert.Equal("a b\tx", text);
    }

    [AvaloniaFact]
    public void Grid_table_cell_block_without_text_adds_no_separator()
    {
        // The second paragraph holds only an ignored HTML inline, so it renders no text.
        const string markdown = """
            +---------------+---+
            | one           | x |
            |               |   |
            | <span></span> |   |
            +---------------+---+
            """;

        var text = SelectAllText(markdown, new MarkdownPipelineBuilder().UseGridTables().Build());

        Assert.Equal("one\tx", text);
    }

    [AvaloniaFact]
    public void Two_images_in_one_paragraph_are_both_copied_as_their_alt_text()
    {
        Assert.Equal("a and b", SelectAllText("![a](a.png) and ![b](b.png)"));
    }

    private static string SelectAllTextWithCopyableBreaks(string markdown)
    {
        var viewer = new MarkdownViewer();
        viewer.Extensions.Add(new CopyableBreakExtension());
        viewer.Markdown = markdown;
        viewer.SelectAll();
        return viewer.GetSelectedText();
    }

    [AvaloniaFact]
    public void Block_control_with_copy_text_is_copied_between_its_neighbours()
    {
        const string markdown = """
            before

            ---

            after
            """;

        Assert.Equal("before\n<break>\nafter", SelectAllTextWithCopyableBreaks(markdown));
    }

    [AvaloniaFact]
    public void Block_control_with_copy_text_opening_a_list_item_is_prefixed_with_the_marker()
    {
        Assert.Equal("• <break>", SelectAllTextWithCopyableBreaks("- ***"));
    }
}
