// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Parsing;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

/// <summary>
/// How <see cref="HtmlDetailsGrouper"/> groups the HTML blocks of <c>&lt;details&gt;</c> / <c>&lt;summary&gt;</c>
/// and the markdown between them into <see cref="DetailsBlock"/>s. These tests build no Avalonia object.
/// </summary>
public class HtmlDetailsParsingTests
{
    // Built for each test: mutation testing runs every mutant in one process, and a pipeline built while a
    // mutant of the extension's setup is active would keep it for the following ones.
    private static MarkdownPipeline Pipeline => new MarkdownPipelineBuilder().UseHtmlDetails().Build();

    private static MarkdownDocument Parse(string markdown, MarkdownPipeline? pipeline = null) =>
        Markdown.Parse(markdown, pipeline ?? Pipeline);

    private static DetailsBlock SingleDetails(string markdown) =>
        Assert.IsType<DetailsBlock>(Assert.Single(Parse(markdown)));

    private static string Text(Inline? inline) => inline switch
    {
        null => string.Empty,
        LiteralInline literal => literal.Content.ToString(),
        LineBreakInline => "\n",
        CodeInline code => code.Content,
        ContainerInline container => string.Concat(container.Select(Text)),
        _ => string.Empty,
    };

    private static string Text(Block block) => block switch
    {
        LeafBlock { Inline: { } inline } => Text(inline),
        HtmlBlock html => html.Lines.ToString(),
        _ => block.GetType().Name,
    };

    private static string[] Texts(ContainerBlock container) => container.Select(Text).ToArray();

    [Fact]
    public void Markdown_between_tags_separated_by_blank_lines_becomes_the_body()
    {
        var details = SingleDetails("""
            <details>
            <summary>Title</summary>

            Body **bold**

            - item

            </details>
            """);

        Assert.Equal("Title", details.Summary);
        Assert.False(details.StartsExpanded);
        Assert.Equal(2, details.Count);
        Assert.Equal("Body bold", Text(details[0]));
        Assert.IsType<ListBlock>(details[1]);
        Assert.All(details, block => Assert.Same(details, block.Parent));
    }

    [Theory]
    [InlineData("<details>\n<summary>Q</summary>\nThe answer &amp; more\n</details>")]
    [InlineData("<details><summary>Q</summary>The answer &amp; more</details>")]
    [InlineData("<details>\r\n<summary>Q</summary>\r\nThe   answer\r\n&amp; more\r\n</details>")]
    public void Text_inside_a_single_html_block_is_a_plain_text_paragraph(string markdown)
    {
        var details = SingleDetails(markdown);

        Assert.Equal("Q", details.Summary);
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(details));
        Assert.Equal("The answer & more", Assert.IsType<LiteralInline>(Assert.Single(paragraph.Inline!)).Content.ToString());
    }

    [Theory]
    [InlineData("<details open>", true)]
    [InlineData("<details OPEN>", true)]
    [InlineData("<details open=\"\">", true)]
    [InlineData("<details class='x' open>", true)]
    [InlineData("<details\nopen>", true)]
    [InlineData("<details>", false)]
    [InlineData("<details class=\"open\">", false)]
    [InlineData("<details title='is open now'>", false)]
    [InlineData("<details data-open>", false)]
    [InlineData("<details opened>", false)]
    public void Open_attribute_starts_the_body_expanded(string tag, bool expanded)
    {
        var details = SingleDetails($"{tag}\n<summary>S</summary>\n\nBody\n\n</details>");

        Assert.Equal(expanded, details.StartsExpanded);
    }

    [Theory]
    [InlineData("<details>\n\nBody\n\n</details>")]
    [InlineData("<details>Body</details>")]
    public void Missing_summary_leaves_it_null(string markdown)
    {
        var details = SingleDetails(markdown);

        Assert.Null(details.Summary);
        Assert.Equal(["Body"], Texts(details));
    }

    [Fact]
    public void Summary_markup_is_removed_and_entities_decoded()
    {
        var details = SingleDetails("<details>\n<summary class=\"s\">\n  <b>Bold</b> &amp;\n  <code>code</code> <!-- note --></summary>\n</details>");

        Assert.Equal("Bold & code", details.Summary);
        Assert.Empty(details);
    }

    [Fact]
    public void Summary_in_its_own_html_block_is_the_summary()
    {
        var details = SingleDetails("<details>\n\n<summary>Own block</summary>\n\nBody\n\n</details>");

        Assert.Equal("Own block", details.Summary);
        Assert.Equal(["Body"], Texts(details));
    }

    [Fact]
    public void Only_the_first_summary_is_the_summary_and_a_later_one_is_text()
    {
        var details = SingleDetails("<details>\n<summary>First</summary>\n<summary>Second</summary>\n</details>");

        Assert.Equal("First", details.Summary);
        Assert.Equal(["Second"], Texts(details));
    }

    [Fact]
    public void Line_breaks_become_hard_breaks_and_trailing_ones_are_dropped()
    {
        var details = SingleDetails("<details><summary>S</summary>one<br>two<BR/>three <br /><br></details>");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(details));
        Assert.Equal("one\ntwo\nthree", Text(paragraph));
        Assert.All(paragraph.Inline!.OfType<LineBreakInline>(), lineBreak => Assert.True(lineBreak.IsHard));
    }

    [Fact]
    public void Leading_and_blank_lines_keep_their_breaks_after_the_first_text()
    {
        var details = SingleDetails("<details><summary>S</summary><br>one<br><br>two</details>");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(details));
        Assert.Equal("one\n\ntwo", Text(paragraph));
        Assert.Equal(4, paragraph.Inline!.Count());
    }

    [Theory]
    [InlineData("<details><summary>S</summary><img src=\"x.png\"></details>")]
    [InlineData("<details><summary>S</summary>  <br>  <!-- hidden --> </details>")]
    [InlineData("<details><summary>S</summary>\n\n</details>")]
    public void Html_without_text_adds_no_paragraph(string markdown)
    {
        Assert.Empty(SingleDetails(markdown));
    }

    [Fact]
    public void Less_than_sign_that_starts_no_tag_is_text()
    {
        var details = SingleDetails("<details><summary>a &lt; b</summary>1 < 2 &gt; 0</details>");

        Assert.Equal("a < b", details.Summary);
        Assert.Equal(["1 < 2 > 0"], Texts(details));
    }

    [Fact]
    public void Non_breaking_spaces_are_kept()
    {
        var details = SingleDetails("<details><summary>&nbsp;S&nbsp;</summary></details>");

        Assert.Equal(" S ", details.Summary);
    }

    [Fact]
    public void Details_nest_across_blocks()
    {
        var outer = SingleDetails("""
            <details>
            <summary>Outer</summary>

            Before

            <details open>
            <summary>Inner</summary>

            Inside

            </details>

            After

            </details>
            """);

        Assert.Equal("Outer", outer.Summary);
        Assert.Equal(3, outer.Count);
        Assert.Equal("Before", Text(outer[0]));
        var inner = Assert.IsType<DetailsBlock>(outer[1]);
        Assert.Equal(("Inner", true), (inner.Summary, inner.StartsExpanded));
        Assert.Equal(["Inside"], Texts(inner));
        Assert.Equal("After", Text(outer[2]));
    }

    [Fact]
    public void Details_nest_within_one_html_block()
    {
        var outer = SingleDetails("<details><summary>A</summary>one<details><summary>B</summary>two</details>three</details>");

        Assert.Equal("A", outer.Summary);
        Assert.Equal(3, outer.Count);
        Assert.Equal("one", Text(outer[0]));
        var inner = Assert.IsType<DetailsBlock>(outer[1]);
        Assert.Equal("B", inner.Summary);
        Assert.Equal(["two"], Texts(inner));
        Assert.Equal("three", Text(outer[2]));
    }

    [Fact]
    public void Consecutive_details_sharing_an_html_block_are_siblings()
    {
        var document = Parse("""
            <details>
            <summary>A</summary>

            Body A

            </details>
            <details>
            <summary>B</summary>

            Body B

            </details>
            """);

        Assert.Equal(2, document.Count);
        var first = Assert.IsType<DetailsBlock>(document[0]);
        var second = Assert.IsType<DetailsBlock>(document[1]);
        Assert.Equal(("A", "B"), (first.Summary, second.Summary));
        Assert.Equal(["Body A"], Texts(first));
        Assert.Equal(["Body B"], Texts(second));
    }

    [Fact]
    public void Blocks_around_the_details_stay_in_place()
    {
        var document = Parse("Before\n\n<details>\n<summary>S</summary>\n\nBody\n\n</details>\n\nAfter");

        Assert.Equal(3, document.Count);
        Assert.Equal("Before", Text(document[0]));
        Assert.Equal(["Body"], Texts(Assert.IsType<DetailsBlock>(document[1])));
        Assert.Equal("After", Text(document[2]));
    }

    [Fact]
    public void Details_inside_a_list_item_are_grouped_there()
    {
        var document = Parse("- item\n\n  <details>\n  <summary>S</summary>\n\n  Body\n\n  </details>");

        var item = Assert.IsType<ListItemBlock>(Assert.Single(Assert.IsType<ListBlock>(Assert.Single(document))));
        Assert.Equal(2, item.Count);
        var details = Assert.IsType<DetailsBlock>(item[1]);
        Assert.Equal("S", details.Summary);
        Assert.Equal(["Body"], Texts(details));
    }

    [Fact]
    public void Details_inside_details_inside_a_quote_are_grouped()
    {
        var quote = Assert.IsType<QuoteBlock>(Assert.Single(Parse("> <details>\n> <summary>S</summary>\n>\n> Body\n>\n> </details>")));

        Assert.Equal(["Body"], Texts(Assert.IsType<DetailsBlock>(Assert.Single(quote))));
    }

    [Theory]
    [InlineData("<details>\n<summary>S</summary>\n\nBody")]
    [InlineData("Body\n\n</details>")]
    [InlineData("<details>\n<summary>S</summary>\n\n- item\n\n  </details>")]
    [InlineData("<summary>S</summary>")]
    public void Unmatched_tags_keep_their_html_blocks(string markdown)
    {
        var plain = Parse(markdown, new MarkdownPipelineBuilder().Build());
        var document = Parse(markdown);

        Assert.Empty(document.Descendants<DetailsBlock>());
        Assert.Equal(plain.Descendants().Select(Describe), document.Descendants().Select(Describe));
    }

    private static string Describe(MarkdownObject obj) => $"{obj.GetType().Name}:{obj.Span}";

    [Fact]
    public void Html_blocks_without_details_tags_are_kept_as_they_are()
    {
        var document = Parse("<div>\nintro\n</div>\n\n<details>\n<summary>S</summary>\n\n<p>kept</p>\n\n</details>");

        var html = Assert.IsType<HtmlBlock>(document[0]);
        Assert.Equal("<div>\nintro\n</div>", html.Lines.ToString());
        var details = Assert.IsType<DetailsBlock>(document[1]);
        var inner = Assert.IsType<HtmlBlock>(Assert.Single(details));
        Assert.Equal("<p>kept</p>", inner.Lines.ToString());
    }

    [Fact]
    public void Unmatched_tags_beside_a_matched_pair_stay_html()
    {
        var document = Parse("<div>intro</div>\n<details><summary>A</summary>Body</details>\n<details>\n<summary>Unclosed</summary>");

        Assert.Equal(3, document.Count);
        var before = Assert.IsType<HtmlBlock>(document[0]);
        Assert.Equal("<div>intro</div>", before.Lines.ToString());
        Assert.Equal(HtmlBlockType.InterruptingBlock, before.Type);
        Assert.Equal(["Body"], Texts(Assert.IsType<DetailsBlock>(document[1])));
        var after = Assert.IsType<HtmlBlock>(document[2]);
        Assert.Equal("<details>\n<summary>Unclosed</summary>", after.Lines.ToString());
        Assert.Equal((before.Span, before.Line, before.Column), (after.Span, after.Line, after.Column));
    }

    [Fact]
    public void Html_around_a_pair_in_the_same_block_is_kept_whole_comments_included()
    {
        var document = Parse("<div><!-- c --></div>\n<details><summary>S</summary>Body</details>\n<p>after</p>");

        Assert.Equal(3, document.Count);
        Assert.Equal("<div><!-- c --></div>", Assert.IsType<HtmlBlock>(document[0]).Lines.ToString());
        Assert.IsType<DetailsBlock>(document[1]);
        Assert.Equal("<p>after</p>", Assert.IsType<HtmlBlock>(document[2]).Lines.ToString());
    }

    [Fact]
    public void Text_between_consecutive_details_adds_no_html_block()
    {
        var document = Parse("<details><summary>A</summary></details>  \n\t<details><summary>B</summary></details>");

        Assert.Equal(["A", "B"], document.Cast<DetailsBlock>().Select(d => d.Summary));
    }

    [Fact]
    public void Stray_closing_tag_after_a_pair_stays_html()
    {
        var document = Parse("<details><summary>A</summary></details>\n</details>");

        Assert.Equal(2, document.Count);
        Assert.IsType<DetailsBlock>(document[0]);
        Assert.Equal("</details>", Assert.IsType<HtmlBlock>(document[1]).Lines.ToString());
    }

    [Theory]
    [InlineData("<!--\n<details>\n-->\n\nBody\n\n</details>")]
    [InlineData("<div><!-- <details> --></div>\n\nBody\n\n</details>")]
    [InlineData("<pre>\n<details>\n</pre>\n\nBody\n\n</details>")]
    [InlineData("```\n<details>\n```\n\nBody\n\n</details>")]
    [InlineData("<details>\n\n```\n</details>\n```")]
    public void Tags_in_comments_preformatted_text_and_code_do_not_count(string markdown)
    {
        Assert.Empty(Parse(markdown).Descendants<DetailsBlock>());
    }

    [Fact]
    public void Comment_inside_a_details_block_is_not_text()
    {
        var details = SingleDetails("<details><summary>S</summary><!-- a <details> b --></details>");

        Assert.Empty(details);
    }

    [Fact]
    public void Details_span_runs_from_its_opening_tag_to_its_closing_tag()
    {
        const string markdown = "Intro\n\n  <details>\n  <summary>S</summary>\n\n  Body\n\n  </details>\n\nAfter";
        var details = Assert.IsType<DetailsBlock>(Parse(markdown)[1]);

        // As Markdig places the HTML block holding the opening tag (in column 0, whatever its indentation).
        Assert.Equal((2, 0), (details.Line, details.Column));
        Assert.Equal(markdown.IndexOf("<details>", StringComparison.Ordinal), details.Span.Start);
        Assert.Equal(markdown.IndexOf("</details>", StringComparison.Ordinal) + "</details>".Length - 1, details.Span.End);
    }

    [Fact]
    public void Paragraph_from_html_text_takes_the_position_of_its_html_block()
    {
        var details = Assert.IsType<DetailsBlock>(Parse("Intro\n\n<details><summary>S</summary>Text</details>")[1]);

        var paragraph = Assert.Single(details);
        Assert.Equal((details.Span, details.Line, details.Column), (paragraph.Span, paragraph.Line, paragraph.Column));
    }

    [Fact]
    public void Headings_and_code_in_the_body_are_kept()
    {
        var details = SingleDetails("<details>\n<summary>S</summary>\n\n## Heading\n\n```bash\nnpm install\n```\n</details>");

        Assert.IsType<HeadingBlock>(details[0]);
        Assert.IsType<FencedCodeBlock>(details[1]);
        Assert.Equal(2, details.Count);
    }

    [Fact]
    public void Closing_tag_right_after_a_paragraph_closes_the_details()
    {
        var details = SingleDetails("<details>\n<summary>S</summary>\n\nBody\n</details>");

        Assert.Equal(["Body"], Texts(details));
    }

    [Fact]
    public void Indented_summary_and_code_as_often_written_in_readmes()
    {
        var details = SingleDetails("<details>\n  <summary>Install</summary>\n\n  ```bash\n  npm install\n  ```\n</details>");

        Assert.Equal("Install", details.Summary);
        var code = Assert.IsType<FencedCodeBlock>(Assert.Single(details));
        Assert.Equal("npm install", code.Lines.ToString());
    }

    [Fact]
    public void Disabling_html_disables_details()
    {
        var pipeline = new MarkdownPipelineBuilder().UseHtmlDetails().DisableHtml().Build();

        Assert.Empty(Parse("<details>\n<summary>S</summary>\n\nBody\n\n</details>", pipeline).Descendants<DetailsBlock>());
    }

    [Fact]
    public void Without_the_extension_details_stay_html_blocks()
    {
        var document = Parse("<details>\n<summary>S</summary>\n\nBody\n\n</details>", new MarkdownPipelineBuilder().Build());

        Assert.Empty(document.Descendants<DetailsBlock>());
        Assert.Equal(2, document.OfType<HtmlBlock>().Count());
    }

    [Fact]
    public void Document_without_html_is_unchanged()
    {
        var document = Parse("# Title\n\n- a\n- b\n\n> quote");

        Assert.Equal([typeof(HeadingBlock), typeof(ListBlock), typeof(QuoteBlock)], document.Select(b => b.GetType()));
    }

    [Fact]
    public void UseHtmlDetails_adds_its_extension_once()
    {
        var builder = new MarkdownPipelineBuilder().UseHtmlDetails().UseHtmlDetails();

        Assert.Single(builder.Extensions.OfType<HtmlDetailsExtension>());
    }

    [Fact]
    public void GitHub_extensions_include_details()
    {
        var builder = new MarkdownPipelineBuilder().UseGitHubExtensions();

        Assert.Single(builder.Extensions.OfType<HtmlDetailsExtension>());
        Assert.Single(Markdown.Parse("<details><summary>S</summary>Body</details>", builder.Build()).Descendants<DetailsBlock>());
    }

    [Fact]
    public void Supported_extensions_do_not_include_details()
    {
        var pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

        Assert.Empty(Markdown.Parse("<details><summary>S</summary>Body</details>", pipeline).Descendants<DetailsBlock>());
    }
}
