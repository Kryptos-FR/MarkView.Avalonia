// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// Integration tests verifying that all block types (lists, tables, blockquotes)
/// are correctly registered with <see cref="MarkView.Avalonia.Rendering.DocumentSelectionLayer"/>
/// so their text is readable via <see cref="MarkdownViewer.SelectAll"/> /
/// <see cref="MarkdownViewer.GetSelectedText"/>.
///
/// These tests exercise the MarkdownViewer registration path (RegisterListItems,
/// RegisterListContent, RegisterTableRows, etc.) which is bypassed by RenderTestBase.
/// </summary>
public class SelectionRegistrationTests
{
    private static string AllText(string markdown)
    {
        var viewer = new MarkdownViewer { Markdown = markdown };
        viewer.SelectAll();
        return viewer.GetSelectedText();
    }

    // ── Unordered lists ───────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SelectAll_unordered_list_items_are_retrievable()
    {
        var text = AllText("- Apple\n- Banana\n- Cherry");
        Assert.Contains("Apple", text);
        Assert.Contains("Banana", text);
        Assert.Contains("Cherry", text);
    }

    [AvaloniaFact]
    public void SelectAll_unordered_list_includes_bullet_marker()
    {
        var text = AllText("- Item");
        Assert.Contains("\u2022", text); // Unicode bullet •
        Assert.Contains("Item", text);
    }

    // ── Ordered lists ─────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SelectAll_ordered_list_includes_number_dots()
    {
        var text = AllText("1. First\n2. Second");
        Assert.Contains("1.", text);
        Assert.Contains("First", text);
        Assert.Contains("2.", text);
        Assert.Contains("Second", text);
    }

    // ── Nested lists ──────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SelectAll_nested_list_both_levels_are_retrievable()
    {
        var text = AllText("- Parent\n  - Child");
        Assert.Contains("Parent", text);
        Assert.Contains("Child", text);
    }

    // ── Blockquote inside list item ───────────────────────────────────────────

    [AvaloniaFact]
    public void SelectAll_blockquote_inside_list_item_is_retrievable()
    {
        // "- > text" produces a Border { Child: Panel } inside the list item's
        // content panel, exercising the Border→Panel recursion in RegisterListContent.
        var text = AllText("- > Quoted inside list");
        Assert.Contains("Quoted inside list", text);
    }

    // ── Tables ────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SelectAll_table_cells_are_retrievable()
    {
        var md = "| Name | Age |\n|------|-----|\n| Alice | 30 |\n| Bob | 25 |";
        var text = AllText(md);
        Assert.Contains("Name", text);
        Assert.Contains("Alice", text);
        Assert.Contains("Bob", text);
        Assert.Contains("30", text);
    }

    [AvaloniaFact]
    public void SelectAll_table_uses_tab_separator_between_cells()
    {
        var md = "| Name | Age |\n|------|-----|\n| Alice | 30 |";
        var text = AllText(md);
        // Cells within a row are tab-separated
        Assert.Contains("Alice\t30", text);
    }

    [AvaloniaFact]
    public void SelectAll_table_uses_newline_separator_between_rows()
    {
        var md = "| A |\n|---|\n| First |\n| Second |";
        var text = AllText(md);
        Assert.Contains("\n", text);
        Assert.Contains("First", text);
        Assert.Contains("Second", text);
    }

    // ── Blockquotes ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SelectAll_blockquote_text_is_retrievable()
    {
        // Blockquote renders as Border { Child: Panel }, exercising the
        // Border→Panel recursion branch in RegisterBlocks.
        var text = AllText("> This is a quotation");
        Assert.Contains("This is a quotation", text);
    }
}
