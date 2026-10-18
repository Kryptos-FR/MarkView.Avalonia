// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Parsing;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

/// <summary>
/// How <see cref="HtmlSubsetGrouper"/> and <see cref="HtmlInlineBuilder"/> turn the HTML of a document into blocks
/// and inlines. These tests build no Avalonia object.
/// </summary>
public class HtmlSubsetParsingTests
{
    // Built for each test: mutation testing runs every mutant in one process, and a pipeline built while a
    // mutant of the extension's setup is active would keep it for the following ones.
    private static MarkdownPipeline Pipeline => new MarkdownPipelineBuilder().UseHtmlSubset().Build();

    private static MarkdownDocument Parse(string markdown, MarkdownPipeline? pipeline = null) =>
        Markdown.Parse(markdown, pipeline ?? Pipeline);

    /// <summary>
    /// A compact description of the blocks of <paramref name="markdown"/>: <c>P(…)</c> for a paragraph,
    /// <c>H2(…)</c> for a heading, <c>Center[…]</c> for an aligned block, <c>Hr</c>, <c>Html</c> for an HTML
    /// block left as it is, and the type name of other blocks.
    /// </summary>
    private static string[] Blocks(string markdown, MarkdownPipeline? pipeline = null) =>
        Parse(markdown, pipeline).Select(Describe).ToArray();

    /// <summary>The single paragraph of <paramref name="markdown"/>, described.</summary>
    private static string Inlines(string markdown) =>
        Describe(Assert.IsType<ParagraphBlock>(Assert.Single(Parse(markdown))).Inline);

    private static string Describe(Block block) => block switch
    {
        HeadingBlock heading => $"H{heading.Level}({Describe(heading.Inline)})",
        ParagraphBlock paragraph => $"P({Describe(paragraph.Inline)})",
        HtmlAlignedBlock aligned => $"{aligned.Alignment}[{string.Join(", ", aligned.Select(Describe))}]",
        DetailsBlock details => $"Details[{string.Join(", ", details.Select(Describe))}]",
        ThematicBreakBlock => "Hr",
        HtmlBlock => "Html",
        _ => block.GetType().Name,
    };

    /// <summary>
    /// A compact description of an inline: its text for a literal, <c>{tag}</c> for an HTML tag left as it is
    /// (which is not rendered), <c>|</c> for a line break, and <c>Kind(content)</c> for the others.
    /// </summary>
    private static string Describe(Inline? inline) => inline switch
    {
        null => string.Empty,
        LiteralInline literal => literal.Content.ToString(),
        HtmlInline html => $"{{{html.Tag}}}",
        HtmlEntityInline entity => entity.Transcoded.ToString(),
        LineBreakInline { IsHard: true } => "|",
        LineBreakInline => " ",
        CodeInline code => $"Code({code.Content})",
        LinkInline { IsImage: true } image => $"Img({image.Url}|{Children(image)}|{Size(image)})",
        LinkInline link => $"Link({link.Url}|{link.Title}:{Children(link)})",
        HtmlElementInline element => $"{element.Kind}({Children(element)})",
        EmphasisInline emphasis => $"Em{emphasis.DelimiterCount}({Children(emphasis)})",
        ContainerInline container => Children(container),
        _ => inline.GetType().Name,
    };

    private static string Children(ContainerInline container) => string.Concat(container.Select(Describe));

    private static string Size(LinkInline image) =>
        image.GetData(typeof(HtmlImageSize)) is HtmlImageSize size ? $"{size.Width}x{size.Height}" : string.Empty;

    // ── Inline tags ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("b", "Bold")]
    [InlineData("strong", "Bold")]
    [InlineData("i", "Italic")]
    [InlineData("em", "Italic")]
    [InlineData("del", "Strikethrough")]
    [InlineData("s", "Strikethrough")]
    [InlineData("strike", "Strikethrough")]
    [InlineData("ins", "Underline")]
    [InlineData("u", "Underline")]
    [InlineData("mark", "Mark")]
    [InlineData("sub", "Subscript")]
    [InlineData("sup", "Superscript")]
    [InlineData("kbd", "Keyboard")]
    [InlineData("KBD", "Keyboard")]
    public void A_pair_of_formatting_tags_becomes_an_element(string name, string kind)
    {
        Assert.Equal($"a {kind}(x) b", Inlines($"a <{name}>x</{name}> b"));
    }

    [Fact]
    public void Elements_nest()
    {
        Assert.Equal("Bold(Italic(x)y)", Inlines("<b><i>x</i>y</b>"));
        Assert.Equal("Bold(aBold(b)c)", Inlines("<b>a<b>b</b>c</b>"));
    }

    [Fact]
    public void An_end_tag_closes_the_elements_opened_inside_its_own()
    {
        Assert.Equal("Bold({<i>}x)y{</i>}", Inlines("<b><i>x</b>y</i>"));
    }

    [Theory]
    [InlineData("x</b>y", "x{</b>}y")]
    [InlineData("<b>x", "{<b>}x")]
    [InlineData("<b/>x", "{<b/>}x")]
    [InlineData("<b>x</i>y", "{<b>}x{</i>}y")]
    [InlineData("<span>x</span>", "{<span>}x{</span>}")]
    [InlineData("<iframe src=\"https://example.com\"></iframe>x", "{<iframe src=\"https://example.com\">}{</iframe>}x")]
    [InlineData("<b onclick=\"alert(1)\">x</b>", "Bold(x)")]
    [InlineData("x<!-- comment -->y", "x{<!-- comment -->}y")]
    public void Unmatched_and_unknown_tags_are_left_for_the_renderer_to_drop(string markdown, string expected)
    {
        Assert.Equal(expected, Inlines(markdown));
    }

    [Theory]
    [InlineData("a<script>alert(1)</script>b", "ab")]
    [InlineData("a<STYLE>p { color: red }</STYLE>b", "ab")]
    [InlineData("a<script><b>x</b> <img src=\"i.png\"></script>b", "ab")]
    [InlineData("a<script>x", "a{<script>}x")]
    public void The_content_of_script_and_style_elements_is_dropped(string markdown, string expected)
    {
        Assert.Equal(expected, Inlines(markdown));
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(Parse(markdown)));
        Assert.DoesNotContain(paragraph.Inline!, inline => inline.GetType() == typeof(ContainerInline));
    }

    [Fact]
    public void Markdown_inside_an_element_is_kept()
    {
        Assert.Equal("Bold(Em2(x) Link(u|:l))", Inlines("<b>**x** [l](u)</b>"));
    }

    [Fact]
    public void Tags_are_built_inside_markdown_inlines()
    {
        Assert.Equal("Link(u|:Bold(x))", Inlines("[<b>x</b>](u)"));
    }

    [Fact]
    public void Tags_pair_only_with_siblings()
    {
        Assert.Equal("Em1({<b>}x){</b>}", Inlines("*<b>x*</b>"));
    }

    [Theory]
    [InlineData("<a href=\"https://example.com\" title=\"Example\">x</a>", "Link(https://example.com|Example:x)")]
    [InlineData("<a href='docs/a.md'>x</a>", "Link(docs/a.md|:x)")]
    [InlineData("<a href=page.md>x</a>", "Link(page.md|:x)")]
    [InlineData("<a href=\" https://example.com \">x</a>", "Link(https://example.com|:x)")]
    [InlineData("<a href=\"?a=1&amp;b=2\">x</a>", "Link(?a=1&b=2|:x)")]
    [InlineData("<A HREF=\"u\">x</A>", "Link(u|:x)")]
    [InlineData("<a href=\"u\"><img src=\"i.png\"></a>", "Link(u|:Img(i.png||))")]
    public void A_link_becomes_a_markdown_link(string markdown, string expected)
    {
        Assert.Equal(expected, Inlines(markdown));
    }

    [Theory]
    [InlineData("<a name=\"top\">x</a>y")]
    [InlineData("<a href=\"\">x</a>y")]
    [InlineData("<a href=\" \">x</a>y")]
    [InlineData("<a>x</a>y")]
    public void An_anchor_without_a_link_shows_its_content(string markdown)
    {
        Assert.Equal("xy", Inlines(markdown));
    }

    [Fact]
    public void An_anchor_without_a_link_keeps_its_elements()
    {
        Assert.Equal("aBold(x)Italic(y)", Inlines("a<a name=\"n\"><b>x</b><i>y</i></a>"));
    }

    [Theory]
    [InlineData("<img src=\"i.png\" alt=\"A logo\" width=\"20\" height=\"10\">", "Img(i.png|A logo|20x10)")]
    [InlineData("<img src=\"i.png\" width=\"20px\">", "Img(i.png||20x)")]
    [InlineData("<img src=\"i.png\" height=\"10\" />", "Img(i.png||x10)")]
    [InlineData("<img src=\"i.png\" width=\"50%\" height=\"auto\">", "Img(i.png||)")]
    [InlineData("<img src=\" i.png \" alt=\"\">", "Img(i.png||)")]
    [InlineData("<img src=\"i.png\" WIDTH=\" 120 \">", "Img(i.png||120x)")]
    [InlineData("<img src=\"i.png\" width=\"123456\">", "Img(i.png||)")]
    public void An_image_becomes_a_markdown_image_with_its_size(string markdown, string expected)
    {
        Assert.Equal($"a {expected} b", Inlines($"a {markdown} b"));
    }

    [Theory]
    [InlineData("<img src=\"i.png\" title=\"Tip\">", "Tip")]
    [InlineData("<img src=\"i.png\">", "")]
    [InlineData("<a href=\"u\" title=\"Tip\">x</a>", "Tip")]
    [InlineData("<a href=\"u\">x</a>", "")]
    public void Images_and_links_keep_their_title(string html, string title)
    {
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(Parse(html)));
        var link = Assert.IsType<LinkInline>(paragraph.Inline!.Single());

        Assert.Equal(title, link.Title);
    }

    [Theory]
    [InlineData("a<img alt=\"x\">b")]
    [InlineData("a<img src=\"\">b")]
    [InlineData("a<img src=\"  \">b")]
    public void An_image_without_a_source_is_dropped(string markdown)
    {
        Assert.Equal("ab", Inlines(markdown));
    }

    [Theory]
    [InlineData("a<br>b")]
    [InlineData("a<br/>b")]
    [InlineData("a<BR />b")]
    public void A_line_break_tag_becomes_a_hard_line_break(string markdown)
    {
        Assert.Equal("a|b", Inlines(markdown));
    }

    [Fact]
    public void An_end_line_break_tag_is_left_for_the_renderer_to_drop()
    {
        Assert.Equal("a{</br>}b", Inlines("a</br>b"));
    }

    [Theory]
    [InlineData("<code>a &lt; b</code>", "Code(a < b)")]
    [InlineData("<code><b>x</b> `y`</code>", "Code(x y)")]
    [InlineData("<code>x<br>y</code>", "Code(x y)")]
    [InlineData("<code></code>", "Code()")]
    public void A_code_element_becomes_a_code_span_of_its_text(string markdown, string expected)
    {
        Assert.Equal(expected, Inlines(markdown));
    }

    [Fact]
    public void Tags_are_built_in_headings_list_items_and_table_cells()
    {
        var pipeline = new MarkdownPipelineBuilder().UseHtmlSubset().UsePipeTables().Build();
        var document = Parse("# T <sup>1</sup>\n\n- <b>x</b>\n\n| <kbd>A</kbd> |\n|---|\n| <i>c</i> |", pipeline);

        Assert.Equal("H1(T Superscript(1))", Describe(document[0]));
        var item = Assert.IsType<ListItemBlock>(Assert.IsType<ListBlock>(document[1]).Single());
        Assert.Equal("P(Bold(x))", Describe(item.Single()));
        var table = Assert.IsType<Markdig.Extensions.Tables.Table>(document[2]);
        var cells = table.Descendants<ParagraphBlock>().Select(p => Describe(p.Inline)).ToArray();
        Assert.Equal(["Keyboard(A)", "Italic(c)"], cells);
    }

    [Fact]
    public void Html_is_literal_text_in_a_pipeline_that_disables_it()
    {
        var pipeline = new MarkdownPipelineBuilder().UseHtmlSubset().DisableHtml().Build();

        Assert.Equal("P(<b>x</b>)", Assert.Single(Blocks("<b>x</b>", pipeline)));
    }

    // ── HTML blocks ───────────────────────────────────────────────────────────

    [Fact]
    public void A_centered_paragraph_of_badges_becomes_an_aligned_paragraph()
    {
        var blocks = Blocks("""
            <p align="center">
              <a href="https://x.org"><img src="b.svg" alt="badge"></a>
              <a href="https://y.org"><img src="c.svg" width="80"></a>
            </p>
            """);

        Assert.Equal(["Center[P(Link(https://x.org|:Img(b.svg|badge|)) Link(https://y.org|:Img(c.svg||80x)))]"], blocks);
    }

    [Fact]
    public void A_readme_header_becomes_aligned_headings_and_paragraphs()
    {
        var blocks = Blocks("""
            <h1 align="center">
              <img src="logo.png" width="120"><br>
              Name
            </h1>
            <p align="center"><b>Fast</b> and <i>small</i>.</p>
            <hr>
            <p>Plain</p>
            """);

        Assert.Equal(
            ["Center[H1(Img(logo.png||120x)|Name)]", "Center[P(Bold(Fast) and Italic(small).)]", "Hr", "P(Plain)"],
            blocks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void A_heading_element_becomes_a_heading_of_its_level(int level)
    {
        Assert.Equal([$"H{level}(Title)"], Blocks($"<h{level}>Title</h{level}>"));
    }

    [Theory]
    [InlineData("<h7>Title</h7>")]
    [InlineData("<h0>Title</h0>")]
    [InlineData("<hx>Title</hx>")]
    [InlineData("<header>Title</header>")]
    public void Other_tags_starting_with_h_are_not_headings(string html)
    {
        Assert.Equal(["P(Title)"], Blocks($"<div>\n{html}\n</div>").Select(Visible));
    }

    [Fact]
    public void A_heading_element_is_marked_as_an_atx_heading()
    {
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(Parse("<h2>Title</h2>")));

        Assert.Equal('#', heading.HeaderChar);
    }

    [Theory]
    [InlineData("left", "Left")]
    [InlineData("center", "Center")]
    [InlineData("right", "Right")]
    [InlineData("justify", "Justify")]
    [InlineData(" CENTER ", "Center")]
    public void The_align_attribute_aligns_the_element(string value, string alignment)
    {
        Assert.Equal([$"{alignment}[P(x)]"], Blocks($"<p align=\"{value}\">x</p>"));
        Assert.Equal([$"{alignment}[H3(x)]"], Blocks($"<h3 align=\"{value}\">x</h3>"));
        Assert.Equal([$"{alignment}[P(x)]"], Blocks($"<div align=\"{value}\">x</div>"));
    }

    [Theory]
    [InlineData("<p align=\"middle\">x</p>")]
    [InlineData("<p align>x</p>")]
    [InlineData("<p class=\"center\">x</p>")]
    [InlineData("<div align=\"middle\">x</div>")]
    public void Elements_without_a_known_alignment_are_not_aligned(string html)
    {
        Assert.Equal(["P(x)"], Blocks(html));
    }

    [Fact]
    public void Text_is_collapsed_trimmed_and_decoded()
    {
        Assert.Equal(["P(a & b|c d e)"], Blocks("<p>\n  a   &amp;\n  b <br>\n  c\td <span> </span> e\n</p>").Select(Visible));
        // A non-breaking space does not collapse.
        Assert.Equal(["P(c \u00A0 d)"], Blocks("<p>c &nbsp; d</p>"));
    }

    [Theory]
    [InlineData("<p>a</br> b</p>", "P(a{</br>} b)")]
    [InlineData("<p>a </img> b</p>", "P(a {</img>}b)")]
    [InlineData("<p>a </img></p>", "P(a{</img>})")]
    [InlineData("<p>a <b></b></p>", "P(aBold())")]
    [InlineData("<p><b></b> a</p>", "P(Bold()a)")]
    public void Only_line_breaks_and_images_change_how_spaces_around_tags_collapse(string html, string expected)
    {
        Assert.Equal([expected], Blocks(html));
    }

    [Fact]
    public void Collapsed_text_leaves_no_empty_literal()
    {
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("<p>a <span> </span>b</p>")));

        Assert.DoesNotContain(paragraph.Inline!, inline => inline is LiteralInline { Content.Length: 0 });
        Assert.Equal(4, paragraph.Inline!.Count());
    }

    [Fact]
    public void Markdown_inside_an_html_block_is_not_parsed()
    {
        Assert.Equal(["Center[P(**not bold** [x](y))]"], Blocks("<div align=\"center\">\n**not bold** [x](y)\n</div>"));
    }

    [Theory]
    [InlineData("<p>a\n<p>b", new[] { "P(a)", "P(b)" })]
    [InlineData("<p>a<h2>b</h2>c</p>", new[] { "P(a)", "H2(b)", "P(c)" })]
    [InlineData("<p>a</h2>b</p>", new[] { "P(ab)" })]
    [InlineData("<p>a<hr>b", new[] { "P(a)", "Hr", "P(b)" })]
    [InlineData("<div>a<div>b</div>c</div>", new[] { "P(a)", "P(b)", "P(c)" })]
    [InlineData("<div>a</p>b</div>", new[] { "P(ab)" })]
    [InlineData("<p>a</p>b", new[] { "P(a)", "P(b)" })]
    public void A_paragraph_or_heading_ends_at_its_end_tag_or_another_block_element(string html, string[] expected)
    {
        Assert.Equal(expected, Blocks(html).Select(Visible));
    }

    [Theory]
    [InlineData("<p></p>")]
    [InlineData("<p> \n </p>")]
    [InlineData("<p><br></p>")]
    [InlineData("<h1></h1>")]
    [InlineData("<div>\n</div>")]
    [InlineData("<div><span> </span></div>")]
    [InlineData("<div><script>alert(1)</script></div>")]
    [InlineData("<div>\n<!-- a comment -->\n</div>")]
    public void Html_that_shows_nothing_adds_no_block(string html)
    {
        Assert.Empty(Blocks(html));
    }

    [Theory]
    [InlineData("<div>\n<span>x</span> <table><tr><td>y</td></tr></table>\n</div>", "P(x y)")]
    [InlineData("<div>\n<script>alert(1)</script>x\n</div>", "P(x)")]
    [InlineData("<div>\n<!-- c -->x\n</div>", "P(x)")]
    [InlineData("<div>a < b</div>", "P(a < b)")]
    [InlineData("<div>a <b c</div>", "P(a <b c)")]
    [InlineData("<details>\n<summary>S</summary>\nbody\n</details>", "P(S body)")]
    public void Other_tags_in_an_html_block_are_dropped_and_their_text_kept(string html, string expected)
    {
        Assert.Equal([expected], Blocks(html).Select(Visible));
    }

    [Theory]
    [InlineData("<script>\nalert(1)\n</script>")]
    [InlineData("<style>\np { color: red }\n</style>")]
    [InlineData("<pre>\ncode\n</pre>")]
    [InlineData("<!-- a comment -->")]
    [InlineData("<!DOCTYPE html>")]
    public void Html_blocks_outside_the_subset_are_left_as_they_are(string html)
    {
        Assert.Equal(["Html"], Blocks(html));
    }

    [Fact]
    public void Html_blocks_take_the_position_of_their_source()
    {
        var document = Parse("Intro\n\n<p>a</p>\n<hr>\n<p align=\"center\">b</p>");

        Assert.All(document.Skip(1), block => Assert.Equal((2, 0, 7), (block.Line, block.Column, block.Span.Start)));
        Assert.All(document.Skip(1), block => Assert.Equal(document[1].Span, block.Span));
        var aligned = Assert.IsType<HtmlAlignedBlock>(document[3]);
        Assert.Equal(document[1].Span, aligned.Single().Span);
        Assert.Equal(2, aligned.Single().Line);
    }

    // ── Div elements around markdown ──────────────────────────────────────────

    [Fact]
    public void A_div_separated_from_markdown_by_blank_lines_aligns_it()
    {
        var markdown = """
            Before

            <div align="center">

            # Title

            ![x](y.png) A <b>bold</b> word

            </div>

            After
            """;
        var document = Parse(markdown);

        Assert.Equal(["P(Before)", "Center[H1(Title), P(Img(y.png|x|) A Bold(bold) word)]", "P(After)"], document.Select(Describe));
        var aligned = Assert.IsType<HtmlAlignedBlock>(document[1]);
        Assert.Equal(markdown.IndexOf("<div", StringComparison.Ordinal), aligned.Span.Start);
        Assert.Equal(markdown.IndexOf("</div>", StringComparison.Ordinal) + "</div>".Length - 1, aligned.Span.End);
        Assert.Equal(2, aligned.Line);
        Assert.All(aligned, block => Assert.Same(aligned, block.Parent));
    }

    [Fact]
    public void Divs_nest()
    {
        var blocks = Blocks("""
            <div align="center">

            <div align="right">

            x

            </div>

            <div>

            y

            </div>

            z

            </div>
            """);

        Assert.Equal(["Center[Right[P(x)], P(y), P(z)]"], blocks);
    }

    [Fact]
    public void A_div_without_alignment_adds_no_block()
    {
        Assert.Equal(["P(x)", "P(y)"], Blocks("<div>\n\nx\n\n</div>\n\ny"));
    }

    [Fact]
    public void Html_inside_a_div_spread_over_blocks_belongs_to_it()
    {
        var blocks = Blocks("""
            <div align="center">
            <img src="logo.png">

            Text

            <p>Closing</p>
            </div>
            <p>Outside</p>
            """);

        Assert.Equal(["Center[P(Img(logo.png||)), P(Text), P(Closing)]", "P(Outside)"], blocks);
    }

    [Theory]
    [InlineData("<div align=\"center\">\n\nx")]
    [InlineData("x\n\n</div>")]
    [InlineData("<div align=\"center\">\n\n- x\n\n  </div>")]
    public void A_div_without_its_match_among_its_siblings_is_dropped(string markdown)
    {
        Assert.DoesNotContain(Blocks(markdown), block => block.StartsWith("Center", StringComparison.Ordinal));
    }

    [Fact]
    public void A_div_inside_a_list_item_aligns_its_markdown()
    {
        var item = Assert.IsType<ListItemBlock>(Assert.IsType<ListBlock>(Assert.Single(Parse("- <div align=\"right\">\n\n  x\n\n  </div>"))).Single());

        Assert.Equal(["Right[P(x)]"], item.Select(Describe));
    }

    // ── Details blocks ────────────────────────────────────────────────────────

    public static TheoryData<string> DetailsPipelines => ["details-first", "subset-first", "github"];

    private static MarkdownPipeline DetailsPipeline(string order) => order switch
    {
        "details-first" => new MarkdownPipelineBuilder().UseHtmlDetails().UseHtmlSubset().Build(),
        "subset-first" => new MarkdownPipelineBuilder().UseHtmlSubset().UseHtmlDetails().Build(),
        _ => new MarkdownPipelineBuilder().UseGitHubExtensions().Build(),
    };

    [Theory]
    [MemberData(nameof(DetailsPipelines))]
    public void Details_are_grouped_first_and_keep_their_html(string order)
    {
        var document = Parse("""
            <details>
            <summary><b>Title</b> <kbd>K</kbd></summary>
            <p align="center"><img src="x.png"></p>

            Body

            </details>
            """, DetailsPipeline(order));

        var details = Assert.IsType<DetailsBlock>(Assert.Single(document));
        Assert.Equal("Title K", details.Summary);
        Assert.Equal("Bold(Title) Keyboard(K)", Describe(details.SummaryInline));
        Assert.Equal(["Center[P(Img(x.png||))]", "P(Body)"], details.Select(Describe));
    }

    [Fact]
    public void A_summary_that_shows_nothing_has_no_inlines()
    {
        var details = Assert.IsType<DetailsBlock>(Assert.Single(Parse("<details><summary><!-- x --></summary>\n</details>", DetailsPipeline("github"))));

        Assert.Null(details.SummaryInline);
    }

    [Fact]
    public void Details_without_the_subset_keep_plain_text()
    {
        var details = Assert.IsType<DetailsBlock>(Assert.Single(Parse(
            "<details><summary><b>S</b></summary><p>a <b>b</b></p></details>",
            new MarkdownPipelineBuilder().UseHtmlDetails().Build())));

        Assert.Null(details.SummaryInline);
        Assert.Equal("<b>S</b>", details.SummaryHtml);
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(details));
        Assert.Equal("a b", Describe(paragraph.Inline));
    }

    [Fact]
    public void A_div_aligns_details_inside_it()
    {
        var blocks = Blocks("""
            <div align="center">

            <details>
            <summary>S</summary>

            B

            </details>

            </div>
            """, DetailsPipeline("github"));

        Assert.Equal(["Center[Details[P(B)]]"], blocks);
    }

    [Fact]
    public void Without_details_the_subset_shows_their_text()
    {
        Assert.Equal(["P(S)", "P(Body)"], Blocks("<details>\n<summary>S</summary>\n\nBody\n\n</details>").Select(Visible));
    }

    // ── Tags ──────────────────────────────────────────────────────────────────

    [Fact]
    public void A_tag_keeps_only_the_attributes_of_the_subset()
    {
        var tag = HtmlTag.Parse("<IMG SRC=\"a.png\" onerror=\"alert(1)\" style=\"x\" alt=b Width='3' height title=\"&lt;t&gt;\" align=\"left\" href=\"h\">")!;

        Assert.Equal("img", tag.Name);
        Assert.False(tag.IsEnd);
        Assert.False(tag.IsSelfClosing);
        Assert.Equal("a.png", tag.Attribute("src"));
        Assert.Equal("b", tag.Attribute("alt"));
        Assert.Equal("3", tag.Attribute("width"));
        Assert.Equal(string.Empty, tag.Attribute("height"));
        Assert.Equal("<t>", tag.Attribute("title"));
        Assert.Equal("left", tag.Attribute("align"));
        Assert.Equal("h", tag.Attribute("href"));
        Assert.Null(tag.Attribute("onerror"));
        Assert.Null(tag.Attribute("style"));
    }

    [Fact]
    public void The_first_of_two_attributes_with_the_same_name_wins()
    {
        Assert.Equal("a", HtmlTag.Parse("<a href=\"a\" HREF=\"b\">")!.Attribute("href"));
    }

    [Fact]
    public void A_quoted_value_can_hold_a_tag_end()
    {
        var tag = HtmlTag.Parse("<a title='x > y' href=\"u\">")!;

        Assert.Equal("x > y", tag.Attribute("title"));
        Assert.Equal("u", tag.Attribute("href"));
    }

    [Theory]
    [InlineData("</b>", "b", true, false)]
    [InlineData("<br/>", "br", false, true)]
    [InlineData("<br >", "br", false, false)]
    [InlineData("<h1  >", "h1", false, false)]
    public void A_tag_is_parsed_with_its_kind(string html, string name, bool isEnd, bool isSelfClosing)
    {
        var tag = HtmlTag.Parse(html)!;

        Assert.Equal((name, isEnd, isSelfClosing), (tag.Name, tag.IsEnd, tag.IsSelfClosing));
    }

    [Theory]
    [InlineData("<!-- x -->")]
    [InlineData("<?php x ?>")]
    [InlineData("<b")]
    [InlineData("x<b>")]
    [InlineData("<b>x")]
    [InlineData("< b>")]
    public void Anything_but_a_whole_tag_is_not_a_tag(string html)
    {
        Assert.Null(HtmlTag.Parse(html));
    }

    [Fact]
    public void Html_is_split_into_tags_and_text_without_comments()
    {
        var tokens = HtmlToken.Tokenize("a<b>c<!-- d -->e</b>");

        Assert.Equal(["a", "<b>", "c", "e", "</b>"], tokens.Select(t => t.Raw));
        Assert.Equal([false, true, false, false, true], tokens.Select(t => t.Tag is not null));
    }

    /// <summary>A block description without the HTML tags left for the renderer to drop, which do not show.</summary>
    private static string Visible(string description) =>
        System.Text.RegularExpressions.Regex.Replace(description, "{[^}]*}", string.Empty);
}
