// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using CSharpMath.Avalonia;

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
    public void Math_block_border_contains_math_view()
    {
        var result = Render("$$\nx^2\n$$");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.IsType<MathView>(border.Child);
    }

    [AvaloniaFact]
    public void Math_block_math_view_has_expected_latex()
    {
        var result = Render("$$\nx^2\n$$");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        var mathView = Assert.IsType<MathView>(border.Child);
        Assert.Equal("x^2", mathView.LaTeX);
    }

    [AvaloniaFact]
    public void Math_block_with_pathologically_nested_braces_falls_back_instead_of_crashing()
    {
        var nested = new string('{', 60) + "x" + new string('}', 60); // 60 > MaxBraceNestingDepth (50) — throws before ever touching CSharpMath
        var result = Render($"$$\n{nested}\n$$");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-math-fallback", border.Classes);
    }
}
