// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;

using Markdig;
using Markdig.Extensions.Mathematics;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class MathStreamingTests
{
    /// <summary>
    /// Renders <paramref name="markdown"/> through a streaming renderer with the math renderer registered.
    /// When <paramref name="withCodeBlockRenderer"/> is <c>false</c>, every other renderer that accepts a
    /// <see cref="MathBlock"/> is removed first, so the math renderer has no fallback.
    /// </summary>
    private static StackPanel Render(string markdown, bool provisional, bool withCodeBlockRenderer = true)
    {
        var pipeline = new MarkdownPipelineBuilder().UseMathematics().UseMathBracketDelimiters().Build();
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = provisional ? 0 : int.MaxValue };
        if (!withCodeBlockRenderer)
            renderer.ObjectRenderers.RemoveAll(r => r is not MathBlockRenderer && r.Accept(renderer, typeof(MathBlock)));
        new MathExtension().Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    [AvaloniaTheory]
    [InlineData("$$\nx^2")]
    [InlineData("$$\nx^2\n$$")]
    [InlineData("\\[\nx^2\n\\]")]
    public void Provisional_math_block_renders_as_a_code_block(string markdown)
    {
        var child = Assert.Single(Render(markdown, provisional: true).Children);
        var container = Assert.IsType<StackPanel>(child);
        Assert.Contains("markdown-code-container", container.Classes);
        Assert.Equal("x^2", CodeText(CodeBlockAssert.Layout(CodeBlockAssert.CodeBorder(container)).Code));
        Assert.DoesNotContain(container.GetLogicalDescendants().OfType<Border>(), b => b.Classes.Contains("markdown-math-block"));
    }

    [AvaloniaFact]
    public void Math_block_before_the_tail_renders_the_formula_while_streaming()
    {
        var child = Assert.Single(Render("$$\nx^2\n$$", provisional: false).Children);
        Assert.Contains("markdown-math-block", Assert.IsType<Border>(child).Classes);
    }

    [AvaloniaFact]
    public void Provisional_math_block_renders_the_formula_when_no_other_renderer_accepts_it()
    {
        var child = Assert.Single(Render("$$\nx^2", provisional: true, withCodeBlockRenderer: false).Children);
        Assert.Contains("markdown-math-block", Assert.IsType<Border>(child).Classes);
    }

    [AvaloniaFact]
    public async Task Math_block_renders_the_formula_once_its_block_closes()
    {
        var viewer = new MarkdownViewer().UseMath();
        viewer.Markdown = "";
        using var session = viewer.BeginStreaming(TimeSpan.FromMilliseconds(1));
        session.Append("$$\nx^2\n$$");
        await AsyncTestHelpers.WaitUntilAsync(() => TryRoot(viewer)?.Children is [StackPanel]);

        session.Append("\n\nAfter the formula.");
        await AsyncTestHelpers.WaitUntilAsync(() => TryRoot(viewer)?.Children.Count == 2);
        Assert.Contains("markdown-math-block", Assert.IsType<Border>(Root(viewer).Children[0]).Classes);
    }

    /// <summary>The rendered root panel, or <c>null</c> before the first update has produced any content.</summary>
    private static StackPanel? TryRoot(MarkdownViewer viewer) => (viewer.Content as Grid)?.Children[0] as StackPanel;

    private static StackPanel Root(MarkdownViewer viewer) => Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);

    /// <summary>The code block's text: its runs joined, with a line break for each <see cref="LineBreak"/> inline.</summary>
    private static string CodeText(TextBlock code) =>
        string.Concat(code.Inlines!.Select(inline => inline is LineBreak ? "\n" : Assert.IsType<Run>(inline).Text));
}
