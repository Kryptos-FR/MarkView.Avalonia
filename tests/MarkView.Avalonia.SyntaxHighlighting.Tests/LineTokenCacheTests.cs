// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using TextMateSharp.Grammars;
using Xunit;

namespace MarkView.Avalonia.SyntaxHighlighting.Tests;

public class LineTokenCacheTests
{
    private static readonly TextMateGrammars Grammars = new(ThemeName.DarkPlus);
    private static readonly IGrammar CSharp = Grammars.Get("csharp")!;
    private static readonly IGrammar Json = Grammars.Get("json")!;
    private static readonly IStateStack End = CSharp.TokenizeLine("int x;", null, TimeSpan.MaxValue).RuleStack;
    private static readonly IStateStack InComment = CSharp.TokenizeLine("/* a", null, TimeSpan.MaxValue).RuleStack;

    [Fact]
    public void A_line_is_found_once_added()
    {
        var cache = new LineTokenCache<string>();
        Assert.False(cache.TryGet(CSharp, null, "var x = 1;", out _, out _));

        cache.Add(CSharp, null, "var x = 1;", "tokens", End);

        Assert.True(cache.TryGet(CSharp, null, "var x = 1;".AsSpan(), out var tokens, out _));
        Assert.Equal("tokens", tokens);
        Assert.False(cache.TryGet(CSharp, null, "var x = 2;", out _, out _));
        Assert.Equal(10, cache.Length);
    }

    [Fact]
    public void Lines_are_kept_per_grammar()
    {
        var cache = new LineTokenCache<string>();
        cache.Add(CSharp, null, "1", "csharp", End);

        Assert.False(cache.TryGet(Json, null, "1", out _, out _));

        cache.Add(Json, null, "1", "json", End);

        Assert.True(cache.TryGet(CSharp, null, "1", out var csharp, out _));
        Assert.Equal("csharp", csharp);
        Assert.True(cache.TryGet(Json, null, "1", out var json, out _));
        Assert.Equal("json", json);
        Assert.Equal(2, cache.Length);
    }

    [Fact]
    public void Adding_a_line_again_keeps_the_first_tokens()
    {
        var cache = new LineTokenCache<string>();
        cache.Add(CSharp, null, "line", "first", End);
        cache.Add(CSharp, null, "line", "second", End);

        Assert.True(cache.TryGet(CSharp, null, "line", out var tokens, out _));
        Assert.Equal("first", tokens);
        Assert.Equal(4, cache.Length);
    }

    [Fact]
    public void A_line_longer_than_the_limit_is_not_kept()
    {
        var cache = new LineTokenCache<string>();
        var longest = new string('a', LineTokenCache<string>.MaxLineLength);
        var tooLong = longest + "a";

        cache.Add(CSharp, null, tooLong, "tokens", End);
        cache.Add(CSharp, null, longest, "tokens", End);

        Assert.False(cache.TryGet(CSharp, null, tooLong, out _, out _));
        Assert.True(cache.TryGet(CSharp, null, longest, out _, out _));
        Assert.Equal(LineTokenCache<string>.MaxLineLength, cache.Length);
    }

    [Fact]
    public void The_cache_starts_again_when_a_line_does_not_fit_in_the_budget()
    {
        var cache = new LineTokenCache<string>();
        var length = LineTokenCache<string>.MaxLineLength;
        var count = LineTokenCache<string>.Budget / length;
        for (var i = 0; i < count; i++)
            cache.Add(i % 2 == 0 ? CSharp : Json, null, i.ToString("D4") + new string('a', length - 4), "tokens", End);
        Assert.Equal(LineTokenCache<string>.Budget, cache.Length);
        Assert.True(cache.TryGet(CSharp, null, "0000" + new string('a', length - 4), out _, out _));
        Assert.True(cache.TryGet(Json, null, "0001" + new string('a', length - 4), out _, out _));

        cache.Add(CSharp, null, "x", "tokens", End);

        Assert.Equal(1, cache.Length);
        Assert.True(cache.TryGet(CSharp, null, "x", out _, out _));
        Assert.False(cache.TryGet(CSharp, null, "0000" + new string('a', length - 4), out _, out _));
        Assert.False(cache.TryGet(Json, null, "0001" + new string('a', length - 4), out _, out _));
    }

    [Fact]
    public void A_line_hands_back_the_state_it_ended_in()
    {
        var cache = new LineTokenCache<string>();
        cache.Add(CSharp, null, "/* a", "tokens", InComment);

        Assert.True(cache.TryGet(CSharp, null, "/* a", out _, out var end));
        Assert.Same(InComment, end);
    }

    [Fact]
    public void Lines_are_kept_per_start_state()
    {
        var cache = new LineTokenCache<string>();
        cache.Add(CSharp, null, "a */", "first line", End);

        Assert.False(cache.TryGet(CSharp, InComment, "a */", out _, out _));

        cache.Add(CSharp, InComment, "a */", "in a comment", End);

        Assert.True(cache.TryGet(CSharp, null, "a */", out var first, out _));
        Assert.Equal("first line", first);
        Assert.True(cache.TryGet(CSharp, InComment, "a */", out var inComment, out _));
        Assert.Equal("in a comment", inComment);
        // States compare by value: another comment's state finds the same line.
        var otherComment = CSharp.TokenizeLine("/* b", null, TimeSpan.MaxValue).RuleStack;
        Assert.True(cache.TryGet(CSharp, otherComment, "a */", out var again, out _));
        Assert.Equal("in a comment", again);
        Assert.Equal(8, cache.Length);
    }
}
