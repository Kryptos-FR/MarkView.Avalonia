// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("My Heading #1", "my-heading-1")]
    [InlineData("C# and .NET", "c-and-net")]
    [InlineData("my_function", "my_function")]
    // GitHub does not collapse consecutive separators: dropping a punctuation char
    // adjacent to a space leaves the space (and its hyphen) behind.
    [InlineData("C# Tips & Tricks", "c-tips--tricks")]
    [InlineData("multiple   spaces", "multiple---spaces")]
    public void GenerateSlug_produces_github_style_slug(string input, string expected)
    {
        var gen = new SlugGenerator();
        Assert.Equal(expected, gen.GenerateSlug(input));
    }

    [Fact]
    public void GenerateSlug_falls_back_to_section_for_symbol_only_heading()
    {
        var gen = new SlugGenerator();
        Assert.Equal("section", gen.GenerateSlug("!!!"));
    }

    [Fact]
    public void GenerateSlug_deduplicates_same_heading()
    {
        var gen = new SlugGenerator();
        Assert.Equal("hello", gen.GenerateSlug("Hello"));
        Assert.Equal("hello-1", gen.GenerateSlug("Hello"));
        Assert.Equal("hello-2", gen.GenerateSlug("Hello"));
    }

    [Fact]
    public void Reset_clears_seen_slugs()
    {
        var gen = new SlugGenerator();
        gen.GenerateSlug("Hello");
        gen.Reset();
        Assert.Equal("hello", gen.GenerateSlug("Hello"));
    }
}
