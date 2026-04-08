using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class ListTightLooseTests : RenderTestBase
{
    [AvaloniaFact]
    public void Tight_list_has_tight_class()
    {
        var result = Render("- a\n- b\n- c");

        var listPanel = Assert.IsType<StackPanel>(Assert.Single(result.Children));
        Assert.Contains("markdown-list-tight", listPanel.Classes);
    }

    [AvaloniaFact]
    public void Loose_list_has_loose_class()
    {
        var result = Render("- a\n\n- b\n\n- c");

        var listPanel = Assert.IsType<StackPanel>(Assert.Single(result.Children));
        Assert.Contains("markdown-list-loose", listPanel.Classes);
    }

    [AvaloniaFact]
    public void Tight_list_has_smaller_spacing_than_loose()
    {
        var tight = Render("- a\n- b");
        var loose = Render("- a\n\n- b");

        var tightPanel = Assert.IsType<StackPanel>(Assert.Single(tight.Children));
        var loosePanel = Assert.IsType<StackPanel>(Assert.Single(loose.Children));

        Assert.True(tightPanel.Spacing < loosePanel.Spacing);
    }
}
