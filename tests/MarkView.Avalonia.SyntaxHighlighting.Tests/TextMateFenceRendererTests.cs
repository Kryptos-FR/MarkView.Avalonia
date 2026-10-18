// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.SyntaxHighlighting.Tests;

public class TextMateFenceRendererTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Fence_renderer_wins_over_highlighting_and_other_languages_stay_highlighted(bool fenceRendererFirst)
    {
        var viewer = new MarkdownViewer();
        if (fenceRendererFirst)
            viewer.UseFenceRenderer("csharp", (_, _) => new Button()).UseTextMateHighlighting();
        else
            viewer.UseTextMateHighlighting().UseFenceRenderer("csharp", (_, _) => new Button());

        viewer.Markdown = "```csharp\nvar x = 1;\n```\n\n```python\nx = 1\n```";

        var blocks = Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]).Children;
        var custom = Assert.IsType<Border>(blocks[0], exactMatch: false);
        Assert.Contains("markdown-custom-fence", custom.Classes);
        Assert.IsType<Button>(custom.Child);
        var code = Assert.IsType<TextBlock>(CodeBlockAssert.CodeBorder(blocks[1]).Child);
        Assert.Contains(code.Inlines!.OfType<Run>(), r => r.Foreground != null);
    }
}
