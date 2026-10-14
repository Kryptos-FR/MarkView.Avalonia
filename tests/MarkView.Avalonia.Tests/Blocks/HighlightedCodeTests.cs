// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

using Markdig;
using Markdig.Syntax;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class HighlightedCodeTests
{
    private static readonly IBrush DarkKeyword = new SolidColorBrush(Colors.LightBlue).ToImmutable();
    private static readonly IBrush LightKeyword = new SolidColorBrush(Colors.DarkBlue).ToImmutable();
    private static readonly IBrush DarkOnly = new SolidColorBrush(Colors.Orange).ToImmutable();
    private static readonly IBrush LightOnly = new SolidColorBrush(Colors.Purple).ToImmutable();

    // Each line: "kw" coloured in both variants, "dark" coloured only in dark, "light" coloured
    // only in light, then the line itself uncoloured.
    private sealed class FakeDualHighlighter : IDualThemeCodeHighlighter
    {
        public int Calls { get; private set; }

        public IReadOnlyList<(string Text, IBrush? Dark, IBrush? Light)>? HighlightBoth(ReadOnlyMemory<char> line, string? language)
        {
            Calls++;
            return language == "fake" ? [("kw", DarkKeyword, LightKeyword), ("dark", DarkOnly, null), ("light", null, LightOnly), (line.ToString(), null, null)] : null;
        }

        public IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(ReadOnlyMemory<char> line, string? language, bool isDark) =>
            throw new InvalidOperationException("A dual-theme highlighter is only asked for both variants at once.");

        public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language) =>
            throw new InvalidOperationException("A dual-theme highlighter is only asked for both variants at once.");
    }

    // Each line: "kw" coloured for the requested variant, then the line itself uncoloured.
    private sealed class FakeThemeAwareHighlighter : IThemeAwareCodeHighlighter
    {
        public IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(ReadOnlyMemory<char> line, string? language, bool isDark) =>
            [("kw", isDark ? DarkKeyword : LightKeyword), (line.ToString(), null)];

        public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language) =>
            throw new InvalidOperationException("A theme-aware highlighter is asked for the current variant.");
    }

    // Each line: "kw" coloured, then the line itself uncoloured.
    private sealed class FakeHighlighter : ICodeHighlighter
    {
        public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language) =>
            [("kw", LightKeyword), (line.ToString(), null)];
    }

    // Every token is coloured in both variants, as with a typical TextMate theme pair.
    private sealed class FullyColouredDualHighlighter : IDualThemeCodeHighlighter
    {
        public IReadOnlyList<(string Text, IBrush? Dark, IBrush? Light)>? HighlightBoth(ReadOnlyMemory<char> line, string? language) =>
            [("kw", DarkKeyword, LightKeyword)];

        public IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(ReadOnlyMemory<char> line, string? language, bool isDark) =>
            throw new InvalidOperationException("A dual-theme highlighter is only asked for both variants at once.");

        public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language) =>
            throw new InvalidOperationException("A dual-theme highlighter is only asked for both variants at once.");
    }

    private static (Border Border, TextBlock Text) Write(ICodeHighlighter? highlighter, string language = "fake")
    {
        var block = Assert.IsType<FencedCodeBlock>(Assert.Single(Markdown.Parse($"```{language}\nfirst\nsecond\n```")));
        var text = new TextBlock();
        var border = new Border { Child = text };
        HighlightedCode.Write(border, text, block, language, highlighter);
        return (border, text);
    }

    private static Color? ColourOf(Run run) =>
        run.IsSet(TextElement.ForegroundProperty) ? ((ISolidColorBrush)run.Foreground!).Color : null;

    [AvaloniaFact]
    public void Dual_highlighter_colours_tokens_for_the_light_variant()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, text) = Write(new FakeDualHighlighter());

        Assert.IsType<Run>(text.Inlines![0]);
        Assert.Single(text.Inlines!.OfType<LineBreak>());
        var runs = text.Inlines!.OfType<Run>().ToList();
        Assert.Equal(["kw", "dark", "light", "first", "kw", "dark", "light", "second"], runs.Select(r => r.Text));
        Assert.Equal(Colors.DarkBlue, ColourOf(runs[0]));
        Assert.Null(ColourOf(runs[1]));
        Assert.Equal(Colors.Purple, ColourOf(runs[2]));
        Assert.Null(ColourOf(runs[3]));
    }

    [AvaloniaFact]
    public void Dual_highlighter_colours_tokens_for_the_dark_variant()
    {
        using var theme = new ThemeScope(ThemeVariant.Dark);
        var (_, text) = Write(new FakeDualHighlighter());

        var runs = text.Inlines!.OfType<Run>().ToList();
        Assert.Equal(Colors.LightBlue, ColourOf(runs[0]));
        Assert.Equal(Colors.Orange, ColourOf(runs[1]));
        Assert.Null(ColourOf(runs[2]));
        Assert.Null(ColourOf(runs[3]));
    }

    [AvaloniaFact]
    public void Theme_switch_recolours_the_same_runs_without_highlighting_again()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var highlighter = new FakeDualHighlighter();
        var (border, text) = Write(highlighter);
        var window = new Window { Content = border };
        try
        {
            window.Show();
            var before = text.Inlines!.ToList();

            theme.Switch(ThemeVariant.Dark);

            Assert.Equal(before, text.Inlines!.ToList());
            Assert.Equal(2, highlighter.Calls);
            var runs = text.Inlines!.OfType<Run>().ToList();
            Assert.Equal(Colors.LightBlue, ColourOf(runs[0]));
            Assert.Equal(Colors.Orange, ColourOf(runs[1]));
            Assert.Null(ColourOf(runs[2]));
            Assert.Null(ColourOf(runs[3]));

            theme.Switch(ThemeVariant.Light);

            Assert.Equal(Colors.DarkBlue, ColourOf(runs[0]));
            Assert.Null(ColourOf(runs[1]));
            Assert.Equal(Colors.Purple, ColourOf(runs[2]));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tokens_with_the_same_colour_pair_share_one_brush()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, text) = Write(new FakeDualHighlighter());

        var keywords = text.Inlines!.OfType<Run>().Where(r => r.Text == "kw").ToList();
        Assert.Equal(2, keywords.Count);
        Assert.Same(keywords[0].Foreground, keywords[1].Foreground);
    }

    [AvaloniaFact]
    public void Unsupported_language_writes_plain_lines()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, text) = Write(new FakeDualHighlighter(), "other");

        var runs = text.Inlines!.OfType<Run>().ToList();
        Assert.Equal(["first", "second"], runs.Select(r => r.Text));
        Assert.All(runs, r => Assert.False(r.IsSet(TextElement.ForegroundProperty)));
        Assert.Single(text.Inlines!.OfType<LineBreak>());
    }

    [AvaloniaFact]
    public void Theme_switch_recolours_tokens_coloured_in_both_variants()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (border, text) = Write(new FullyColouredDualHighlighter());
        var window = new Window { Content = border };
        try
        {
            window.Show();
            var run = text.Inlines!.OfType<Run>().First();
            Assert.Equal(Colors.DarkBlue, ColourOf(run));

            theme.Switch(ThemeVariant.Dark);

            Assert.Equal(Colors.LightBlue, ColourOf(run));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Theme_aware_highlighter_rebuilds_lines_for_the_new_variant()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (border, text) = Write(new FakeThemeAwareHighlighter());
        var window = new Window { Content = border };
        try
        {
            window.Show();
            Assert.Equal(Colors.DarkBlue, ColourOf(text.Inlines!.OfType<Run>().First()));

            theme.Switch(ThemeVariant.Dark);

            var runs = text.Inlines!.OfType<Run>().ToList();
            Assert.Equal(["kw", "first", "kw", "second"], runs.Select(r => r.Text));
            Assert.Equal(Colors.LightBlue, ColourOf(runs[0]));
            Assert.Null(ColourOf(runs[1]));
            Assert.Single(text.Inlines!.OfType<LineBreak>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plain_highlighter_colours_tokens()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, text) = Write(new FakeHighlighter());

        Assert.IsType<Run>(text.Inlines![0]);
        var runs = text.Inlines!.OfType<Run>().ToList();
        Assert.Equal(["kw", "first", "kw", "second"], runs.Select(r => r.Text));
        Assert.Equal(Colors.DarkBlue, ColourOf(runs[0]));
        Assert.Null(ColourOf(runs[1]));
        Assert.Single(text.Inlines!.OfType<LineBreak>());
    }

    [AvaloniaFact]
    public void No_highlighter_writes_plain_lines()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, text) = Write(null);

        Assert.IsType<Run>(text.Inlines![0]);
        var runs = text.Inlines!.OfType<Run>().ToList();
        Assert.Equal(["first", "second"], runs.Select(r => r.Text));
        Assert.All(runs, r => Assert.False(r.IsSet(TextElement.ForegroundProperty)));
        Assert.Single(text.Inlines!.OfType<LineBreak>());
    }
}
