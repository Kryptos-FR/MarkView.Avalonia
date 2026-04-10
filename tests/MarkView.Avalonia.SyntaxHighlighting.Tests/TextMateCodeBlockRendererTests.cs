// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
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

    [AvaloniaFact]
    public void TextMateExtension_Register_sets_theme_aware_CodeHighlighter()
    {
        var renderer = new AvaloniaRenderer();
        new TextMateExtension().Register(renderer);
        Assert.IsAssignableFrom<IThemeAwareCodeHighlighter>(renderer.CodeHighlighter);
    }

    [AvaloniaFact]
    public void Theme_aware_highlighter_returns_tokens_for_both_variants()
    {
        var renderer = new AvaloniaRenderer();
        new TextMateExtension().Register(renderer);
        var themeAware = (IThemeAwareCodeHighlighter)renderer.CodeHighlighter!;

        var darkTokens = themeAware.HighlightVariant("var x = 1;", "csharp", isDark: true);
        var lightTokens = themeAware.HighlightVariant("var x = 1;", "csharp", isDark: false);

        Assert.NotNull(darkTokens);
        Assert.NotNull(lightTokens);
        Assert.NotEmpty(darkTokens);
        Assert.NotEmpty(lightTokens);
    }

    [AvaloniaFact]
    public void Dark_and_light_variants_produce_different_token_colours()
    {
        var renderer = new AvaloniaRenderer();
        new TextMateExtension().Register(renderer);
        var themeAware = (IThemeAwareCodeHighlighter)renderer.CodeHighlighter!;

        var dark = themeAware.HighlightVariant("var x = 1;", "csharp", isDark: true)!;
        var light = themeAware.HighlightVariant("var x = 1;", "csharp", isDark: false)!;

        // At least one token should differ in colour between dark and light themes.
        var darkColours = dark.Select(t => t.Foreground?.ToString()).ToList();
        var lightColours = light.Select(t => t.Foreground?.ToString()).ToList();
        Assert.False(darkColours.SequenceEqual(lightColours),
            "Expected dark and light themes to produce at least one different token colour.");
    }
}
