using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class CodeInlineTests : RenderTestBase
{
    [AvaloniaFact]
    public void Code_inline_renders_as_Run_with_class()
    {
        var result = Render("Use `code` here");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var inlines = textBlock.Inlines!.ToList();
        Assert.Equal(3, inlines.Count);
        var run = Assert.IsType<Run>(inlines[1]);
        Assert.Contains("markdown-code-inline", run.Classes);
    }

    [AvaloniaFact]
    public void Code_inline_Run_contains_correct_text()
    {
        var result = Render("Use `hello` here");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var run = Assert.IsType<Run>(textBlock.Inlines!.ToList()[1]);
        Assert.Equal("hello", run.Text);
    }
}
