// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using Markdig;

using MarkView.Avalonia.Mermaid;
using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class MathExtensionTests
{
    [AvaloniaFact]
    public void MathExtension_Register_inserts_MathBlockRenderer_before_default_CodeBlockRenderer()
    {
        var renderer = new AvaloniaRenderer();
        new MathExtension().Register(renderer);

        var pipeline = new MarkdownPipelineBuilder().UseMathematics().Build();
        var document = Markdown.Parse("$$\nx^2\n$$", pipeline);
        pipeline.Setup(renderer);
        renderer.Render(document);

        var border = Assert.IsType<Border>(Assert.Single(renderer.RootPanel.Children));
        Assert.Contains("markdown-math-block", border.Classes);
    }

    [AvaloniaFact]
    public void MathExtension_registers_before_MermaidExtension_regardless_of_call_order()
    {
        // Math registered first, Mermaid second — Mermaid's Insert(0, ...) must not
        // steal $$ blocks away from MathBlockRenderer.
        var renderer = new AvaloniaRenderer();
        new MathExtension().Register(renderer);
        new MermaidExtension().Register(renderer);

        var pipeline = new MarkdownPipelineBuilder().UseMathematics().Build();
        var document = Markdown.Parse("$$\nx^2\n$$", pipeline);
        pipeline.Setup(renderer);
        renderer.Render(document);

        var border = Assert.IsType<Border>(Assert.Single(renderer.RootPanel.Children));
        Assert.Contains("markdown-math-block", border.Classes);
    }

    [AvaloniaFact]
    public void MermaidExtension_registers_before_MathExtension_regardless_of_call_order()
    {
        // Reverse order from the test above — must still be order-independent.
        var renderer = new AvaloniaRenderer();
        new MermaidExtension().Register(renderer);
        new MathExtension().Register(renderer);

        var pipeline = new MarkdownPipelineBuilder().UseMathematics().Build();
        var document = Markdown.Parse("$$\nx^2\n$$", pipeline);
        pipeline.Setup(renderer);
        renderer.Render(document);

        var border = Assert.IsType<Border>(Assert.Single(renderer.RootPanel.Children));
        Assert.Contains("markdown-math-block", border.Classes);
    }

    [AvaloniaFact]
    public void UseMath_adds_MathExtension_to_viewer()
    {
        var viewer = new MarkdownViewer();
        var result = viewer.UseMath();
        Assert.Same(viewer, result);
        Assert.Single(viewer.Extensions);
        Assert.IsType<MathExtension>(viewer.Extensions[0]);
    }

    [AvaloniaFact]
    public void UseMath_sets_pipeline_on_viewer()
    {
        var viewer = new MarkdownViewer();
        viewer.UseMath();
        Assert.NotNull(viewer.Pipeline);
    }

    [AvaloniaFact]
    public void UseMath_renders_math_correctly_even_when_markdown_was_set_before_UseMath()
    {
        // Regression test: Markdown is set BEFORE UseMath() is called, so the viewer's initial
        // render (triggered by the Markdown property changing) runs with neither the math
        // pipeline extension nor MathExtension's renderer registered. UseMath() must still end
        // up rendering the formula correctly once it runs — this ordering bug would not be
        // caught by tests that only inspect viewer.Pipeline/viewer.Extensions state.
        var viewer = new MarkdownViewer();
        viewer.Markdown = "$$\nx^2\n$$";
        viewer.UseMath();

        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var contentGrid = Assert.IsType<Grid>(scrollViewer.Content);
        var panel = Assert.IsType<StackPanel>(contentGrid.Children[0]);
        var border = Assert.IsType<Border>(Assert.Single(panel.Children));
        Assert.Contains("markdown-math-block", border.Classes);
        var image = Assert.IsType<Image>(border.Child);
        Assert.NotNull(image.Source);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void UseMath_and_UseMermaid_together_render_both_correctly_regardless_of_order(bool mathFirst)
    {
        var viewer = new MarkdownViewer();
        if (mathFirst)
        {
            viewer.UseMath();
            viewer.UseMermaid();
        }
        else
        {
            viewer.UseMermaid();
            viewer.UseMath();
        }

        viewer.Markdown = "$$\nx^2\n$$\n\n```mermaid\ngraph TD;\nA-->B;\n```";

        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var contentGrid = Assert.IsType<Grid>(scrollViewer.Content);
        var panel = Assert.IsType<StackPanel>(contentGrid.Children[0]);
        Assert.Equal(2, panel.Children.Count);

        var mathBorder = Assert.IsType<Border>(panel.Children[0]);
        Assert.Contains("markdown-math-block", mathBorder.Classes);
        var mathImage = Assert.IsType<Image>(mathBorder.Child);
        Assert.NotNull(mathImage.Source);

        var mermaidBorder = Assert.IsType<Border>(panel.Children[1]);
        Assert.Contains("markdown-mermaid", mermaidBorder.Classes);
        Assert.IsType<Image>(mermaidBorder.Child);
    }
}
