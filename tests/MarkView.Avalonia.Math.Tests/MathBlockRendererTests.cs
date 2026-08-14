// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using Markdig;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class MathBlockRendererTests
{
    private static StackPanel Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseMathematics().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer();
        renderer.ObjectRenderers.Insert(0, new MathBlockRenderer());
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    [AvaloniaFact]
    public void Math_block_renders_border_with_math_block_class()
    {
        var result = Render("$$\nx^2\n$$");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-math-block", border.Classes);
    }

    [AvaloniaFact]
    public void Math_block_border_contains_image()
    {
        var result = Render("$$\nx^2\n$$");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.IsType<Image>(border.Child);
    }

    [AvaloniaFact]
    public void Math_block_image_has_non_null_bitmap_source()
    {
        var result = Render("$$\nx^2\n$$");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        var image = Assert.IsType<Image>(border.Child);
        Assert.NotNull(image.Source);
    }
}
