// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using System.Reflection.Emit;

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

    [Theory]
    [InlineData("**b", "**b**")]
    [InlineData("*i", "*i*")]
    [InlineData("***bi", "***bi***")]
    [InlineData("__b", "__b__")]
    [InlineData("_i", "_i_")]
    [InlineData("~~s", "~~s~~")]
    [InlineData("**b *i", "**b *i***")]
    [InlineData("*i **b", "*i **b***")]
    [InlineData("**b `c", "**b `c`**")]
    [InlineData("**b [t](u", "**b [t](u)**")]
    [InlineData("a **b** *c", "a **b** *c*")]
    [InlineData("**b\n", "**b**\n")]
    [InlineData("**a\n*b", "**a\n*b***")]
    [InlineData("> **a\n> *b", "> **a\n> *b***")]
    [InlineData("**a > *b", "**a > *b***")]
    [InlineData("**a\n- *b", "**a\n- *b***")]
    [InlineData("**a\n*>", "**a\n*>***")]
    public void Unmatched_emphasis_is_closed_in_reverse_order_of_opening(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Theory]
    [InlineData("snake_case")]
    [InlineData("2 * 3")]
    [InlineData("a * b * c")]
    [InlineData("- item")]
    [InlineData("* item")]
    [InlineData("\\*not")]
    [InlineData("~one")]
    [InlineData("~~~x")]
    [InlineData("^sup")]
    [InlineData("++ins")]
    [InlineData("==mark")]
    [InlineData("a_b_c")]
    public void Runs_that_cannot_open_are_literal(string text) =>
        Assert.Same(text, Heal(text));

    [Theory]
    [InlineData("abc **")]
    [InlineData("abc *")]
    [InlineData("**")]
    [InlineData("abc ** ")]
    [InlineData("**a\n*")]
    [InlineData("__a\n_")]
    [InlineData("~~a\n~~")]
    [InlineData("**a\n  ** ")]
    [InlineData("- **a\n  *")]
    [InlineData("> **a\n> *")]
    [InlineData("**a\n- *")]
    [InlineData("**a\n1. *")]
    [InlineData("> - **a\n>   ~~")]
    public void Delimiter_run_at_the_end_is_left_alone(string text) =>
        Assert.Same(text, Heal(text));

    [Theory]
    [InlineData("**a*", "**a**")]
    [InlineData("*a**", "*a**")]
    [InlineData("**a** *b", "**a** *b*")]
    // The closing * takes one * from the **: the two single * entries left open are closed.
    [InlineData("*a **b*", "*a **b***")]
    public void Closers_consume_openers_partially(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Fact]
    public void Emphasis_opened_inside_a_link_text_is_dropped_when_the_link_closes() =>
        Assert.Equal("[**a](u)", Heal("[**a](u"));

    [Fact]
    public void Emphasis_opened_inside_a_bare_bracket_is_still_closed() =>
        Assert.Equal("[**a**", Heal("[**a"));

    [Fact]
    public void Intraword_underscore_does_not_close() =>
        Assert.Equal("_a_b_", Heal("_a_b"));

    [Fact]
    public void Asterisk_after_punctuation_opens() =>
        Assert.Equal("(*a*", Heal("(*a"));

    [Fact]
    public void Left_neighbour_before_tail_start_is_consulted() =>
        // tailStart points at `*` whose left neighbour `x` makes it right-flanking; followed by `y` it is also
        // left-flanking, and `*` can then open: healed. (With `_` it could not: intraword.)
        Assert.Equal("x*y*", Heal("x*y", tailStart: 1));

    [Fact]
    public void Underscore_with_a_word_before_tail_start_is_intraword() =>
        Assert.Same("x_y", Heal("x_y", tailStart: 1));

    [Fact]
    public void Closer_drops_the_openers_it_passes() =>
        // The closing _ passes the * on its way down to the _: the * becomes literal and is not closed.
        Assert.Same("_a *b_", Heal("_a *b_"));

    [Fact]
    public void Partial_close_still_drops_the_openers_it_passes() =>
        // The closing * passes the _ and takes one * from the **: the _ is literal, one * is left open.
        Assert.Equal("**a _b**", Heal("**a _b*"));

    [Theory]
    // The * finds no * to close, so the _ below it stays open.
    [InlineData("_a b*", "_a b*_")]
    // The * between a and b closes the first one and has none left to open with, so the last * finds none.
    [InlineData("*a*b _c*", "*a*b _c*_")]
    public void Closer_without_a_matching_opener_drops_nothing(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Fact]
    public void Closer_count_left_over_keeps_looking_down() =>
        // The ** closes the * and then takes one * from the ** below it.
        Assert.Equal("**a *b***", Heal("**a *b**"));

    [Fact]
    public void Closer_count_left_over_opens_when_it_can() =>
        // The ** after a closes the * with one of its two and opens with the other.
        Assert.Equal("*a**b*", Heal("*a**b"));

    [Fact]
    public void Strikethrough_closes_like_emphasis() =>
        Assert.Equal("~~a~~ ~~b~~", Heal("~~a~~ ~~b"));

    [Fact]
    public void Link_closing_drops_only_the_entries_of_its_own_bracket() =>
        // The inner bracket's address closes; the ** opened inside the outer bracket is still open.
        Assert.Equal("[**a [b](u)**", Heal("[**a [b](u"));

    [Theory]
    // Next is punctuation, previous is whitespace: left-flanking.
    [InlineData("a *(b", "a *(b*")]
    // Between two punctuation characters _ is both left- and right-flanking, and opens since punctuation precedes it.
    [InlineData("(_(a", "(_(a_")]
    // A no-break space is Unicode whitespace: the _ is left-flanking only.
    [InlineData("a\u00A0_b", "a\u00A0_b_")]
    // Symbols count as punctuation: as with "(_(a", the _ opens.
    [InlineData("+_+a", "+_+a_")]
    public void Flanking_follows_commonmark(string text, string expected) =>
        Assert.Equal(expected, Heal(text));

    [Theory]
    // Previous is punctuation, next is whitespace: right-flanking, so the * closes.
    [InlineData("*(a)* b")]
    // Between two punctuation characters the * is right-flanking and closes.
    [InlineData("*(a)*.")]
    // Between two punctuation characters the _ closes since punctuation follows it.
    [InlineData("_a._.")]
    public void Closer_after_punctuation_closes(string text) =>
        Assert.Same(text, Heal(text));

    [Theory]
    [InlineData("**a\\")]
    [InlineData("[t](u\\")]
    [InlineData("\\")]
    public void Trailing_backslash_heals_nothing(string text) =>
        // Any closer written after it would be escaped.
        Assert.Same(text, Heal(text));

    [Fact]
    public void Escaped_backslash_at_the_end_still_heals() =>
        Assert.Equal("**a\\\\**", Heal("**a\\\\"));

    private static readonly HealingOptions DollarMath = new(true, false);
    private static readonly HealingOptions Bracket = new(true, true);

    [Theory]
    [InlineData("$a *b$ *c", "$a *b$ *c*")]
    [InlineData("$$a *b$$ *c", "$$a *b$$ *c*")]
    public void Complete_dollar_math_is_opaque(string text, string expected) =>
        Assert.Equal(expected, MarkdownHealer.Heal(text, 0, DollarMath));

    [Theory]
    [InlineData("$5 and *c", "$5 and *c*")]
    [InlineData("$a *b", "$a *b*")]
    // A run closes only a run of the same length.
    [InlineData("$a$$ *c", "$a$$ *c*")]
    [InlineData("$$a$ *c", "$$a$ *c*")]
    public void Unclosed_dollar_is_literal_and_never_healed(string text, string expected) =>
        Assert.Equal(expected, MarkdownHealer.Heal(text, 0, DollarMath));

    [Fact]
    public void Dollar_run_of_three_is_literal() =>
        Assert.Equal("$$$a *b$$$ c*", MarkdownHealer.Heal("$$$a *b$$$ c", 0, DollarMath));

    [Fact]
    public void Dollar_math_is_not_opaque_without_the_option() =>
        Assert.Equal("$a *b$ c*", MarkdownHealer.Heal("$a *b$ c", 0, None));

    [Theory]
    [InlineData("\\(")]
    [InlineData("a \\[")]
    [InlineData("a \\( ")]
    public void Bracket_math_opener_at_the_end_is_left_alone(string text) =>
        Assert.Same(text, MarkdownHealer.Heal(text, 0, Bracket));

    [Theory]
    [InlineData("\\(x^2", "\\(x^2\\)")]
    [InlineData("\\[x *y", "\\[x *y\\]")]
    [InlineData("**b \\(x", "**b \\(x\\)**")]
    [InlineData("\\(a\\) *c", "\\(a\\) *c*")]
    [InlineData("\\[a\\] *c", "\\[a\\] *c*")]
    // The formula is opaque to the other rules.
    [InlineData("\\(x `y *z", "\\(x `y *z\\)")]
    // An escaped backslash and a parenthesis are not a closer: the formula is still open.
    [InlineData("\\(x \\\\)", "\\(x \\\\)\\)")]
    [InlineData("\\[x \\\\]", "\\[x \\\\]\\]")]
    // An escaped backslash followed by a real closer: closed.
    [InlineData("\\(x \\\\\\) *y", "\\(x \\\\\\) *y*")]
    public void Bracket_math_is_closed_when_enabled(string text, string expected) =>
        Assert.Equal(expected, MarkdownHealer.Heal(text, 0, Bracket));

    [Fact]
    public void Bracket_math_closer_is_not_a_link_text_bracket() =>
        // The ] after the opened \[ is part of the formula, not the end of a link text.
        Assert.Equal("[a](b) \\[x] *y\\]", MarkdownHealer.Heal("[a](b) \\[x] *y", 0, Bracket));

    [Fact]
    public void Bracket_math_does_not_close_the_link_text_bracket()
    {
        // The open [ stays open under the formula, and the bracket entry writes nothing.
        Assert.Equal("[a \\(x\\)", MarkdownHealer.Heal("[a \\(x", 0, Bracket));
        // A ] inside the formula is not matched with the [ before it, since the scan has stopped.
        Assert.Equal("[a \\(x]\\)", MarkdownHealer.Heal("[a \\(x]", 0, Bracket));
    }

    [Fact]
    public void Bracket_math_parenthesis_is_an_escape_without_the_option() =>
        Assert.Equal("\\(x *y*", MarkdownHealer.Heal("\\(x *y", 0, DollarMath));

    [Fact]
    public void Bracket_math_bracket_is_an_escape_without_the_option() =>
        Assert.Same("\\[x", MarkdownHealer.Heal("\\[x", 0, DollarMath));

    [Fact]
    public void OptionsFor_reads_the_pipeline()
    {
        Assert.Equal(new HealingOptions(false, false), MarkdownHealer.OptionsFor(new MarkdownPipelineBuilder().Build()));
        Assert.Equal(new HealingOptions(true, false), MarkdownHealer.OptionsFor(new MarkdownPipelineBuilder().UseMathematics().Build()));
    }

    [Fact]
    public void OptionsFor_recognises_the_bracket_math_extension_by_its_type_name()
    {
        // The extension is declared in MarkView.Avalonia.Math, which this project does not reference: a
        // type of the same full name stands in for it. It is emitted into a dynamic assembly because a real
        // namespace MarkView.Avalonia.Math in this assembly would shadow System.Math in every test.
        var builder = new MarkdownPipelineBuilder();
        builder.Extensions.Add(NamedExtension("MarkView.Avalonia.Math.Parsing.BracketMathExtension"));
        Assert.Equal(new HealingOptions(false, true), MarkdownHealer.OptionsFor(builder.Build()));

        var other = new MarkdownPipelineBuilder();
        other.Extensions.Add(NamedExtension("MarkView.Avalonia.Math.Parsing.OtherExtension"));
        Assert.Equal(new HealingOptions(false, false), MarkdownHealer.OptionsFor(other.Build()));
    }

    /// <summary>An extension that does nothing, of a type with the full name <paramref name="fullName"/>.</summary>
    private static IMarkdownExtension NamedExtension(string fullName)
    {
        var module = AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName(fullName), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("Main");
        var type = module.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Sealed, typeof(object), [typeof(IMarkdownExtension)]);
        type.DefineDefaultConstructor(MethodAttributes.Public);
        foreach (var method in typeof(IMarkdownExtension).GetMethods())
        {
            var implementation = type.DefineMethod(
                method.Name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                method.ReturnType,
                method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
            implementation.GetILGenerator().Emit(OpCodes.Ret);
            type.DefineMethodOverride(implementation, method);
        }
        return (IMarkdownExtension)Activator.CreateInstance(type.CreateType())!;
    }

    [Fact]
    public void TailBlock_is_the_paragraph_at_the_end()
    {
        var text = "a\n\nb";
        var tail = Assert.IsType<ParagraphBlock>(MarkdownHealer.TailBlock(Parse(text), text));
        Assert.Equal(3, tail.Span.Start);
    }

    [Theory]
    [InlineData("a\n\nb\n")]
    [InlineData("a\n\nb\r\n")]
    [InlineData("a\n\nb\r")]
    public void TailBlock_allows_one_trailing_line_break(string text) =>
        Assert.Equal(3, MarkdownHealer.TailBlock(Parse(text), text)!.Span.Start);

    [Theory]
    [InlineData("")]
    [InlineData("a\n\n")]
    [InlineData("a\r\n\r\n")]
    [InlineData("a\r\n\r")]
    [InlineData("a\r\r")]
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
