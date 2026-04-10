using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class ThematicBreakTests : RenderTestBase
{
    [AvaloniaFact]
    public void Thematic_break_renders_as_Separator()
    {
        var result = Render("---");
        var separator = Assert.IsType<Separator>(Assert.Single(result.Children));
        Assert.Contains("markdown-thematic-break", separator.Classes);
    }
}
