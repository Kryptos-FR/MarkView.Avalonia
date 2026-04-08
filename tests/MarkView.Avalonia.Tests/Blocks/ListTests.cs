using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
}
