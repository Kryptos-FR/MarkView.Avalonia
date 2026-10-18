// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Syntax;

using MarkView.Avalonia.Parsing;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

/// <summary>
/// How <see cref="ThinkingBlockParser"/> splits a language model's output into its reasoning and its answer.
/// </summary>
public class ThinkingBlockParsingTests
{
    private static MarkdownDocument Parse(string markdown, ThinkingBlockOptions? options = null) =>
        Markdown.Parse(markdown, new MarkdownPipelineBuilder().UseGitHubExtensions().UseThinkingBlocks(options).Build());

    private static string Text(Block block) => block switch
    {
        LeafBlock leaf when leaf.Inline is { } inline => string.Concat(inline.Descendants<Markdig.Syntax.Inlines.LiteralInline>().Select(l => l.Content.ToString())),
        LeafBlock leaf => leaf.Lines.ToString(),
        _ => block.GetType().Name,
    };

    [Fact]
    public void The_reasoning_between_the_tags_is_markdown_and_the_answer_follows()
    {
        var document = Parse("<think>\nFirst **idea**\n- a\n- b\n</think>\n\nThe answer");

        Assert.Equal(2, document.Count);
        var thinking = Assert.IsType<ThinkingBlock>(document[0]);
        Assert.True(thinking.HasClosingTag);
        Assert.Equal("think", thinking.TagName);
        Assert.Collection(thinking,
            b => Assert.Equal("First idea", Text(Assert.IsType<ParagraphBlock>(b))),
            b => Assert.IsType<ListBlock>(b));
        Assert.Equal("The answer", Text(Assert.IsType<ParagraphBlock>(document[1])));
    }

    [Fact]
    public void The_block_spans_its_tags()
    {
        const string markdown = "<think>\nIdea\n</think>\nAnswer";
        var thinking = Assert.IsType<ThinkingBlock>(Parse(markdown)[0]);

        Assert.Equal(0, thinking.Span.Start);
        Assert.Equal(markdown.IndexOf("</think>", StringComparison.Ordinal) + "</think>".Length - 1, thinking.Span.End);
    }

    [Fact]
    public void An_answer_right_after_the_closing_tag_line_is_not_part_of_the_reasoning()
    {
        var document = Parse("<think>\nIdea\n</think>\nAnswer");

        var thinking = Assert.IsType<ThinkingBlock>(document[0]);
        Assert.Equal("Idea", Text(Assert.Single(thinking)));
        Assert.Equal("Answer", Text(Assert.IsType<ParagraphBlock>(document[1])));
    }

    [Fact]
    public void Text_on_the_opening_tag_line_starts_the_reasoning()
    {
        var thinking = Assert.IsType<ThinkingBlock>(Parse("<think>Okay, so\nthe user asks\n</think>")[0]);

        Assert.Equal("Okay, sothe user asks", Text(Assert.Single(thinking)));
    }

    [Fact]
    public void A_closing_tag_can_end_the_last_line_of_the_reasoning()
    {
        var document = Parse("<think>\nSo the answer is 4.</think>\nIt is 4.");

        var thinking = Assert.IsType<ThinkingBlock>(document[0]);
        Assert.True(thinking.HasClosingTag);
        Assert.Equal("So the answer is 4.", Text(Assert.Single(thinking)));
        Assert.Equal("It is 4.", Text(Assert.IsType<ParagraphBlock>(document[1])));
    }

    [Fact]
    public void Reasoning_on_a_single_line_between_the_tags()
    {
        var document = Parse("<think>Short thought</think>  \nAnswer");

        var thinking = Assert.IsType<ThinkingBlock>(document[0]);
        Assert.True(thinking.HasClosingTag);
        Assert.Equal("Short thought", Text(Assert.Single(thinking)));
        Assert.Equal("Answer", Text(Assert.IsType<ParagraphBlock>(document[1])));
    }

    [Fact]
    public void Text_after_a_closing_tag_that_starts_a_line_follows_the_block()
    {
        var document = Parse("<think>\nIdea\n</think>The answer");

        Assert.Equal("Idea", Text(Assert.Single(Assert.IsType<ThinkingBlock>(document[0]))));
        Assert.Equal("The answer", Text(Assert.IsType<ParagraphBlock>(document[1])));
    }

    [Fact]
    public void An_empty_block_has_no_content()
    {
        var thinking = Assert.IsType<ThinkingBlock>(Parse("<think>\n\n</think>\n\nAnswer")[0]);

        Assert.True(thinking.HasClosingTag);
        Assert.Empty(thinking);
    }

    [Fact]
    public void An_empty_block_on_one_line_has_no_content()
    {
        var document = Parse("<think></think>\nAnswer");

        var thinking = Assert.IsType<ThinkingBlock>(document[0]);
        Assert.True(thinking.HasClosingTag);
        Assert.Empty(thinking);
        Assert.Equal("Answer", Text(Assert.IsType<ParagraphBlock>(document[1])));
    }

    [Fact]
    public void Without_its_closing_tag_the_block_runs_to_the_end()
    {
        var document = Parse("<think>\nStill thinking\n\nabout it");

        var thinking = Assert.IsType<ThinkingBlock>(Assert.Single(document));
        Assert.False(thinking.HasClosingTag);
        Assert.Equal(2, thinking.Count);
    }

    [Fact]
    public void A_closing_tag_inside_a_fenced_code_block_is_code()
    {
        var document = Parse("<think>\n```xml\n</think>\n```\n</think>\nAnswer");

        var thinking = Assert.IsType<ThinkingBlock>(document[0]);
        var code = Assert.IsType<FencedCodeBlock>(Assert.Single(thinking));
        Assert.Equal("</think>", code.Lines.ToString());
        Assert.Equal("Answer", Text(Assert.IsType<ParagraphBlock>(document[1])));
    }

    [Fact]
    public void A_closing_tag_ending_a_line_inside_a_fenced_code_block_is_code()
    {
        var document = Parse("<think>\n```\nx</think>\n```\n</think>");

        var code = Assert.IsType<FencedCodeBlock>(Assert.Single(Assert.IsType<ThinkingBlock>(Assert.Single(document))));
        Assert.Equal("x</think>", code.Lines.ToString());
    }

    [Theory]
    [InlineData("think")]
    [InlineData("thinking")]
    [InlineData("reasoning")]
    [InlineData("reason")]
    [InlineData("thought")]
    public void The_usual_tag_names_are_recognised(string name)
    {
        var thinking = Assert.IsType<ThinkingBlock>(Parse($"<{name}>\nIdea\n</{name}>\n\nAnswer")[0]);

        Assert.Equal(name, thinking.TagName);
        Assert.True(thinking.HasClosingTag);
    }

    [Fact]
    public void Tags_match_without_regard_to_case_and_may_have_spaces_before_their_end()
    {
        var document = Parse("<THINK >\nIdea\n</Think  >\nAnswer");

        var thinking = Assert.IsType<ThinkingBlock>(document[0]);
        Assert.True(thinking.HasClosingTag);
        Assert.Equal("Answer", Text(Assert.IsType<ParagraphBlock>(document[1])));
    }

    [Fact]
    public void Only_the_tag_that_opened_the_block_closes_it()
    {
        var thinking = Assert.IsType<ThinkingBlock>(Assert.Single(Parse("<thinking>\nIdea\n</think>\nmore")));

        Assert.False(thinking.HasClosingTag);
    }

    [Fact]
    public void The_tag_names_can_be_chosen()
    {
        var options = new ThinkingBlockOptions { TagNames = ["scratchpad", ""] };

        Assert.IsType<ThinkingBlock>(Parse("<scratchpad>\nIdea\n</scratchpad>", options)[0]);
        Assert.IsNotType<ThinkingBlock>(Parse("<think>\nIdea\n</think>", options)[0]);
    }

    [Theory]
    [InlineData("<thinker>\nIdea")]
    [InlineData("<think\nIdea")]
    [InlineData("<think x>\nIdea")]
    [InlineData("<")]
    [InlineData("    <think>\nIdea")]
    [InlineData("<thin")]
    public void Other_text_is_not_a_thinking_block(string markdown)
    {
        Assert.DoesNotContain(Parse(markdown), block => block is ThinkingBlock);
    }

    [Fact]
    public void A_closing_tag_without_its_slash_does_not_close_the_block()
    {
        var thinking = Assert.IsType<ThinkingBlock>(Assert.Single(Parse("<think>\nIdea\n<think>\n")));

        Assert.False(thinking.HasClosingTag);
    }

    [Fact]
    public void An_indented_closing_tag_is_content()
    {
        var thinking = Assert.IsType<ThinkingBlock>(Assert.Single(Parse("<think>\nIdea\n\n    </think>\n")));

        Assert.False(thinking.HasClosingTag);
    }

    [Fact]
    public void The_block_interrupts_a_paragraph_and_nests_in_containers()
    {
        var document = Parse("Let me think.\n<think>\nIdea\n</think>\n\n> <think>\n> Quoted\n> </think>");

        Assert.IsType<ParagraphBlock>(document[0]);
        Assert.IsType<ThinkingBlock>(document[1]);
        var quote = Assert.IsType<QuoteBlock>(document[2]);
        Assert.True(Assert.IsType<ThinkingBlock>(Assert.Single(quote)).HasClosingTag);
    }

    [Fact]
    public void Without_the_extension_the_tags_are_html()
    {
        var document = Markdown.Parse("<think>\nIdea\n</think>", new MarkdownPipelineBuilder().Build());

        Assert.IsType<HtmlBlock>(document[0]);
    }

    [Fact]
    public void Using_the_extension_again_replaces_its_options()
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseThinkingBlocks()
            .UseThinkingBlocks(new ThinkingBlockOptions { TagNames = ["scratchpad"] })
            .Build();

        Assert.Single(pipeline.Extensions.OfType<ThinkingBlockExtension>());
        Assert.IsNotType<ThinkingBlock>(Markdown.Parse("<think>\nIdea\n</think>", pipeline)[0]);
        Assert.IsType<ThinkingBlock>(Markdown.Parse("<scratchpad>\nIdea\n</scratchpad>", pipeline)[0]);
    }

    [Theory]
    [InlineData(0.2, "Thought for 1 second")]
    [InlineData(1.4, "Thought for 1 second")]
    [InlineData(1.5, "Thought for 2 seconds")]
    [InlineData(59.4, "Thought for 59 seconds")]
    [InlineData(59.5, "Thought for 1m 0s")]
    [InlineData(125, "Thought for 2m 5s")]
    public void Durations_are_formatted_in_seconds_then_minutes(double seconds, string expected)
    {
        Assert.Equal(expected, ThinkingBlockOptions.FormatDuration(TimeSpan.FromSeconds(seconds)));
    }
}
