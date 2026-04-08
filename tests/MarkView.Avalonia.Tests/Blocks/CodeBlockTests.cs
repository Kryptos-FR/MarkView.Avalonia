using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class CodeBlockTests : RenderTestBase
{
    [AvaloniaFact]
    public void Fenced_code_block_renders_as_Border_with_TextBlock()
    {
        var result = Render("```\nvar x = 1;\n```");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-code-block", border.Classes);
        var textBlock = Assert.IsType<TextBlock>(border.Child);
        Assert.Contains("var x = 1;", textBlock.Inlines!.OfType<Run>().First().Text!);
    }

    [AvaloniaFact]
    public void Fenced_code_block_with_language_has_data_attribute()
    {
        var result = Render("```csharp\nvar x = 1;\n```");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-code-block", border.Classes);
        Assert.Contains("language-csharp", border.Classes);
    }

    [AvaloniaFact]
    public void Indented_code_block_renders_as_Border_with_TextBlock()
    {
        var result = Render("    indented code");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-code-block", border.Classes);
    }
}
