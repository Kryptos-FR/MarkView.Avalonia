// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using Markdig.Syntax;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class ListTests : RenderTestBase
{
    [AvaloniaFact]
    public void Unordered_list_renders_as_StackPanel()
    {
        var result = Render("- Item 1\n- Item 2\n- Item 3");
        var listPanel = Assert.IsType<StackPanel>(Assert.Single(result.Children));
        Assert.Contains("markdown-list", listPanel.Classes);
        Assert.Equal(3, listPanel.Children.Count);
    }

    [AvaloniaFact]
    public void Unordered_list_items_have_bullet_marker()
    {
        var result = Render("- Item 1");
        var listPanel = Assert.IsType<StackPanel>(Assert.Single(result.Children));
        var itemGrid = Assert.IsType<Grid>(Assert.Single(listPanel.Children));
        var marker = Assert.IsType<TextBlock>(itemGrid.Children[0]);
        Assert.Contains("\u2022", marker.Text);
    }

    [AvaloniaFact]
    public void Ordered_list_has_number_markers()
    {
        var result = Render("1. First\n2. Second");
        var listPanel = Assert.IsType<StackPanel>(Assert.Single(result.Children));
        Assert.Contains("markdown-list-ordered", listPanel.Classes);
        var firstItem = Assert.IsType<Grid>(listPanel.Children[0]);
        var marker = Assert.IsType<TextBlock>(firstItem.Children[0]);
        Assert.Equal("1.", marker.Text);
    }

    [AvaloniaFact]
    public void Nested_list_renders_recursively()
    {
        var result = Render("- Parent\n  - Child");
        var listPanel = Assert.IsType<StackPanel>(Assert.Single(result.Children));
        Assert.Single(listPanel.Children);
    }

    private static TextBlock[] Markers(StackPanel list) =>
        list.Children.Cast<Grid>().Select(g => g.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 0)).ToArray();

    [AvaloniaFact]
    public void Ordered_list_numbers_start_at_the_markdown_start_value()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("""
            3. a
            4. b
            5. c
            """).Children));

        Assert.Equal(["3.", "4.", "5."], Markers(list).Select(m => m.Text));
        Assert.Contains("markdown-list-ordered", list.Classes);
        Assert.DoesNotContain("markdown-list-unordered", list.Classes);
    }

    [AvaloniaFact]
    public void Unordered_list_items_all_use_the_bullet_marker()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("""
            - a
            - b
            """).Children));

        Assert.Equal(["\u2022", "\u2022"], Markers(list).Select(m => m.Text));
        Assert.Contains("markdown-list-unordered", list.Classes);
    }

    [AvaloniaFact]
    public void List_item_is_a_two_column_grid_with_marker_and_content()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("- a").Children));
        var item = Assert.IsType<Grid>(Assert.Single(list.Children));

        Assert.Equal(2, item.ColumnDefinitions.Count);
        Assert.True(item.ColumnDefinitions[0].Width.IsAuto);
        Assert.True(item.ColumnDefinitions[1].Width.IsStar);
        var marker = Markers(list)[0];
        Assert.Equal(new Thickness(0, 0, 8, 0), marker.Margin);
        Assert.Contains("markdown-list-marker", marker.Classes);
        var content = item.Children.OfType<StackPanel>().Single();
        Assert.Equal(1, Grid.GetColumn(content));
        Assert.Equal(4, content.Spacing);
    }

    [AvaloniaFact]
    public void Task_items_show_checkbox_glyph_in_marker_column_and_not_in_the_text()
    {
        var pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("""
            - [x] done
            - [ ] todo
            """, pipeline).Children));

        var markers = Markers(list);
        Assert.Equal(["\u2611", "\u2610"], markers.Select(m => m.Text));
        Assert.All(markers, m => Assert.Contains("markdown-task-list", m.Classes));
        var texts = list.Children.Cast<Grid>()
            .SelectMany(g => g.Children.OfType<StackPanel>().Single().Children.OfType<MarkdownSelectableTextBlock>());
        Assert.All(texts, t => Assert.DoesNotContain(t.Inlines!.OfType<Run>(), r => r.Classes.Contains("markdown-task-list")));
    }

    [AvaloniaFact]
    public void Task_marker_carries_the_list_marker_class()
    {
        var pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("- [x] done", pipeline).Children));

        var marker = Assert.Single(Markers(list));
        Assert.Contains("markdown-list-marker", marker.Classes);
    }

    [AvaloniaFact]
    public void Task_item_text_does_not_start_with_the_space_after_the_check_glyph()
    {
        var pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("""
            - [x] done
            - [ ] *todo*
            """, pipeline).Children));

        var texts = list.Children.Cast<Grid>()
            .Select(g => g.Children.OfType<StackPanel>().Single().Children.OfType<MarkdownSelectableTextBlock>().Single());
        Assert.Equal(["done", "todo"], texts.Select(t => MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!)));
    }

    [AvaloniaFact]
    public void Empty_list_item_renders_its_marker_with_an_empty_content_panel()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("""
            -
            - b
            """).Children));

        Assert.Equal(2, list.Children.Count);
        var empty = Assert.IsType<Grid>(list.Children[0]);
        Assert.Equal("\u2022", Assert.Single(empty.Children.OfType<TextBlock>()).Text);
        Assert.Empty(empty.Children.OfType<StackPanel>().Single().Children);
    }

    // Built programmatically: Markdig always supplies a start value for ordered lists, so this covers the fallback only reachable through custom parsers.
    [AvaloniaFact]
    public void Ordered_list_without_a_start_value_numbers_from_one()
    {
        var list = new ListBlock(null!) { IsOrdered = true, OrderedStart = null };
        list.Add(new ListItemBlock(null!));
        list.Add(new ListItemBlock(null!));
        var renderer = new AvaloniaRenderer();

        renderer.Render(list);

        var panel = Assert.IsType<StackPanel>(Assert.Single(renderer.RootPanel.Children));
        Assert.Equal(["1.", "2."], Markers(panel).Select(m => m.Text));
    }

    [AvaloniaTheory]
    [InlineData("a. x\nb. y", new[] { "a.", "b." })]
    [InlineData("A. x\nB. y", new[] { "A.", "B." })]
    [InlineData("i. x\nii. y\niii. z", new[] { "i.", "ii.", "iii." })]
    [InlineData("IV. x\nV. y", new[] { "IV.", "V." })]
    [InlineData("c) x\nd) y", new[] { "c)", "d)" })]
    public void List_extras_markers_use_letters_and_roman_numerals(string markdown, string[] expected)
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(
            Render(markdown, new MarkdownPipelineBuilder().UseListExtras().Build()).Children));

        Assert.Equal(expected, Markers(list).Select(m => m.Text));
    }

    [AvaloniaFact]
    public void Ordered_list_with_parenthesis_delimiter_keeps_it()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("1) a\n2) b").Children));

        Assert.Equal(["1)", "2)"], Markers(list).Select(m => m.Text));
    }

    [Theory]
    [InlineData(1, 'a', '.', "a.")]
    [InlineData(26, 'a', '.', "z.")]
    [InlineData(27, 'a', '.', "aa.")]
    [InlineData(52, 'A', '.', "AZ.")]
    [InlineData(53, 'A', ')', "BA)")]
    [InlineData(0, 'a', '.', "0.")]
    [InlineData(4, 'i', '.', "iv.")]
    [InlineData(9, 'I', '.', "IX.")]
    [InlineData(14, 'I', '.', "XIV.")]
    [InlineData(40, 'I', '.', "XL.")]
    [InlineData(90, 'I', '.', "XC.")]
    [InlineData(400, 'I', '.', "CD.")]
    [InlineData(1994, 'I', '.', "MCMXCIV.")]
    [InlineData(1666, 'I', '.', "MDCLXVI.")]
    [InlineData(3999, 'I', '.', "MMMCMXCIX.")]
    [InlineData(4000, 'I', '.', "4000.")]
    [InlineData(0, 'i', '.', "0.")]
    [InlineData(12, '1', ')', "12)")]
    [InlineData(12, '1', '\0', "12.")]
    [InlineData(5, 'x', '.', "5.")]
    public void Ordered_marker_formats_the_number_for_the_bullet_type(int number, char bulletType, char delimiter, string expected)
    {
        Assert.Equal(expected, MarkView.Avalonia.Rendering.Blocks.ListRenderer.OrderedMarker(number, bulletType, delimiter));
    }

    [AvaloniaFact]
    public void Ordered_markers_share_one_right_aligned_column()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("8. a\n9. b\n10. c").Children));

        Assert.True(Grid.GetIsSharedSizeScope(list));
        var items = list.Children.Cast<Grid>().ToList();
        Assert.All(items, item => Assert.Equal("MarkdownListMarker", item.ColumnDefinitions[0].SharedSizeGroup));
        Assert.All(Markers(list), marker => Assert.Equal(global::Avalonia.Layout.HorizontalAlignment.Right, marker.HorizontalAlignment));
    }
}
