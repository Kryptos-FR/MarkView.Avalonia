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

    [Fact]
    public void A_line_is_found_once_added()
    {
        var cache = new LineTokenCache<string>();
        Assert.False(cache.TryGet(CSharp, "var x = 1;", out _));

        cache.Add(CSharp, "var x = 1;", "tokens");

        Assert.True(cache.TryGet(CSharp, "var x = 1;".AsSpan(), out var tokens));
        Assert.Equal("tokens", tokens);
        Assert.False(cache.TryGet(CSharp, "var x = 2;", out _));
        Assert.Equal(10, cache.Length);
    }

    [Fact]
    public void Lines_are_kept_per_grammar()
    {
        var cache = new LineTokenCache<string>();
        cache.Add(CSharp, "1", "csharp");

        Assert.False(cache.TryGet(Json, "1", out _));

        cache.Add(Json, "1", "json");

        Assert.True(cache.TryGet(CSharp, "1", out var csharp));
        Assert.Equal("csharp", csharp);
        Assert.True(cache.TryGet(Json, "1", out var json));
        Assert.Equal("json", json);
        Assert.Equal(2, cache.Length);
    }

    [Fact]
    public void Adding_a_line_again_keeps_the_first_tokens()
    {
        var cache = new LineTokenCache<string>();
        cache.Add(CSharp, "line", "first");
        cache.Add(CSharp, "line", "second");

        Assert.True(cache.TryGet(CSharp, "line", out var tokens));
        Assert.Equal("first", tokens);
        Assert.Equal(4, cache.Length);
    }

    [Fact]
    public void A_line_longer_than_the_limit_is_not_kept()
    {
        var cache = new LineTokenCache<string>();
        var longest = new string('a', LineTokenCache<string>.MaxLineLength);
        var tooLong = longest + "a";

        cache.Add(CSharp, tooLong, "tokens");
        cache.Add(CSharp, longest, "tokens");

        Assert.False(cache.TryGet(CSharp, tooLong, out _));
        Assert.True(cache.TryGet(CSharp, longest, out _));
        Assert.Equal(LineTokenCache<string>.MaxLineLength, cache.Length);
    }

    [Fact]
    public void The_cache_starts_again_when_a_line_does_not_fit_in_the_budget()
    {
        var cache = new LineTokenCache<string>();
        var length = LineTokenCache<string>.MaxLineLength;
        var count = LineTokenCache<string>.Budget / length;
        for (var i = 0; i < count; i++)
            cache.Add(i % 2 == 0 ? CSharp : Json, i.ToString("D4") + new string('a', length - 4), "tokens");
        Assert.Equal(LineTokenCache<string>.Budget, cache.Length);
        Assert.True(cache.TryGet(CSharp, "0000" + new string('a', length - 4), out _));
        Assert.True(cache.TryGet(Json, "0001" + new string('a', length - 4), out _));

        cache.Add(CSharp, "x", "tokens");

        Assert.Equal(1, cache.Length);
        Assert.True(cache.TryGet(CSharp, "x", out _));
        Assert.False(cache.TryGet(CSharp, "0000" + new string('a', length - 4), out _));
        Assert.False(cache.TryGet(Json, "0001" + new string('a', length - 4), out _));
    }
}
