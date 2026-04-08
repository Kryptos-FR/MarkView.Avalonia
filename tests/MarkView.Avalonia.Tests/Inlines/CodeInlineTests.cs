using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class CodeInlineTests : RenderTestBase
{
    [AvaloniaFact]
    public void Code_inline_renders_with_code_style_class()
    {
        var result = Render("Use `code` here");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var inlines = textBlock.Inlines!.ToList();
        Assert.Equal(3, inlines.Count);
        var container = Assert.IsType<InlineUIContainer>(inlines[1]);
        var border = Assert.IsType<Border>(container.Child);
        Assert.Contains("markdown-code-inline", border.Classes);
    }
}
