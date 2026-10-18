// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// Pins the Markdig behaviour that incremental rendering of a streamed document relies on, with the
/// pipeline the viewer uses by default. These tests construct no Avalonia object.
/// </summary>
public class MarkdigStreamingFactsTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();

    private static MarkdownDocument Parse(string text) => Markdown.Parse(text, Pipeline);

    private static IEnumerable<MarkdownObject> Descendants(ParagraphBlock paragraph) =>
        paragraph.Inline is { } inline ? Descendants(inline) : [];

    private static IEnumerable<MarkdownObject> Descendants(ContainerInline container)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            yield return child;
            if (child is ContainerInline nested)
            {
                foreach (var descendant in Descendants(nested))
                    yield return descendant;
            }
        }
    }

    [Fact]
    public void Blocks_reaching_end_of_input_stay_open()
    {
        var single = Parse("para");
        Assert.True(single[0].IsOpen);

        var two = Parse("para\n\nnext");
        Assert.False(two[0].IsOpen);
        Assert.True(two[1].IsOpen);
    }

    [Fact]
    public void Containers_reaching_end_of_input_stay_open()
    {
        Assert.True(Assert.IsType<ListBlock>(Parse("- a")[0]).IsOpen);
        Assert.True(Assert.IsType<QuoteBlock>(Parse("> q")[0]).IsOpen);
        Assert.True(Assert.IsType<Markdig.Extensions.Tables.Table>(Parse("| a |\n|---|\n| 1 |")[0]).IsOpen);
    }

    [Fact]
    public void List_with_several_items_is_closed_at_end_of_input_but_its_last_item_is_open()
    {
        // Markdig closes the list block itself as soon as a second item starts, so ListBlock.IsOpen
        // cannot tell whether a list is still being written: its items stay open.
        var list = Assert.IsType<ListBlock>(Parse("- a\n- b")[0]);

        Assert.False(list.IsOpen);
        Assert.True(list[^1].IsOpen);
    }

    [Fact]
    public void Unclosed_fence_parses_as_a_code_block()
    {
        var fence = Assert.IsType<FencedCodeBlock>(Parse("```cs\nx")[0]);

        Assert.True(fence.IsOpen);
        Assert.Equal(0, fence.ClosingFencedCharCount);
    }

    [Fact]
    public void Closed_fence_is_not_open()
    {
        var fence = Assert.IsType<FencedCodeBlock>(Parse("```\nx\n```")[0]);

        Assert.False(fence.IsOpen);
    }

    [Theory]
    [InlineData("**a")]
    [InlineData("[t](u")]
    [InlineData("`c")]
    [InlineData("~~s")]
    public void Unclosed_inline_syntax_is_literal(string text)
    {
        var paragraph = Assert.IsType<ParagraphBlock>(Parse(text)[0]);

        var descendants = Descendants(paragraph).ToList();
        Assert.NotEmpty(descendants);
        Assert.DoesNotContain(descendants, d => d is EmphasisInline or LinkInline or CodeInline);
    }

    [Fact]
    public void Inline_parent_block_is_set()
    {
        var paragraph = Assert.IsType<ParagraphBlock>(Parse("**a**")[0]);

        var root = Assert.IsAssignableFrom<ContainerInline>(paragraph.Inline);
        Assert.Same(paragraph, root.ParentBlock);
    }

    [Fact]
    public void Link_reference_definition_group_sits_at_its_first_definition_and_stays_open()
    {
        var document = Parse("[a]: /u\n\npara\n\nend");

        Assert.Equal(3, document.Count);
        var group = Assert.IsType<LinkReferenceDefinitionGroup>(document[0]);
        Assert.True(group.IsOpen);
        Assert.IsType<ParagraphBlock>(document[1]);
        Assert.IsType<ParagraphBlock>(document[2]);

        var later = Parse("[a]\n\n[a]: /u");
        var paragraph = Assert.IsType<ParagraphBlock>(later[0]);
        Assert.Contains(Descendants(paragraph), d => d is LinkInline);
        Assert.IsType<LinkReferenceDefinitionGroup>(later[1]);
    }

    [Fact]
    public void Pipe_table_stays_open_after_the_blank_line_that_ends_it()
    {
        var document = Parse("| a |\n|---|\n| 1 |\n\nafter");

        Assert.Equal(2, document.Count);
        var table = Assert.IsType<Table>(document[0]);
        Assert.True(table.IsOpen);
        Assert.Equal((0, 16), (table.Span.Start, table.Span.End));
        Assert.IsType<ParagraphBlock>(document[1]);
    }

    [Fact]
    public void Footnote_group_is_last_and_open()
    {
        var document = Parse("x[^1]\n\n[^1]: note\n\nafter");

        var group = Assert.IsType<FootnoteGroup>(document[^1]);
        Assert.True(group.IsOpen);
        Assert.IsType<LinkReferenceDefinitionGroup>(document[1]);

        var alone = Assert.IsType<ParagraphBlock>(Parse("x[^1]")[0]);
        Assert.DoesNotContain(Descendants(alone), d => d is FootnoteLink);
    }

    [Fact]
    public void Atx_heading_at_end_of_input_is_closed()
    {
        var document = Parse("# title");
        Assert.False(document[0].IsOpen);
        Assert.Equal(6, document[0].Span.End);
    }

    [Fact]
    public void Paragraph_span_excludes_its_trailing_line_break()
    {
        Assert.Equal(2, Parse("abc\n")[0].Span.End);
        Assert.Equal(2, Parse("abc\n\n")[0].Span.End);
    }

    [Fact]
    public void Code_span_closer_must_have_the_same_length()
    {
        var paragraph = Assert.IsType<ParagraphBlock>(Parse("``a` b``")[0]);
        Assert.Single(Descendants(paragraph).OfType<CodeInline>());
        Assert.Empty(Descendants(Assert.IsType<ParagraphBlock>(Parse("``a`")[0])).OfType<CodeInline>());
    }

    [Fact]
    public void Open_fence_span_stops_at_its_opening_line()
    {
        // The span of an open fence is not extended over its content lines; only its lines reach the end.
        var fence = Assert.IsType<FencedCodeBlock>(Parse("```\ncode")[0]);
        Assert.Equal(3, fence.Span.End);
        Assert.Equal(7, fence.Lines.Lines[0].Slice.End);
    }
}
