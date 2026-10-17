// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

/// <summary>
/// The line-number, wrapping and maximum-height options of the code block toolbar applied to the
/// fenced code blocks the Mermaid renderer writes as code, and left out of its diagrams.
/// </summary>
public class MermaidCodeBlockOptionsTests
{
    private const string Code = "```text\nfirst\nsecond\n```";

    private sealed class VariantHighlighter : IThemeAwareCodeHighlighter
    {
        public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language) =>
            throw new InvalidOperationException("Theme-aware path must call HighlightVariant.");

        public IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(ReadOnlyMemory<char> line, string? language, bool isDark) =>
            [(line.ToString(), isDark ? Brushes.White : Brushes.Black)];
    }

    private static StackPanel Render(string markdown, bool lineNumbers, bool wrap, double maxHeight,
        ICodeHighlighter? highlighter = null)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer
        {
            ShowCodeBlockLineNumbers = lineNumbers,
            WrapCodeBlockLines = wrap,
            CodeBlockMaxHeight = maxHeight,
        };
        new MermaidExtension().Register(renderer);
        renderer.CodeHighlighter = highlighter;
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    [AvaloniaTheory]
    [InlineData(true, false, double.PositiveInfinity)]
    [InlineData(false, true, double.PositiveInfinity)]
    [InlineData(false, false, 60)]
    [InlineData(true, true, 60)]
    public void Code_fence_gets_the_code_block_options(bool lineNumbers, bool wrap, double maxHeight)
    {
        var root = Render(Code, lineNumbers, wrap, maxHeight);
        var border = CodeBlockAssert.CodeBorder(Assert.Single(root.Children));

        var (scroll, numbers, code) = CodeBlockAssert.Layout(border);
        Assert.Equal(!double.IsPositiveInfinity(maxHeight), scroll is not null);
        Assert.Equal(maxHeight, border.MaxHeight);
        Assert.Equal(lineNumbers ? "1\n2" : null, numbers?.Text);
        Assert.Equal(wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, code.TextWrapping);
        Assert.Equal(["first", "second"], code.Inlines!.OfType<Run>().Select(r => r.Text));
    }

    [AvaloniaFact]
    public void Diagram_gets_none_of_the_code_block_options()
    {
        var root = Render("```mermaid\ngraph TD\n  A --> B\n```", lineNumbers: true, wrap: true, maxHeight: 60);

        var border = Assert.IsType<Border>(Assert.Single(root.Children));
        Assert.Contains("markdown-mermaid", border.Classes);
        Assert.IsType<Image>(border.Child);
        Assert.Equal(double.PositiveInfinity, border.MaxHeight);
    }

    [AvaloniaFact]
    public void Theme_switch_rebuilds_the_runs_of_line_numbered_scrolling_code()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Code, lineNumbers: true, wrap: true, maxHeight: 60, new VariantHighlighter());
        var border = CodeBlockAssert.CodeBorder(Assert.Single(root.Children));
        var (_, numbers, code) = CodeBlockAssert.Layout(border);
        var window = new Window { Content = root };
        try
        {
            window.Show();
            Assert.All(code.Inlines!.OfType<Run>(), r => Assert.Same(Brushes.Black, r.Foreground));

            theme.Switch(ThemeVariant.Dark);

            Assert.Equal(3, code.Inlines!.Count);
            Assert.All(code.Inlines!.OfType<Run>(), r => Assert.Same(Brushes.White, r.Foreground));
            Assert.Equal("1\n2", numbers!.Text);
            Assert.Same(code, CodeBlockAssert.Layout(border).Code);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_everything_copies_code_and_diagram_source_without_line_numbers()
    {
        var viewer = new MarkdownViewer
        {
            ShowCodeBlockLineNumbers = true,
            CodeBlockMaxHeight = 40,
        }.UseMermaid();
        viewer.Markdown = "Intro\n\n" + Code + "\n\n```mermaid\ngraph TD\n  A --> B\n```\n\nOutro";

        viewer.SelectAll();

        // Blocks are joined by '\n', and so is the diagram's source; the lines inside a code block
        // keep the platform's line ending.
        Assert.Equal($"Intro\nfirst{Environment.NewLine}second\n```mermaid\ngraph TD\n  A --> B\n```\nOutro", viewer.GetSelectedText());
    }
}
