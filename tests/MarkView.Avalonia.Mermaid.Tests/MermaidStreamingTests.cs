// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

public class MermaidStreamingTests
{
    private const string OpenDiagram = "```mermaid\ngraph TD\n  A --> B";

    private static StackPanel Render(string markdown, bool provisional)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = provisional ? 0 : int.MaxValue };
        new MermaidExtension().Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    [AvaloniaFact]
    public void Provisional_mermaid_fence_renders_as_a_code_block()
    {
        var child = Assert.Single(Render(OpenDiagram, provisional: true).Children);
        var container = Assert.IsType<StackPanel>(child);
        Assert.Contains("markdown-code-container", container.Classes);
        var border = CodeBlockAssert.CodeBorder(container);
        Assert.Contains("language-mermaid", border.Classes);
        Assert.Equal("graph TD\n  A --> B", CodeText(CodeBlockAssert.Layout(border).Code));   // exact source, no fence
        Assert.DoesNotContain(container.GetLogicalDescendants().OfType<Border>(), b => b.Classes.Contains("markdown-mermaid"));
    }

    [AvaloniaFact]
    public void Closed_mermaid_fence_at_the_end_of_the_text_is_still_provisional()
    {
        var child = Assert.Single(Render(OpenDiagram + "\n```", provisional: true).Children);
        Assert.Contains("markdown-code-container", Assert.IsType<StackPanel>(child).Classes);
    }

    [AvaloniaFact]
    public void Mermaid_fence_before_the_tail_renders_the_diagram_while_streaming()
    {
        var child = Assert.Single(Render(OpenDiagram + "\n```", provisional: false).Children);
        Assert.Contains("markdown-mermaid", Assert.IsType<Border>(child).Classes);
    }

    [AvaloniaFact]
    public async Task Mermaid_fence_renders_the_diagram_once_its_block_closes()
    {
        var viewer = new MarkdownViewer().UseMermaid();
        viewer.Markdown = "";
        using var session = viewer.BeginStreaming(TimeSpan.FromMilliseconds(1));
        session.Append(OpenDiagram + "\n```");
        await AsyncTestHelpers.WaitUntilAsync(() => TryRoot(viewer)?.Children is [StackPanel]);
        Assert.Contains("markdown-code-container", Assert.IsType<StackPanel>(Root(viewer).Children[0]).Classes);

        session.Append("\n\nAfter the diagram.");
        await AsyncTestHelpers.WaitUntilAsync(() => TryRoot(viewer)?.Children.Count == 2);
        Assert.Contains("markdown-mermaid", Assert.IsType<Border>(Root(viewer).Children[0]).Classes);
    }

    [AvaloniaFact]
    public void Mermaid_fence_that_ends_the_streamed_text_grows_in_chunks_of_lines()
    {
        var viewer = new MarkdownViewer().UseMermaid();
        var session = viewer.BeginStreaming();
        session.Append(OpenDiagram + "\n" + string.Concat(Enumerable.Range(0, 40).Select(i => $"  N{i} --> N{i + 1}\n")));
        session.ApplyPendingUpdate();

        var border = CodeBlockAssert.CodeBorder(Assert.Single(Root(viewer).Children));
        Assert.Contains("language-mermaid", border.Classes);
        Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<TextBlock>>(CodeBlockToolbar.FindCodeChunks(border)).Count());
    }

    /// <summary>The rendered root panel, or <c>null</c> before the first update has produced any content.</summary>
    private static StackPanel? TryRoot(MarkdownViewer viewer) => (viewer.Content as Grid)?.Children[0] as StackPanel;

    private static StackPanel Root(MarkdownViewer viewer) => Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);

    /// <summary>The code block's text: its runs joined, with a line break for each <see cref="LineBreak"/> inline.</summary>
    private static string CodeText(TextBlock code) =>
        string.Concat(code.Inlines!.Select(inline => inline is LineBreak ? "\n" : Assert.IsType<Run>(inline).Text));
}
