// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using TextMateSharp.Grammars;

using Xunit;

namespace MarkView.Avalonia.SyntaxHighlighting.Tests;

/// <summary>Highlighted code blocks with <see cref="AvaloniaRenderer.IsLazyRenderingEnabled"/>.</summary>
public class TextMateLazyRenderingTests
{
    private const string Code = "```csharp\nvar x = 1;\n```";

    private static StackPanel Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer { IsLazyRenderingEnabled = true };
        new TextMateExtension(ThemeName.DarkPlus).Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    private static TextBlock CodeOf(Control block) => CodeBlockAssert.Layout(CodeBlockAssert.CodeBorder(block)).Code;

    private static bool IsColoured(TextBlock code) =>
        code.Inlines!.OfType<Run>().Any(r => r.IsSet(TextElement.ForegroundProperty));

    [AvaloniaFact]
    public void Block_far_below_the_visible_area_is_plain_text_until_scrolled_near()
    {
        var root = Render(Code);
        var code = CodeOf(Assert.Single(root.Children));
        var scrollViewer = new ScrollViewer { Content = new StackPanel { Children = { new Border { Height = 1000 }, root } } };
        var window = new Window { Width = 400, Height = 300, Content = scrollViewer };
        try
        {
            window.Show();
            Assert.False(IsColoured(code));
            Assert.Equal("var x = 1;", Assert.Single(code.Inlines!.OfType<Run>()).Text);

            scrollViewer.ScrollToEnd();
            window.UpdateLayout();

            Assert.True(IsColoured(code));
            Assert.Equal("var x = 1;", string.Concat(code.Inlines!.OfType<Run>().Select(r => r.Text)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Streamed_code_block_is_coloured_as_it_grows()
    {
        var viewer = new MarkdownViewer().UseTextMateHighlighting();
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        try
        {
            window.Show();
            var session = viewer.BeginStreaming();

            session.Append("```csharp\nvar x = 1;\n");
            await AsyncTestHelpers.WaitUntilAsync(() => StreamedCode(viewer) is { } code && IsColoured(code));

            session.Append("var y = 2;\n");
            await AsyncTestHelpers.WaitUntilAsync(() =>
                StreamedCode(viewer) is { } code && IsColoured(code) && code.Inlines!.OfType<Run>().Any(r => r.Text!.Contains("y")));

            session.Complete();
            window.UpdateLayout();
            Assert.True(IsColoured(StreamedCode(viewer)!));
        }
        finally
        {
            window.Close();
        }
    }

    private static TextBlock? StreamedCode(MarkdownViewer viewer) =>
        viewer.GetLogicalDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("markdown-code-block")) is { } border
            ? CodeBlockAssert.Layout(border).Code
            : null;
}
