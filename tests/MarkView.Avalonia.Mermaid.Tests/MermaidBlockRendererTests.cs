// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Markdig;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Tests.Shared;
using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

public class MermaidBlockRendererTests
{
    private static StackPanel Render(string markdown, ThemeLookup? themes = null)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer();
        if (themes is not null)
            renderer.Themes = themes;
        new MermaidExtension().Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    [AvaloniaFact]
    public void Non_mermaid_fence_gets_the_code_block_toolbar()
    {
        var result = Render("```csharp\nvar x = 1;\n```");
        var container = Assert.IsType<StackPanel>(Assert.Single(result.Children));
        Assert.Contains("markdown-code-container", container.Classes);
        var toolbar = Assert.IsType<Grid>(Assert.IsType<Border>(container.Children[0]).Child);
        Assert.Contains(toolbar.Children.OfType<TextBlock>(), tb => tb.Text == "csharp");
        Assert.Contains(toolbar.Children.OfType<Button>(), b => b.Classes.Contains("markdown-code-copy"));
        Assert.Same(CodeBlockAssert.CodeBorder(container), container.Children[1]);
    }

    [AvaloniaFact]
    public void Mermaid_fence_renders_border_with_mermaid_class()
    {
        var result = Render("```mermaid\ngraph TD\n  A --> B\n```");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-mermaid", border.Classes);
    }

    [AvaloniaFact]
    public void Mermaid_fence_border_takes_the_mermaid_theme()
    {
        var theme = new ControlTheme(typeof(Border));
        var result = Render("```mermaid\ngraph TD\n  A --> B\n```", new ThemeLookup(key => key == "MarkdownMermaidTheme" ? theme : null));
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Same(theme, border.Theme);
    }

    [AvaloniaFact]
    public void Mermaid_fence_border_contains_image()
    {
        var result = Render("```mermaid\ngraph TD\n  A --> B\n```");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.IsType<Image>(border.Child);
    }

    [AvaloniaFact]
    public void Mermaid_diagram_image_is_named_by_its_source()
    {
        var border = Assert.IsType<Border>(Assert.Single(Render("```mermaid\ngraph TD\n  A --> B\n```").Children));
        var peer = ControlAutomationPeer.CreatePeerForElement(Assert.IsType<Image>(border.Child));
        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
        Assert.Equal("graph TD\n  A --> B", peer.GetName());
    }

    [AvaloniaFact]
    public void Non_mermaid_fence_renders_with_code_block_class()
    {
        var result = Render("```csharp\nvar x = 1;\n```");
        var border = CodeBlockAssert.CodeBorder(Assert.Single(result.Children));
        Assert.Contains("markdown-code-block", border.Classes);
        Assert.DoesNotContain("markdown-mermaid-fallback", border.Classes);
    }

    [AvaloniaFact]
    public void Non_mermaid_fence_gets_language_class()
    {
        var result = Render("```csharp\nvar x = 1;\n```");
        var border = CodeBlockAssert.CodeBorder(Assert.Single(result.Children));
        Assert.Contains("language-csharp", border.Classes);
    }

    [AvaloniaFact]
    public void MermaidExtension_Register_inserts_renderer_before_default_CodeBlockRenderer()
    {
        var renderer = new AvaloniaRenderer();
        new MermaidExtension().Register(renderer);
        var renderers = renderer.ObjectRenderers.ToList();
        var mermaidIndex = renderers.FindIndex(r => r is MermaidBlockRenderer);
        var codeBlockIndex = renderers.FindIndex(r => r is CodeBlockRenderer);
        Assert.True(mermaidIndex >= 0 && codeBlockIndex >= 0 && mermaidIndex < codeBlockIndex);
    }

    [AvaloniaFact]
    public void UseMermaid_adds_MermaidExtension_to_viewer()
    {
        var viewer = new MarkdownViewer();
        viewer.UseMermaid();
        Assert.Single(viewer.Extensions);
        Assert.Equal(MermaidStyle.Quiet, Assert.IsType<MermaidExtension>(viewer.Extensions[0]).Style);
    }

    [AvaloniaTheory]
    [InlineData(MermaidStyle.Quiet)]
    [InlineData(MermaidStyle.Blueprint)]
    [InlineData(MermaidStyle.Tonal)]
    public void The_style_given_to_UseMermaid_reaches_the_block_renderer(MermaidStyle style)
    {
        var viewer = new MarkdownViewer().UseMermaid(style);
        var renderer = new AvaloniaRenderer();

        Assert.Equal(style, Assert.IsType<MermaidExtension>(Assert.Single(viewer.Extensions)).Style);
        viewer.Extensions[0].Register(renderer);

        Assert.Equal(style, Assert.Single(renderer.ObjectRenderers.OfType<MermaidBlockRenderer>()).Style);
    }

    [Fact]
    public void A_block_renderer_paints_in_the_quiet_style_by_default() =>
        Assert.Equal(MermaidStyle.Quiet, new MermaidBlockRenderer().Style);
}
