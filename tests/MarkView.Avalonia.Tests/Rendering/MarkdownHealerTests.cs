// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// Tests for <see cref="MarkdownHealer"/>, which closes the inline syntax left open at the end of a
/// streamed text. These tests construct no Avalonia object.
/// </summary>
public class MarkdownHealerTests
{
    private static readonly HealingOptions None = new(false, false);

    private static string Heal(string text, int tailStart = 0) => MarkdownHealer.Heal(text, tailStart, None);

    private static MarkdownDocument Parse(string text) =>
        Markdown.Parse(text, new MarkdownPipelineBuilder().UseGitHubExtensions().Build());

    [Theory]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("`code`")]
    [InlineData("``a ` b``")]
    [InlineData("\\`not code")]
    // A longer run inside the span is content, not the closer or the start of one.
    [InlineData("`a `` b` c")]
    public void Complete_text_is_returned_as_the_same_instance(string text) =>
        Assert.Same(text, Heal(text));

    [Theory]
    [InlineData("a `b", "a `b`")]
    [InlineData("a ``b` c", "a ``b` c``")]
    [InlineData("a `b` `c", "a `b` `c`")]
    public void Unmatched_backtick_run_is_closed_with_the_same_length(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Theory]
    [InlineData("abc `")]
    [InlineData("abc `` ")]
    [InlineData("``")]
    public void Backtick_run_at_the_end_is_left_alone(string text) =>
        Assert.Same(text, Heal(text));

    [Theory]
    [InlineData("a `b\n", "a `b`\n")]
    [InlineData("a `b  \r\n", "a `b`  \r\n")]
    [InlineData("a `b\t", "a `b`\t")]
    public void Closers_go_before_trailing_whitespace(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Theory]
    [InlineData("a ``b`")]
    [InlineData("a `b ``")]
    [InlineData("a `b`` ")]
    public void Backtick_run_is_left_alone_when_the_content_ends_with_a_backtick(string text) =>
        // A closer written there would merge with the last backtick into a longer run that closes nothing.
        Assert.Same(text, Heal(text));

    [Fact]
    public void Negative_tail_start_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkdownHealer.Heal("a", -1, None));

    [Fact]
    public void Escaped_backtick_does_not_open_a_code_span() =>
        Assert.Same("a \\`b", Heal("a \\`b"));

    [Fact]
    public void Nothing_before_tail_start_is_scanned()
    {
        // The first paragraph's lone backtick is literal for good; only the tail is healed.
        var text = "a `b\n\nc `d";
        Assert.Equal("a `b\n\nc `d`", Heal(text, tailStart: 6));
    }

    [Fact]
    public void Tail_start_at_the_end_heals_nothing() =>
        Assert.Same("a `b", Heal("a `b", tailStart: 4));

    [Theory]
    [InlineData("[t](u", "[t](u)")]
    [InlineData("![a](u", "![a](u)")]
    [InlineData("[t](", "[t]()")]
    [InlineData("[t](u \"ti", "[t](u \"ti\")")]
    [InlineData("[t](u 'ti", "[t](u 'ti')")]
    [InlineData("[t](u(v", "[t](u(v))")]
    [InlineData("see [t](u", "see [t](u)")]
    public void Unclosed_link_address_is_closed(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Theory]
    [InlineData("[t")]
    [InlineData("![a")]
    [InlineData("- [ ")]
    [InlineData("[1")]
    [InlineData("[t]")]
    [InlineData("[t][r")]
    [InlineData("a ] b")]
    public void Bare_bracket_stays_literal(string text) =>
        Assert.Same(text, Heal(text));

    [Theory]
    [InlineData("a](u")]
    [InlineData("\\[t](u")]
    // The first ] closes the link text without an address, so the second one has no [ to pair with.
    [InlineData("[t] x](u")]
    // Likewise once the first link is complete.
    [InlineData("[a](u) b](v")]
    public void Closing_bracket_without_an_open_one_starts_no_address(string text) =>
        Assert.Same(text, Heal(text));

    [Theory]
    [InlineData("[t](u) `c", "[t](u) `c`")]
    [InlineData("[t](u \"ti)\") `c", "[t](u \"ti)\") `c`")]
    public void Scan_continues_after_a_closed_address(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Fact]
    public void Link_inside_brackets_is_closed() =>
        Assert.Equal("[a [b](u)", Heal("[a [b](u"));

    [Fact]
    public void Scan_resumes_just_after_the_closing_parenthesis() =>
        // The backtick before ) is address content: it does not pair with the one after.
        Assert.Equal("[t](u`) `c`", Heal("[t](u`) `c"));

    [Fact]
    public void Nothing_inside_an_address_is_healed() =>
        Assert.Equal("[t](u`v)", Heal("[t](u`v"));

    [Fact]
    public void Quote_inside_the_destination_does_not_open_a_title() =>
        Assert.Equal("[t](u\"v)", Heal("[t](u\"v"));

    [Theory]
    [InlineData("[t](u \"ti)", "[t](u \"ti)\")")]
    [InlineData("[t](u \"(", "[t](u \"(\")")]
    public void Parentheses_inside_a_title_are_plain(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Theory]
    [InlineData("[t](a(b)c", "[t](a(b)c)")]
    [InlineData("[t](u\\)", "[t](u\\))")]
    [InlineData("[t](u \"a\\\"b", "[t](u \"a\\\"b\")")]
    public void Address_tracks_nesting_and_escapes(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Fact]
    public void Backtick_inside_a_link_text_is_still_healed() =>
        Assert.Equal("[`t`", Heal("[`t"));

    [Fact]
    public void Unmatched_backtick_inside_a_link_text_swallows_the_address() =>
        // The code span rule stops the scan: what follows the backtick is code content, as CommonMark reads it.
        Assert.Equal("[a `b](u`", Heal("[a `b](u"));

    [Theory]
    [InlineData("<b>x `c", "<b>x `c`")]
    [InlineData("<https://x.y/`a> `c", "<https://x.y/`a> `c`")]
    public void Complete_tag_or_autolink_is_opaque(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Theory]
    [InlineData("<b class=\"`x")]
    [InlineData("<https://x.y/`a")]
    public void Half_tag_ends_the_scan(string text) =>
        Assert.Same(text, Heal(text));

    [Theory]
    // The backtick inside the autolink does not pair with the one after it.
    [InlineData("<https://x.y/`> `c", "<https://x.y/`> `c`")]
    [InlineData("<b>`c", "<b>`c`")]
    public void Tag_is_opaque_up_to_its_greater_than(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Theory]
    [InlineData("</b `x")]
    [InlineData("<!-- `x")]
    [InlineData("<?php `x")]
    [InlineData("a <b `x")]
    public void Half_tag_of_any_kind_ends_the_scan(string text) =>
        Assert.Same(text, Heal(text));

    [Fact]
    public void Half_tag_still_gets_the_earlier_closers() =>
        Assert.Equal("`a <b`", Heal("`a <b"));

    [Fact]
    public void Less_than_before_a_space_is_literal() =>
        Assert.Equal("1 < 2 `c`", Heal("1 < 2 `c"));

    [Fact]
    public void Less_than_at_the_end_is_literal() =>
        Assert.Same("x <", Heal("x <"));

    [Fact]
    public void TailBlock_is_the_paragraph_at_the_end()
    {
        var text = "a\n\nb";
        var tail = Assert.IsType<ParagraphBlock>(MarkdownHealer.TailBlock(Parse(text), text));
        Assert.Equal(3, tail.Span.Start);
    }

    [Fact]
    public void TailBlock_allows_one_trailing_line_break()
    {
        var text = "a\n\nb\n";
        Assert.Equal(3, MarkdownHealer.TailBlock(Parse(text), text)!.Span.Start);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a\n\n")]
    [InlineData("a\r\n\r\n")]
    public void TailBlock_is_null_after_a_blank_line_or_for_an_empty_text(string text) =>
        Assert.Null(MarkdownHealer.TailBlock(Parse(text), text));

    [Fact]
    public void TailBlock_is_an_atx_heading_at_the_end()
    {
        var text = "para\n\n# **t";
        Assert.IsType<HeadingBlock>(MarkdownHealer.TailBlock(Parse(text), text));
    }

    [Fact]
    public void TailBlock_descends_into_the_last_list_item()
    {
        var text = "- a\n- **b";
        var tail = Assert.IsType<ParagraphBlock>(MarkdownHealer.TailBlock(Parse(text), text));
        Assert.Equal(6, tail.Span.Start);
    }

    [Fact]
    public void TailBlock_descends_into_the_last_table_cell()
    {
        var text = "| h |\n|---|\n| *b";
        Assert.IsType<ParagraphBlock>(MarkdownHealer.TailBlock(Parse(text), text));
    }

    [Fact]
    public void TailBlock_is_the_open_fence()
    {
        var text = "a\n\n```\ncode";
        Assert.IsType<FencedCodeBlock>(MarkdownHealer.TailBlock(Parse(text), text));
    }

    [Fact]
    public void TailBlock_is_the_open_fence_after_a_blank_line_inside_it()
    {
        // A blank line does not end a fence: the next text is still code.
        var text = "```\ncode\n\n";
        Assert.IsType<FencedCodeBlock>(MarkdownHealer.TailBlock(Parse(text), text));
    }

    [Fact]
    public void TailBlock_is_the_closed_fence()
    {
        var text = "a\n\n```\ncode\n```";
        Assert.IsType<FencedCodeBlock>(MarkdownHealer.TailBlock(Parse(text), text));
    }

    [Fact]
    public void TailBlock_is_the_block_with_the_greatest_span_end_not_the_last_index()
    {
        // With footnotes enabled the footnote group is moved to the end of the document, after the paragraph.
        var text = "[^1]: note\n\nlast *x";
        var document = Markdown.Parse(text, new MarkdownPipelineBuilder().UseFootnotes().Build());
        Assert.IsType<FootnoteGroup>(document[^1]);
        var tail = Assert.IsType<ParagraphBlock>(MarkdownHealer.TailBlock(document, text));
        Assert.Equal(12, tail.Span.Start);
    }

    [Fact]
    public void TailBlock_is_an_empty_list_item()
    {
        var text = "- a\n- ";
        Assert.IsType<ListItemBlock>(MarkdownHealer.TailBlock(Parse(text), text));
    }
}
