// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

public class MermaidFenceRendererTests
{
    private static Controls Render(bool fenceRendererFirst)
    {
        var viewer = new MarkdownViewer();
        if (fenceRendererFirst)
            viewer.UseFenceRenderer("mermaid", (_, _) => new Button()).UseMermaid();
        else
            viewer.UseMermaid().UseFenceRenderer("mermaid", (_, _) => new Button());
        viewer.UseFenceRenderer("chart", (_, _) => null);

        viewer.Markdown = "```mermaid\ngraph TD;\nA-->B;\n```\n\n```chart\nx\n```\n\n```python\ny\n```";

        return Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]).Children;
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Fence_renderer_wins_over_Mermaid_whatever_the_registration_order(bool fenceRendererFirst)
    {
        var blocks = Render(fenceRendererFirst);

        var border = Assert.IsType<Border>(blocks[0]);
        Assert.Contains("markdown-custom-fence", border.Classes);
        Assert.IsType<Button>(border.Child);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Blocks_a_fence_renderer_declines_or_does_not_handle_render_through_Mermaid(bool fenceRendererFirst)
    {
        var blocks = Render(fenceRendererFirst);

        Assert.Equal(3, blocks.Count);
        Assert.Contains("language-chart", CodeBlockAssert.CodeBorder(blocks[1]).Classes);
        Assert.Contains("language-python", CodeBlockAssert.CodeBorder(blocks[2]).Classes);
    }
}
