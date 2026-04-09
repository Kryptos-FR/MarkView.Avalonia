using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.SyntaxHighlighting;
using TextMateSharp.Grammars;
using Xunit;

namespace MarkView.Avalonia.SyntaxHighlighting.Tests;

public class TextMateCodeBlockRendererTests
{
    // Helper: render markdown with the TextMate extension active
    private static StackPanel Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer();
        var extension = new TextMateExtension(ThemeName.DarkPlus);
        extension.Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    [AvaloniaFact]
    public void Fenced_csharp_block_renders_as_Border_with_TextBlock()
    {
        var result = Render("```csharp\nvar x = 1;\n```");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-code-block", border.Classes);
        var textBlock = Assert.IsType<TextBlock>(border.Child);
        Assert.NotNull(textBlock.Inlines);
        Assert.NotEmpty(textBlock.Inlines);
    }

    [AvaloniaFact]
    public void Fenced_csharp_block_produces_coloured_Runs()
    {
        var result = Render("```csharp\nvar x = 1;\n```");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        var textBlock = Assert.IsType<TextBlock>(border.Child);
        // At least one Run with a non-null Foreground should exist
        var runs = textBlock.Inlines!.OfType<Run>().ToList();
        Assert.NotEmpty(runs);
        Assert.Contains(runs, r => r.Foreground != null);
    }

    [AvaloniaFact]
    public void Fenced_block_with_unknown_language_falls_back_to_plain_runs()
    {
        var result = Render("```notareallanguage\nsome code\n```");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        var textBlock = Assert.IsType<TextBlock>(border.Child);
        var runs = textBlock.Inlines!.OfType<Run>().ToList();
        Assert.NotEmpty(runs);
        Assert.Equal("some code", string.Concat(runs.Select(r => r.Text)));
    }

    [AvaloniaFact]
    public void TextMateExtension_Register_sets_CodeHighlighter_on_renderer()
    {
        var renderer = new AvaloniaRenderer();
        var extension = new TextMateExtension(ThemeName.DarkPlus);
        extension.Register(renderer);
        Assert.NotNull(renderer.CodeHighlighter);
    }

    [AvaloniaFact]
    public void UseTextMateHighlighting_wires_extension_on_viewer()
    {
        var viewer = new MarkdownViewer();
        viewer.UseTextMateHighlighting();
        Assert.Single(viewer.Extensions);
        Assert.IsType<TextMateExtension>(viewer.Extensions[0]);
    }
}
