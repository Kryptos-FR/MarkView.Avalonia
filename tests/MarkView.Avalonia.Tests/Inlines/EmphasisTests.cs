using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class EmphasisTests : RenderTestBase
{
    [AvaloniaFact]
    public void Bold_text_renders_as_Bold_inline()
    {
        var result = Render("**bold**");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var bold = Assert.IsType<Bold>(Assert.Single(textBlock.Inlines!));
        var run = Assert.IsType<Run>(Assert.Single(bold.Inlines));
        Assert.Equal("bold", run.Text);
    }

    [AvaloniaFact]
    public void Italic_text_renders_as_Italic_inline()
    {
        var result = Render("*italic*");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var italic = Assert.IsType<Italic>(Assert.Single(textBlock.Inlines!));
        var run = Assert.IsType<Run>(Assert.Single(italic.Inlines));
        Assert.Equal("italic", run.Text);
    }

    [AvaloniaFact]
    public void Mixed_emphasis_in_paragraph()
    {
        var result = Render("normal **bold** and *italic*");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var inlines = textBlock.Inlines!.ToList();
        Assert.Equal(4, inlines.Count); // "normal " + Bold + " and " + Italic
    }
}
