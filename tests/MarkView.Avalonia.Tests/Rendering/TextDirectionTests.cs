// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;

using Markdig;
using Markdig.Syntax;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// Which direction <see cref="TextDirection"/> finds in a block's text. Builds no Avalonia object
/// (<see cref="FlowDirection"/> is an enum and the Unicode data are static), so plain facts are safe.
/// </summary>
public class TextDirectionTests
{
    private static Block FirstBlock(string markdown, MarkdownPipeline? pipeline = null) =>
        Markdown.Parse(markdown, pipeline ?? new MarkdownPipelineBuilder().UseGitHubExtensions().Build())[0];

    private static FlowDirection? Detect(string markdown) => TextDirection.Detect(FirstBlock(markdown));

    [Theory]
    [InlineData("שלום עולם")]
    [InlineData("مرحبا بالعالم")]
    [InlineData("ܫܠܡܐ")] // Syriac, an Arabic-letter script too
    [InlineData("123, 456! שלום")]
    public void Hebrew_or_Arabic_text_reads_right_to_left(string markdown) =>
        Assert.Equal(FlowDirection.RightToLeft, Detect(markdown));

    [Theory]
    [InlineData("Hello")]
    [InlineData("Привет")]
    [InlineData("日本語")]
    [InlineData("42. Hello שלום")]
    public void Latin_Cyrillic_or_CJK_text_reads_left_to_right(string markdown) =>
        Assert.Equal(FlowDirection.LeftToRight, Detect(markdown));

    [Theory]
    [InlineData("123 456")]
    [InlineData("!?… 😀")]
    [InlineData("---\n\n")]
    public void Text_without_a_letter_has_no_direction(string markdown) =>
        Assert.Null(TextDirection.Detect(markdown.AsSpan()));

    [Fact]
    public void A_paragraph_of_digits_has_no_direction() => Assert.Null(Detect("2026-10-18"));

    [Fact]
    public void The_first_strong_character_decides_wherever_it_is_in_the_emphasis() =>
        Assert.Equal(FlowDirection.RightToLeft, Detect("**_שלום_** world"));

    [Fact]
    public void A_link_s_text_counts() => Assert.Equal(FlowDirection.RightToLeft, Detect("[שלום](https://example.com) world"));

    [Theory]
    [InlineData("`npm install` מתקין את החבילות")]
    [InlineData("<https://example.com> הוא האתר")]
    [InlineData("![logo](logo.png) שלום")]
    [InlineData("<b>שלום</b> world")]
    public void Code_spans_autolinks_images_and_tags_are_skipped(string markdown) =>
        Assert.Equal(FlowDirection.RightToLeft, Detect(markdown));

    [Fact]
    public void Math_is_skipped() =>
        Assert.Equal(FlowDirection.RightToLeft, TextDirection.Detect(FirstBlock("$x + y$ שווה", new MarkdownPipelineBuilder().UseMathematics().Build())));

    [Fact]
    public void A_character_reference_counts() => Assert.Equal(FlowDirection.RightToLeft, Detect("&#x5E9;lom"));

    [Fact]
    public void Text_inside_a_directional_isolate_is_skipped() =>
        Assert.Equal(FlowDirection.RightToLeft, TextDirection.Detect("\u2066abc\u2069 שלום".AsSpan()));

    [Fact]
    public void Nested_isolates_are_skipped_until_the_outer_one_ends() =>
        Assert.Equal(FlowDirection.LeftToRight, TextDirection.Detect("\u2067\u2068א\u2069ב\u2069 abc".AsSpan()));

    [Fact]
    public void An_unmatched_pop_isolate_is_ignored() =>
        Assert.Equal(FlowDirection.RightToLeft, TextDirection.Detect("\u2069 שלום".AsSpan()));

    [Fact]
    public void A_heading_has_the_direction_of_its_text() => Assert.Equal(FlowDirection.RightToLeft, Detect("# כותרת"));

    [Fact]
    public void A_code_block_has_no_direction_of_its_own() => Assert.Null(Detect("```\nשלום\n```"));

    [Fact]
    public void A_container_takes_the_first_direction_among_its_blocks()
    {
        Assert.Equal(FlowDirection.RightToLeft, Detect("> 123\n>\n> ```\n> code\n> ```\n>\n> שלום\n>\n> Hello"));
        Assert.Equal(FlowDirection.RightToLeft, Detect("- 1\n- שלום\n- Hello"));
        Assert.Equal(FlowDirection.LeftToRight, Detect("| 1 | Hello |\n|---|---|\n| שלום | x |"));
    }

    [Fact]
    public void A_container_of_neutral_blocks_has_no_direction() => Assert.Null(Detect("> 1\n>\n> 2"));

    [Fact]
    public void A_container_skips_the_blocks_an_html_dir_attribute_sets()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();
        var quote = Assert.IsType<QuoteBlock>(FirstBlock("> <p dir=\"ltr\">English</p>\n>\n> שלום", pipeline));

        Assert.Equal(FlowDirection.LeftToRight, TextDirection.Explicit(quote[0]));
        Assert.Equal(FlowDirection.RightToLeft, TextDirection.Detect(quote));
    }

    [Fact]
    public void A_details_element_reads_its_summary_first()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();
        Assert.Equal(FlowDirection.RightToLeft, TextDirection.Detect(FirstBlock("<details>\n<summary>פרטים</summary>\n\nHello\n\n</details>", pipeline)));
        Assert.Equal(FlowDirection.LeftToRight, TextDirection.Detect(FirstBlock("<details>\n<summary>123</summary>\n\nHello\n\n</details>", pipeline)));
    }

    [Fact]
    public void A_details_element_without_the_html_subset_reads_its_plain_summary()
    {
        var pipeline = new MarkdownPipelineBuilder().UseHtmlDetails().Build();
        Assert.Equal(FlowDirection.RightToLeft, TextDirection.Detect(FirstBlock("<details>\n<summary>פרטים</summary>\n\nHello\n\n</details>", pipeline)));
    }

    [Fact]
    public void Code_blocks_are_always_left_to_right()
    {
        Assert.Equal(FlowDirection.LeftToRight, TextDirection.Of(FirstBlock("```\nשלום\n```"), detect: true));
        Assert.Equal(FlowDirection.LeftToRight, TextDirection.Of(FirstBlock("```\nשלום\n```"), detect: false));
    }

    [Fact]
    public void Without_detection_only_code_and_dir_attributes_give_a_direction()
    {
        Assert.Null(TextDirection.Of(FirstBlock("שלום"), detect: false));
        Assert.Equal(FlowDirection.RightToLeft, TextDirection.Of(FirstBlock("<p dir=\"rtl\">Hello</p>"), detect: false));
    }

    [Fact]
    public void A_dir_attribute_wins_over_the_text() =>
        Assert.Equal(FlowDirection.LeftToRight, TextDirection.Of(FirstBlock("<p dir=\"ltr\">שלום</p>"), detect: true));

    [Fact]
    public void A_neutral_paragraph_resolves_to_the_direction_of_the_block_around_it()
    {
        var list = Assert.IsType<ListBlock>(FirstBlock("- `x()`\n- שלום"));
        var paragraph = (ParagraphBlock)((ListItemBlock)list[0])[0];

        Assert.Null(TextDirection.Of(paragraph, detect: true));
        Assert.Equal(FlowDirection.RightToLeft, TextDirection.Resolve(paragraph, detect: true));
        Assert.Null(TextDirection.Resolve(paragraph, detect: false));
    }

    [Fact]
    public void A_neutral_table_cell_resolves_to_the_direction_of_the_table()
    {
        var table = FirstBlock("| `x` | שלום |\n|---|---|");
        var paragraph = table.Descendants<ParagraphBlock>().First();

        Assert.Equal(FlowDirection.RightToLeft, TextDirection.Resolve(paragraph, detect: true));
    }

    [Fact]
    public void A_neutral_top_level_paragraph_resolves_to_nothing() => Assert.Null(TextDirection.Resolve(FirstBlock("`x()`"), detect: true));
}
