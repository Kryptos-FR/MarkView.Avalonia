using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using Markdig;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

public class MermaidBlockRendererTests
{
    private static StackPanel Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer();
        new MermaidExtension().Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    [AvaloniaFact]
    public void Mermaid_fence_renders_as_single_block()
    {
        var result = Render("```mermaid\ngraph TD\n  A --> B\n```");
        Assert.Single(result.Children);
    }

    [AvaloniaFact]
    public void Non_mermaid_fence_is_not_handled_by_MermaidBlockRenderer()
    {
        // A plain csharp fence must still produce a Border with markdown-code-block
        var result = Render("```csharp\nvar x = 1;\n```");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-code-block", border.Classes);
        Assert.DoesNotContain("markdown-mermaid-fallback", border.Classes);
    }

    [AvaloniaFact]
    public void MermaidExtension_Register_inserts_renderer_at_index_0()
    {
        var renderer = new AvaloniaRenderer();
        new MermaidExtension().Register(renderer);
        Assert.IsType<MermaidBlockRenderer>(renderer.ObjectRenderers[0]);
    }

    [AvaloniaFact]
    public void UseMermaid_adds_MermaidExtension_to_viewer()
    {
        var viewer = new MarkdownViewer();
        viewer.UseMermaid();
        Assert.Single(viewer.Extensions);
        Assert.IsType<MermaidExtension>(viewer.Extensions[0]);
    }
}
