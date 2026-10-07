// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
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

public class MermaidCodeBlockFallbackTests
{
    private sealed class PrefixHighlighter : ICodeHighlighter
    {
        public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language) =>
            [("kw", Brushes.Red), (line.ToString(), null)];
    }

    private sealed class VariantHighlighter : IThemeAwareCodeHighlighter
    {
        public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language) =>
            throw new InvalidOperationException("Theme-aware path must call HighlightVariant.");

        public IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(ReadOnlyMemory<char> line, string? language, bool isDark) =>
            [(line.ToString(), isDark ? Brushes.White : Brushes.Black)];
    }

    private static (StackPanel Root, Border Border, TextBlock Text) Render(string markdown, ICodeHighlighter? highlighter = null)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        new MermaidExtension().Register(renderer);
        renderer.CodeHighlighter = highlighter;
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        var border = Assert.IsType<Border>(Assert.Single(renderer.RootPanel.Children));
        return (renderer.RootPanel, border, Assert.IsType<TextBlock>(border.Child));
    }

    [AvaloniaFact]
    public void Multi_line_block_without_highlighter_renders_plain_lines_separated_by_line_breaks()
    {
        var (_, _, text) = Render("```text\nfirst\nsecond\n```");

        var inlines = text.Inlines!.ToList();
        Assert.Equal(3, inlines.Count);
        Assert.Equal("first", Assert.IsType<Run>(inlines[0]).Text);
        Assert.IsType<LineBreak>(inlines[1]);
        Assert.Equal("second", Assert.IsType<Run>(inlines[2]).Text);
    }

    [AvaloniaFact]
    public void Highlighter_tokens_become_runs_and_only_coloured_tokens_set_foreground()
    {
        var (_, _, text) = Render("```cs\nx\n```", new PrefixHighlighter());

        var runs = text.Inlines!.OfType<Run>().ToList();
        Assert.Equal(["kw", "x"], runs.Select(r => r.Text));
        Assert.Same(Brushes.Red, runs[0].Foreground);
        Assert.False(runs[1].IsSet(TextElement.ForegroundProperty));
    }

    [AvaloniaFact]
    public void Fence_without_info_string_gets_no_language_class()
    {
        var (_, border, _) = Render("```\ncode\n```");

        Assert.DoesNotContain(border.Classes, c => c.StartsWith("language-", StringComparison.Ordinal));
        Assert.Contains("markdown-code-block", border.Classes);
    }

    [AvaloniaFact]
    public void Empty_fence_renders_an_empty_code_block()
    {
        var (_, border, text) = Render("```cs\n```");

        Assert.Contains("markdown-code-block", border.Classes);
        Assert.True(text.Inlines is null || text.Inlines.Count == 0);
    }

    [AvaloniaFact]
    public void Theme_aware_highlighter_uses_current_variant_and_rebuilds_in_place_on_theme_change()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, _, text) = Render("```cs\na\nb\n```", new VariantHighlighter());
        Assert.All(text.Inlines!.OfType<Run>(), r => Assert.Same(Brushes.Black, r.Foreground));

        theme.Switch(ThemeVariant.Dark);

        Assert.Equal(3, text.Inlines!.Count);
        Assert.All(text.Inlines!.OfType<Run>(), r => Assert.Same(Brushes.White, r.Foreground));
    }

    [AvaloniaFact]
    public async Task Code_block_removed_from_tree_stops_following_theme_changes()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (root, _, text) = Render("```cs\na\n```", new VariantHighlighter());
        var window = new Window { Content = root };
        try
        {
            window.Show();

            window.Content = null;
            theme.Switch(ThemeVariant.Dark);
            await AsyncTestHelpers.PumpAsync();

            Assert.Same(Brushes.Black, Assert.IsType<Run>(Assert.Single(text.Inlines!)).Foreground);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Code_block_never_wraps_even_when_an_ancestor_style_enables_wrapping()
    {
        var (root, _, text) = Render("```text\nfirst\n```");
        var window = new Window { Content = root };
        window.Styles.Add(new Style(x => x.OfType<TextBlock>())
        {
            Setters = { new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap) },
        });
        try
        {
            window.Show();

            Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Unrelated_application_property_change_does_not_rebuild_the_inlines()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, _, text) = Render("```cs\na\n```", new VariantHighlighter());
        var before = Assert.Single(text.Inlines!);
        var app = Application.Current!;
        var savedName = app.Name;

        try
        {
            app.Name = "MermaidCodeBlockFallbackTests";
            await AsyncTestHelpers.PumpAsync();
        }
        finally
        {
            app.Name = savedName;
        }

        Assert.Same(before, Assert.Single(text.Inlines!));
    }
}
