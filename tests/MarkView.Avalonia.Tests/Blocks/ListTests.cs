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
}
