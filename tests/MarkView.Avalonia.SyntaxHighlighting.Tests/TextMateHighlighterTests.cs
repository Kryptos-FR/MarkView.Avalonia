// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

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
}
