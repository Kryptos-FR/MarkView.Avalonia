using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class HeadingTests : RenderTestBase
{
    [AvaloniaTheory]
    [InlineData("# H1", 1)]
    [InlineData("## H2", 2)]
    [InlineData("### H3", 3)]
    [InlineData("#### H4", 4)]
    [InlineData("##### H5", 5)]
    [InlineData("###### H6", 6)]
    public void Heading_renders_with_level_style_class(string markdown, int level)
    {
        var result = Render(markdown);
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        Assert.Contains($"markdown-h{level}", textBlock.Classes);
    }

    [AvaloniaFact]
    public void Heading_renders_text_content()
    {
        var result = Render("# Hello");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var run = Assert.IsType<Run>(Assert.Single(textBlock.Inlines!));
        Assert.Equal("Hello", run.Text);
    }
}
