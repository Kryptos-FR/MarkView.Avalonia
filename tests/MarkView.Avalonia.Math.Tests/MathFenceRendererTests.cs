// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class MathFenceRendererTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Math_blocks_keep_rendering_as_math_next_to_a_fence_renderer(bool fenceRendererFirst)
    {
        var viewer = new MarkdownViewer();
        if (fenceRendererFirst)
            viewer.UseFenceRenderer("chart", (_, _) => new Button()).UseMath();
        else
            viewer.UseMath().UseFenceRenderer("chart", (_, _) => new Button());

        viewer.Markdown = "$$\nx^2\n$$\n\n```chart\nx\n```";

        var blocks = Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]).Children;
        Assert.Contains("markdown-math-block", blocks[0].Classes);
        Assert.Contains("markdown-custom-fence", blocks[1].Classes);
    }
}
