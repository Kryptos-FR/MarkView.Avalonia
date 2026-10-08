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
using MarkView.Avalonia.Rendering.Blocks;
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

        var darkTokens = themeAware.HighlightVariant("var x = 1;".AsMemory(), "csharp", isDark: true);
        var lightTokens = themeAware.HighlightVariant("var x = 1;".AsMemory(), "csharp", isDark: false);

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

        var dark = themeAware.HighlightVariant("var x = 1;".AsMemory(), "csharp", isDark: true)!;
        var light = themeAware.HighlightVariant("var x = 1;".AsMemory(), "csharp", isDark: false)!;

        // At least one token should differ in colour between dark and light themes.
        var darkColours = dark.Select(t => t.Foreground?.ToString()).ToList();
        var lightColours = light.Select(t => t.Foreground?.ToString()).ToList();
        Assert.False(darkColours.SequenceEqual(lightColours),
            "Expected dark and light themes to produce at least one different token colour.");
    }

    private static (Border Border, TextBlock Text) SingleCodeBlock(StackPanel root)
    {
        var border = Assert.IsType<Border>(Assert.Single(root.Children));
        return (border, Assert.IsType<TextBlock>(border.Child));
    }

    [AvaloniaFact]
    public void Fenced_block_gets_language_class_and_no_wrapping()
    {
        var (border, text) = SingleCodeBlock(Render("""
        ```csharp
        var x = 1;
        ```
        """));

        Assert.Contains("language-csharp", border.Classes);
        Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
    }

    [AvaloniaFact]
    public void Code_block_keeps_no_wrapping_against_a_wrapping_style()
    {
        var root = Render("""
            ```csharp
            var x = 1;
            ```
            """);
        var (_, text) = SingleCodeBlock(root);
        var probe = new TextBlock();
        var window = new Window { Content = new StackPanel { Children = { root, probe } } };
        window.Styles.Add(new Style(x => x.OfType<TextBlock>())
        {
            Setters = { new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap) },
        });
        try
        {
            window.Show();

            Assert.Equal(TextWrapping.Wrap, probe.TextWrapping);
            Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Indented_code_block_gets_no_language_class()
    {
        var (border, _) = SingleCodeBlock(Render("    var x = 1;"));

        Assert.Contains("markdown-code-block", border.Classes);
        Assert.DoesNotContain(border.Classes, c => c.StartsWith("language-", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Empty_fenced_block_renders_without_inlines()
    {
        var (border, text) = SingleCodeBlock(Render("""
        ```csharp
        ```
        """));

        Assert.Contains("markdown-code-block", border.Classes);
        Assert.True(text.Inlines is null || text.Inlines.Count == 0);
    }

    [AvaloniaFact]
    public void Lines_are_separated_by_exactly_one_line_break()
    {
        var (_, text) = SingleCodeBlock(Render("""
        ```csharp
        int a;
        int b;
        ```
        """));

        var inlines = text.Inlines!.ToList();
        Assert.IsNotType<LineBreak>(inlines[0]);
        Assert.Single(inlines.OfType<LineBreak>());
        Assert.IsNotType<LineBreak>(inlines[^1]);
    }

    [AvaloniaFact]
    public void Tokens_without_colour_leave_run_foreground_unset()
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        new TextMateExtension().Register(renderer);
        renderer.CodeHighlighter = new NoColourHighlighter();
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse("""
            ```csharp
            x
            ```
            """, pipeline));

        var (_, text) = SingleCodeBlock(renderer.RootPanel);
        var run = Assert.IsType<Run>(Assert.Single(text.Inlines!));
        Assert.False(run.IsSet(TextElement.ForegroundProperty));
    }

    private sealed class NoColourHighlighter : ICodeHighlighter
    {
        public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language) =>
            [(line.ToString(), null)];
    }

    [AvaloniaFact]
    public void Register_replaces_the_default_code_block_renderer()
    {
        var renderer = new AvaloniaRenderer();

        new TextMateExtension().Register(renderer);

        Assert.DoesNotContain(renderer.ObjectRenderers, r => r is CodeBlockRenderer);
        Assert.Single(renderer.ObjectRenderers.OfType<TextMateCodeBlockRenderer>());
    }
}
