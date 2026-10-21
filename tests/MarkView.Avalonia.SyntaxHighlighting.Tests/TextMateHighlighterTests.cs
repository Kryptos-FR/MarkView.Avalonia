// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.ObjectModel;

using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

using MarkView.Avalonia.Tests.Shared;

using TextMateSharp.Grammars;
using Xunit;

namespace MarkView.Avalonia.SyntaxHighlighting.Tests;

public class TextMateHighlighterTests
{
    [Fact]
    public void Highlight_returns_null_for_unknown_language()
    {
        var highlighter = new TextMateHighlighter();
        var result = highlighter.Highlight("var x = 1;".AsMemory(), "notareallanguage");
        Assert.Null(result);
    }

    [Fact]
    public void Highlight_returns_tokens_for_csharp()
    {
        var highlighter = new TextMateHighlighter();
        var result = highlighter.Highlight("var x = 1;".AsMemory(), "csharp");
        Assert.NotNull(result);
        var tokens = result!.ToList();
        Assert.NotEmpty(tokens);
        // The concatenation of all token texts must equal the original line
        Assert.Equal("var x = 1;", string.Concat(tokens.Select(t => t.Text)));
    }

    [Fact]
    public void Highlight_returns_tokens_for_json()
    {
        var highlighter = new TextMateHighlighter();
        var result = highlighter.Highlight("{\"key\": 42}".AsMemory(), "json");
        Assert.NotNull(result);
        Assert.Equal("{\"key\": 42}", string.Concat(result!.Select(t => t.Text)));
    }

    [Fact]
    public void Highlight_accepts_null_language_and_returns_null()
    {
        var highlighter = new TextMateHighlighter();
        var result = highlighter.Highlight("anything".AsMemory(), null);
        Assert.Null(result);
    }

    [Fact]
    public void Highlight_with_DarkPlus_theme_produces_colored_tokens()
    {
        var highlighter = new TextMateHighlighter(ThemeName.DarkPlus);
        var result = highlighter.Highlight("var x = 1;".AsMemory(), "csharp");
        Assert.NotNull(result);
        // At least one token should have a non-null brush
        Assert.Contains(result!, t => t.Foreground != null);
    }

    [Fact]
    public void Highlight_tokens_cover_the_line_exactly_without_empty_tokens()
    {
        const string line = "var x = 1; // done";
        var tokens = new TextMateHighlighter(ThemeName.DarkPlus).Highlight(line.AsMemory(), "csharp")!;

        Assert.All(tokens, t => Assert.NotEmpty(t.Text));
        Assert.Equal(line, string.Concat(tokens.Select(t => t.Text)));
    }

    [Fact]
    public void Highlight_empty_line_returns_no_tokens()
    {
        var tokens = new TextMateHighlighter(ThemeName.DarkPlus).Highlight(ReadOnlyMemory<char>.Empty, "csharp");

        Assert.NotNull(tokens);
        Assert.Empty(tokens);
    }

    [Fact]
    public void Highlight_returns_the_same_read_only_tokens_for_a_line_seen_before()
    {
        var highlighter = new TextMateHighlighter();
        var first = highlighter.Highlight("var x = 1;".AsMemory(), "csharp");
        var again = highlighter.Highlight("xvar x = 1;".AsMemory()[1..], "csharp");

        Assert.Same(first, again);
        Assert.IsType<ReadOnlyCollection<(string Text, IBrush? Foreground)>>(first);
        Assert.NotSame(first, highlighter.Highlight("var x = 2;".AsMemory(), "csharp"));
        Assert.NotSame(first, highlighter.Highlight("var x = 1;".AsMemory(), "javascript"));
    }

    [Fact]
    public void HighlightBoth_returns_the_same_tokens_for_a_line_seen_before()
    {
        var highlighter = new DualThemeTextMateHighlighter(ThemeName.DarkPlus, ThemeName.LightPlus);
        var first = highlighter.HighlightBoth("var x = 1;".AsMemory(), "csharp");
        var again = highlighter.HighlightBoth("xvar x = 1;".AsMemory()[1..], "csharp");

        Assert.NotNull(first);
        Assert.Same(first, again);
        Assert.Equal("var x = 1;", string.Concat(first!.Select(t => t.Text)));
        Assert.NotSame(first, highlighter.HighlightBoth("var x = 2;".AsMemory(), "csharp"));
    }

    private static Color? ColourOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    [Fact]
    public void Highlight_carries_a_block_comment_on_to_the_next_line()
    {
        var highlighter = new TextMateHighlighter(ThemeName.DarkPlus);
        object? state = null;
        var opening = highlighter.Highlight("/* a comment".AsMemory(), "csharp", ref state)!;
        var inside = highlighter.Highlight("int x;".AsMemory(), "csharp", ref state)!;
        var closing = highlighter.Highlight("*/ int y;".AsMemory(), "csharp", ref state)!;
        var after = highlighter.Highlight("int z;".AsMemory(), "csharp", ref state)!;

        var comment = ColourOf(opening[^1].Foreground);
        Assert.NotNull(comment);
        Assert.Equal("int x;", Assert.Single(inside).Text);
        Assert.Equal(comment, ColourOf(inside[0].Foreground));
        Assert.Equal("*/", closing[0].Text);
        Assert.Equal(comment, ColourOf(closing[0].Foreground));
        Assert.Equal(ColourOf(highlighter.Highlight("int z;".AsMemory(), "csharp")![0].Foreground), ColourOf(after[0].Foreground));
        Assert.NotEqual(comment, ColourOf(after[0].Foreground));

        // Read again, the lines come from the cache, with the states they ended in.
        object? again = null;
        Assert.Same(opening, highlighter.Highlight("/* a comment".AsMemory(), "csharp", ref again));
        Assert.Same(inside, highlighter.Highlight("int x;".AsMemory(), "csharp", ref again));
        Assert.Same(closing, highlighter.Highlight("*/ int y;".AsMemory(), "csharp", ref again));
        Assert.Same(after, highlighter.Highlight("int z;".AsMemory(), "csharp", ref again));
    }

    [Fact]
    public void Highlight_without_a_state_reads_the_line_as_the_first_of_a_block()
    {
        var highlighter = new TextMateHighlighter(ThemeName.DarkPlus);
        object? state = null;
        var comment = ColourOf(highlighter.Highlight("/* a comment".AsMemory(), "csharp", ref state)![^1].Foreground);
        object? inComment = state;

        var alone = highlighter.Highlight("int x;".AsMemory(), "csharp")!;
        var inside = highlighter.Highlight("int x;".AsMemory(), "csharp", ref inComment)!;

        Assert.NotSame(alone, inside);
        Assert.NotEqual(comment, ColourOf(alone[0].Foreground));
        Assert.Equal(comment, ColourOf(inside[0].Foreground));
        Assert.Same(inside, highlighter.Highlight("int x;".AsMemory(), "csharp", ref state));
        Assert.Same(inComment, state);
    }

    [Fact]
    public void Highlight_leaves_the_state_alone_for_an_unknown_language()
    {
        var highlighter = new TextMateHighlighter(ThemeName.DarkPlus);
        object? state = "state";

        Assert.Null(highlighter.Highlight("/*".AsMemory(), "notareallanguage", ref state));
        Assert.Null(highlighter.Highlight("/*".AsMemory(), null, ref state));
        Assert.Equal("state", state);
    }

    [Fact]
    public void HighlightBoth_carries_a_block_comment_on_to_the_next_line()
    {
        var highlighter = new DualThemeTextMateHighlighter(ThemeName.DarkPlus, ThemeName.LightPlus);
        object? state = null;
        var opening = highlighter.HighlightBoth("/* a comment".AsMemory(), "csharp", ref state)!;
        var inside = highlighter.HighlightBoth("int x;".AsMemory(), "csharp", ref state)!;
        var alone = highlighter.HighlightBoth("int x;".AsMemory(), "csharp")!;

        var (_, dark, light) = Assert.Single(inside);
        Assert.Equal(ColourOf(opening[^1].Dark), ColourOf(dark));
        Assert.Equal(ColourOf(opening[^1].Light), ColourOf(light));
        Assert.NotEqual(ColourOf(dark), ColourOf(alone[0].Dark));
        object? again = null;
        Assert.Same(opening, highlighter.HighlightBoth("/* a comment".AsMemory(), "csharp", ref again));
        Assert.Same(inside, highlighter.HighlightBoth("int x;".AsMemory(), "csharp", ref again));
        Assert.Equal(state, again);
    }

    [Fact]
    public void HighlightVariant_carries_the_state_across_both_themes()
    {
        var highlighter = new DualThemeTextMateHighlighter(ThemeName.DarkPlus, ThemeName.LightPlus);
        object? state = null;
        var opening = highlighter.HighlightVariant("/* a comment".AsMemory(), "csharp", isDark: true, ref state)!;
        var inside = highlighter.HighlightVariant("int x;".AsMemory(), "csharp", isDark: false, ref state)!;
        var both = highlighter.HighlightBoth("/* a comment".AsMemory(), "csharp")!;

        Assert.Equal(ColourOf(both[^1].Dark), ColourOf(opening[^1].Foreground));
        Assert.Equal(ColourOf(both[^1].Light), ColourOf(Assert.Single(inside).Foreground));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dual_theme_Highlight_carries_the_state_on_in_the_current_theme(bool dark)
    {
        using var theme = new ThemeScope(dark ? ThemeVariant.Dark : ThemeVariant.Light);
        var highlighter = new DualThemeTextMateHighlighter(ThemeName.DarkPlus, ThemeName.LightPlus);
        object? state = null;
        highlighter.Highlight("/* a comment".AsMemory(), "csharp", ref state);
        var inside = highlighter.Highlight("int x;".AsMemory(), "csharp", ref state)!;

        var both = highlighter.HighlightBoth("/* a comment".AsMemory(), "csharp")!;
        Assert.Equal(ColourOf(dark ? both[^1].Dark : both[^1].Light), ColourOf(Assert.Single(inside).Foreground));
    }
}
