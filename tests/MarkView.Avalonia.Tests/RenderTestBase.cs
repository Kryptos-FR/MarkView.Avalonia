using Avalonia.Controls;
using Markdig;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Tests;

public abstract class RenderTestBase
{
    protected static StackPanel Render(string markdown, MarkdownPipeline? pipeline = null)
    {
        pipeline ??= new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }
}
