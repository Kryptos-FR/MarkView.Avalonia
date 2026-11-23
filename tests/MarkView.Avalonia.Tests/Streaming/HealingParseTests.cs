// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;

using Markdig;
using Markdig.Syntax;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Streaming;

/// <summary>
/// A streaming update heals from where the last update's tail started and parses once (see
/// <see cref="MarkdownHealer.ParseStreamed"/>), with the same result as parsing, healing from the tail of that parse
/// and parsing again.
/// </summary>
public class HealingParseTests
{
    private static readonly MarkdownPipeline Pipeline = StreamingEquivalenceTests.Pipeline;
    private static readonly HealingOptions Options = MarkdownHealer.OptionsFor(Pipeline);

    private static MarkdownObject Root(Block block)
    {
        MarkdownObject root = block;
        while (root is Block { Parent: { } parent })
            root = parent;
        return root;
    }

    public static TheoryData<string> Assets() =>
        new() { "chat.md", "healing.md", "references.md", "abbreviations.md", "thinking.md" };

    [AvaloniaTheory]
    [MemberData(nameof(Assets))]
    public void Every_prefix_parses_as_with_two_parses(string asset)
    {
        var text = StreamingEquivalenceTests.Load(asset, crlf: false);
        AssertEveryPrefix(text);
    }

    [Theory]
    [InlineData("Some **bold and *italic* and `code` and [a link](https://example.com/path) and ~~struck~~ text.\n\nNext **paragraph with `code\n\n- a list item with **bold\n- and _emphasis\n\n> a quote with `code\n> and more **bold\n\n# A heading with **bold\n\nSetext **bold\n---\n\n***\n\nend")]
    [InlineData("a **b\n-\n\nc *d\n***\n\ne `f\n===\n\n1. **g\n2. h\n\n**i\n\n  **j")]
    [InlineData("x\n\n```cs `y\ncode\n```\n\na **b\n<pre `c\n\n<a title=\"`d\">\n\n**e\n<div f\n\n_g\n[h]: <i\n\n1. **j\n2")]
    public void Every_prefix_of_a_text_full_of_open_syntax_parses_as_with_two_parses(string text) =>
        AssertEveryPrefix(text);

    [AvaloniaTheory]
    [InlineData("chat.md", 1)]
    [InlineData("healing.md", 2)]
    [InlineData("healing.md", 3)]
    [InlineData("references.md", 4)]
    [InlineData("thinking.md", 5)]
    public void Every_update_of_a_stream_in_random_chunks_parses_as_with_two_parses(string asset, int seed)
    {
        var text = StreamingEquivalenceTests.Load(asset, crlf: seed % 2 == 0);
        var random = new Random(seed);
        AssertEveryPrefix(text, () => random.Next(1, 12));
    }

    private static void AssertEveryPrefix(string text, Func<int>? step = null)
    {
        TailHint? hint = null;
        for (var length = 1; length <= text.Length; length = length == text.Length ? length + 1 : Math.Min(text.Length, length + (step?.Invoke() ?? 1)))
        {
            var prefix = text[..length];
            var expected = MarkdownHealer.ParseStreamed(prefix, Pipeline, Options, final: false, hint: null);
            var actual = MarkdownHealer.ParseStreamed(prefix, Pipeline, Options, final: false, hint);

            Assert.True(expected.ParsedText == actual.ParsedText, $"healed text differs at {length} (hint {hint}): EXPECTED[{expected.ParsedText[System.Math.Max(0, length - 60)..].Replace("\n", "\\n")}] ACTUAL[{actual.ParsedText[System.Math.Max(0, length - 60)..].Replace("\n", "\\n")}] tails {expected.Tail?.GetType().Name}@{expected.Tail?.Span.Start} / {actual.Tail?.GetType().Name}@{actual.Tail?.Span.Start}");
            Assert.Equal(expected.Tail?.GetType(), actual.Tail?.GetType());
            Assert.Equal(expected.Tail?.Span.Start, actual.Tail?.Span.Start);
            hint = MarkdownHealer.Heals(actual.Tail) ? new TailHint(actual.Tail.Span.Start, prefix.Length) : null;
        }
    }

    [Fact]
    public void Text_healed_from_the_hint_is_parsed_once()
    {
        var (document, parsed, tail) = MarkdownHealer.ParseStreamed("a\n\nSome **bold", Pipeline, Options, final: false, new TailHint(3, "a\n\nSome **bo".Length));

        Assert.Equal("a\n\nSome **bold**", parsed);
        // The tail comes from the document returned, the healed one: no parse of the text as it is was made.
        Assert.Same(document, Root(Assert.IsType<ParagraphBlock>(tail)));
    }

    [Fact]
    public void Tail_on_more_than_one_line_falls_back_to_two_parses()
    {
        // Closers appended to "2." would make it lazy text of the paragraph instead of a list item.
        var (_, parsed, tail) = MarkdownHealer.ParseStreamed("1. **a\n2.", Pipeline, Options, final: false, new TailHint(3, "1. **a\n2".Length));

        Assert.Equal("1. **a\n2.", parsed);
        Assert.IsType<ListItemBlock>(tail);
    }

    [Fact]
    public void Tail_starting_with_a_backtick_falls_back_to_two_parses()
    {
        // "```cs" opens a fence, which closers appended to it ("```cs```") would turn back into inline code.
        var (document, parsed, _) = MarkdownHealer.ParseStreamed("```cs", Pipeline, Options, final: false, new TailHint(0, "``".Length));

        Assert.Equal("```cs", parsed);
        Assert.IsType<FencedCodeBlock>(Assert.Single(document));
    }

    [Fact]
    public void New_line_since_the_last_update_falls_back_to_two_parses()
    {
        // The last update's tail started at 0; the text has started another paragraph since.
        var (document, parsed, tail) = MarkdownHealer.ParseStreamed("Some **bold\n\nmore `code", Pipeline, Options, final: false, new TailHint(0, "Some **bold".Length));

        Assert.Equal("Some **bold\n\nmore `code`", parsed);
        Assert.Equal(13, tail!.Span.Start);
        // That tail comes from the parse of the text as it is, before healing.
        Assert.NotSame(document, Root(tail));
    }

    [Fact]
    public void Hint_whose_tail_moved_falls_back_to_two_parses()
    {
        // "1" was a paragraph; "1. " made it a list item, whose own paragraph is the tail.
        var (document, parsed, tail) = MarkdownHealer.ParseStreamed("1. **c", Pipeline, Options, final: false, new TailHint(0, "1".Length));

        Assert.Equal("1. **c**", parsed);
        Assert.Equal(3, tail!.Span.Start);
        Assert.NotSame(document, Root(tail));
    }

    [Fact]
    public void Final_update_ignores_the_hint()
    {
        var (_, parsed, tail) = MarkdownHealer.ParseStreamed("Some **bold", Pipeline, Options, final: true, new TailHint(0, "Some **bo".Length));

        Assert.Equal("Some **bold", parsed);
        Assert.Null(tail);
    }
}
