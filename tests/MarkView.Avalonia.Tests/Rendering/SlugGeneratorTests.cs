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

    [Theory]
    // A suffixed slug can be another heading's own slug: suffixes are tried until one is unused.
    [InlineData(new[] { "A", "A", "A-1" }, new[] { "a", "a-1", "a-1-1" })]
    [InlineData(new[] { "A-1", "A", "A" }, new[] { "a-1", "a", "a-2" })]
    [InlineData(new[] { "A", "A-1", "A", "A" }, new[] { "a", "a-1", "a-2", "a-3" })]
    [InlineData(new[] { "A", "A", "A-1", "A-1" }, new[] { "a", "a-1", "a-1-1", "a-1-2" })]
    // The anchors github.com gives these headings.
    [InlineData(new[] { "A", "A", "A-1", "A-1", "A-2" }, new[] { "a", "a-1", "a-1-1", "a-1-2", "a-2" })]
    public void GenerateSlug_never_repeats_a_slug(string[] headings, string[] expected)
    {
        var gen = new SlugGenerator();

        Assert.Equal(expected, headings.Select(gen.GenerateSlug));
    }

    [Fact]
    public void TruncateTo_forgets_the_suffixes_tried_after_the_cut()
    {
        var gen = new SlugGenerator();
        gen.GenerateSlug("A-1");
        gen.GenerateSlug("A");
        gen.GenerateSlug("A"); // a-1 is taken: a-2
        gen.GenerateSlug("A-1"); // a-1-1

        gen.TruncateTo(2);

        Assert.Equal(["a-2", "a-1-1"], new[] { "A", "A-1" }.Select(gen.GenerateSlug));
        gen.TruncateTo(1);
        Assert.Equal(["a", "a-1-1"], new[] { "A", "A-1" }.Select(gen.GenerateSlug));
    }

    [Fact]
    public void Reset_clears_seen_slugs()
    {
        var gen = new SlugGenerator();
        gen.GenerateSlug("Hello");
        gen.Reset();
        Assert.Equal("hello", gen.GenerateSlug("Hello"));
    }

    [Fact]
    public void Reset_clears_count()
    {
        var gen = new SlugGenerator();
        gen.GenerateSlug("Hello");
        Assert.Equal(1, gen.Count);

        gen.Reset();

        Assert.Equal(0, gen.Count);
    }

    [Fact]
    public void TruncateTo_forgets_later_duplicates()
    {
        var gen = new SlugGenerator();
        gen.GenerateSlug("a");
        gen.GenerateSlug("a");
        gen.GenerateSlug("a"); // a, a-1, a-2

        gen.TruncateTo(1);

        Assert.Equal(1, gen.Count);
        Assert.Equal("a-1", gen.GenerateSlug("a"));
    }

    [Fact]
    public void TruncateTo_zero_forgets_everything()
    {
        var gen = new SlugGenerator();
        gen.GenerateSlug("a");

        gen.TruncateTo(0);

        Assert.Equal(0, gen.Count);
        Assert.Equal("a", gen.GenerateSlug("a"));
    }

    [Fact]
    public void TruncateTo_count_is_a_no_op()
    {
        var gen = new SlugGenerator();
        gen.GenerateSlug("a");
        gen.GenerateSlug("b");

        gen.TruncateTo(2);

        Assert.Equal(2, gen.Count);
        Assert.Equal("a-1", gen.GenerateSlug("a"));
        Assert.Equal(3, gen.Count);
    }

    [Fact]
    public void TruncateTo_keeps_the_slugs_before_the_cut_distinct()
    {
        var gen = new SlugGenerator();
        gen.GenerateSlug("a");
        gen.GenerateSlug("b");
        gen.GenerateSlug("a"); // a-1

        gen.TruncateTo(2);

        Assert.Equal("a-1", gen.GenerateSlug("a"));
        Assert.Equal("b-1", gen.GenerateSlug("b"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void TruncateTo_rejects_a_count_outside_the_generated_range(int count)
    {
        var gen = new SlugGenerator();
        gen.GenerateSlug("a");

        Assert.Throws<ArgumentOutOfRangeException>(() => gen.TruncateTo(count));
    }
}
