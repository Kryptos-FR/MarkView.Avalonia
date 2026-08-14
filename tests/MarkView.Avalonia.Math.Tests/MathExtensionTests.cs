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
}
