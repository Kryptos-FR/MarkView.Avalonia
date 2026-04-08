using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class ParagraphTests : RenderTestBase
{
    [AvaloniaFact]
    public void Simple_paragraph_renders_as_TextBlock_with_Run()
    {
        var result = Render("Hello, world!");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var run = Assert.IsType<Run>(Assert.Single(textBlock.Inlines!));
        Assert.Equal("Hello, world!", run.Text);
    }

    [AvaloniaFact]
    public void Multiple_paragraphs_render_as_separate_TextBlocks()
    {
        var result = Render("First paragraph.\n\nSecond paragraph.");
        Assert.Equal(2, result.Children.Count);
        Assert.All(result.Children, child => Assert.IsType<TextBlock>(child));
    }

    [AvaloniaFact]
    public void Paragraph_TextBlock_has_text_wrapping()
    {
        var result = Render("Hello, world!");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        Assert.Equal(TextWrapping.Wrap, textBlock.TextWrapping);
    }
}
