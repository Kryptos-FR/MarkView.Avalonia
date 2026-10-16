// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Math.Parsing;
using MarkView.Avalonia.Mermaid;
using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class BracketMathTests
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseMathematics().UseMathBracketDelimiters().Build();

    private static StackPanel Render(string markdown, MarkdownPipeline? pipeline = null)
    {
        pipeline ??= Pipeline;
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer();
        new MathExtension().Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    private static MarkdownSelectableTextBlock Paragraph(StackPanel root) =>
        Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children));

    private static string Text(MarkdownSelectableTextBlock textBlock) =>
        string.Concat(textBlock.Inlines!.OfType<Run>().Select(r => r.Text));

    private static Image InlineFormula(MarkdownSelectableTextBlock textBlock) =>
        Assert.IsType<Image>(textBlock.Inlines!.OfType<InlineUIContainer>().Single().Child);

    private static void AssertFormulaBlock(Control control)
    {
        var border = Assert.IsType<Border>(control);
        Assert.Contains("markdown-math-block", border.Classes);
        Assert.NotNull(Assert.IsType<Image>(border.Child).Source);
    }

    private static readonly MarkdownPipeline PrecisePipeline =
        new MarkdownPipelineBuilder().UsePreciseSourceLocation().UseMathBracketDelimiters().Build();

    private static MarkdownDocument Parse(string markdown) => Markdown.Parse(markdown, Pipeline);

    private static BracketMathInline SingleFormula(string markdown) => SingleFormulaIn(Parse(markdown));

    private static BracketMathInline SingleFormulaIn(MarkdownDocument document) =>
        Assert.Single(document.Descendants<BracketMathInline>());

    // ---- \( ... \) ----

    [AvaloniaFact]
    public void Parenthesis_formula_renders_as_an_inline_image_inside_its_paragraph()
    {
        var paragraph = Paragraph(Render(@"Area \(\pi r^2\) of a disc"));

        var image = InlineFormula(paragraph);
        Assert.Contains("markdown-math-inline", image.Classes);
        Assert.NotNull(image.Source);
        Assert.Equal("Area  of a disc", Text(paragraph));
    }

    [Fact]
    public void Parenthesis_formula_content_excludes_the_delimiters_and_surrounding_spaces()
    {
        var formula = SingleFormulaIn(Markdown.Parse(@"a \(  x^2 + 1 \) b", PrecisePipeline));

        Assert.Equal("x^2 + 1", formula.Content.ToString());
        Assert.False(formula.IsDisplay);
        Assert.Equal(2, formula.Span.Start);
        Assert.Equal(15, formula.Span.End);
        Assert.Equal(0, formula.Line);
        Assert.Equal(2, formula.Column);
    }

    [Fact]
    public void Formula_on_a_later_line_reports_its_own_line_and_column()
    {
        var formula = SingleFormulaIn(Markdown.Parse("first line\nthen \\(x\\)", PrecisePipeline));

        Assert.Equal(1, formula.Line);
        Assert.Equal(5, formula.Column);
        Assert.Equal(16, formula.Span.Start);
        Assert.Equal(20, formula.Span.End);
    }

    [Fact]
    public void Text_after_a_formula_is_parsed_from_the_character_after_the_closing_delimiter()
    {
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(Parse(@"\(x\)*y*")));

        var inlines = paragraph.Inline!.ToList();
        Assert.Equal(2, inlines.Count);
        Assert.IsType<BracketMathInline>(inlines[0]);
        Assert.IsType<EmphasisInline>(inlines[1]);
    }

    [Fact]
    public void Parenthesis_formula_does_not_span_lines()
    {
        Assert.Empty(Parse("\\(a\nb\\)").Descendants<BracketMathInline>());
    }

    [Fact]
    public void Backslash_before_a_line_break_does_not_let_a_parenthesis_formula_span_lines()
    {
        Assert.Empty(Parse("\\(a\\\nb\\)").Descendants<BracketMathInline>());
    }

    [Fact]
    public void TeX_control_symbols_do_not_close_the_formula()
    {
        // "\\" is TeX's line break: the ")" after it is part of the formula, not the closing "\)".
        Assert.Equal(@"a \\) b", SingleFormula(@"\(a \\) b\)").Content.ToString());
    }

    [Fact]
    public void Closing_delimiter_of_the_other_kind_does_not_close_the_formula()
    {
        Assert.Equal(@"a \] b", SingleFormula(@"\(a \] b\)").Content.ToString());
        Assert.Equal(@"a \) b", SingleFormula(@"x \[a \) b\] y").Content.ToString());
    }

    [Fact]
    public void Closing_delimiter_just_before_the_opening_one_is_not_taken_for_its_end()
    {
        Assert.Equal("a", SingleFormula(@"\)\(a\)").Content.ToString());
    }

    [AvaloniaTheory]
    [InlineData(@"a \(b", "a (b")]
    [InlineData(@"a \(b\", @"a (b\")]
    [InlineData(@"a \[b", "a [b")]
    [InlineData(@"a \(\) b", "a () b")]
    [InlineData(@"a \( \) b", "a ( ) b")]
    [InlineData(@"a \[ \] b", "a [ ] b")]
    [InlineData(@"arr\[i\]", "arr[i]")]
    [InlineData(@"f\(x\)", "f(x)")]
    [InlineData(@"2\(x\)", "2(x)")]
    [InlineData(@"\\(x\)", @"\(x)")]
    [InlineData(@"\{x\}", "{x}")]
    public void Openings_that_do_not_start_a_formula_keep_their_CommonMark_meaning(string markdown, string text)
    {
        var paragraph = Paragraph(Render(markdown));

        Assert.Empty(paragraph.Inlines!.OfType<InlineUIContainer>());
        Assert.Equal(text, Text(paragraph));
    }

    [AvaloniaFact]
    public void Opening_after_punctuation_starts_a_formula()
    {
        Assert.NotNull(InlineFormula(Paragraph(Render(@"(\(x\))"))).Source);
    }

    [AvaloniaFact]
    public void Formula_at_the_start_of_a_paragraph_is_recognised()
    {
        Assert.NotNull(InlineFormula(Paragraph(Render(@"\(x\) is a variable"))).Source);
    }

    [AvaloniaFact]
    public void Code_spans_keep_bracket_delimiters_as_code()
    {
        var paragraph = Paragraph(Render(@"Write `\(x\)` or `\[x\]`"));

        Assert.DoesNotContain(paragraph.Inlines!.OfType<InlineUIContainer>(), c => c.Child is Image);
    }

    [Fact]
    public void Code_blocks_keep_bracket_delimiters_as_code()
    {
        var document = Parse("```\n\\(x\\)\n\\[\ny\n\\]\n```\n\n    \\[z\\]");

        Assert.Empty(document.Descendants<BracketMathInline>());
        Assert.Empty(document.Descendants<BracketMathBlock>());
        Assert.Equal(2, document.Descendants<CodeBlock>().Count());
    }

    [Fact]
    public void Formula_content_is_not_parsed_as_markdown()
    {
        var formula = SingleFormula(@"\(a*b*c_d_e\)");

        Assert.Equal("a*b*c_d_e", formula.Content.ToString());
    }

    // ---- \[ ... \] ----

    [AvaloniaFact]
    public void Square_bracket_formula_alone_in_its_paragraph_renders_as_a_block()
    {
        AssertFormulaBlock(Assert.Single(Render(@"\[x^2\]").Children));
    }

    [AvaloniaFact]
    public void Square_bracket_formula_on_several_lines_renders_as_one_block()
    {
        const string markdown = """
            \[
            \begin{aligned}
            a &= b \\
              &= c
            \end{aligned}
            \]
            """;

        AssertFormulaBlock(Assert.Single(Render(markdown).Children));
        var block = Assert.Single(Parse(markdown).Descendants<BracketMathBlock>());
        Assert.Equal("\\begin{aligned}\na &= b \\\\\n&= c\n\\end{aligned}", block.Lines.ToString());
    }

    [AvaloniaFact]
    public void Square_bracket_formula_on_its_own_lines_splits_the_surrounding_text_into_paragraphs()
    {
        var root = Render("Text before\n\\[x^2\\]\ntext after");

        Assert.Equal(3, root.Children.Count);
        Assert.Equal("Text before", Text(Assert.IsType<MarkdownSelectableTextBlock>(root.Children[0])));
        AssertFormulaBlock(root.Children[1]);
        Assert.Equal("text after", Text(Assert.IsType<MarkdownSelectableTextBlock>(root.Children[2])));
    }

    [Fact]
    public void Text_before_a_formula_keeps_its_paragraph_and_the_blocks_after_it_get_their_lines()
    {
        var document = Parse("Text\non lines\n\\[\nx\n\\]\n*More*\n\\[y\\]");
        var blocks = document.ToList();

        Assert.Equal(4, blocks.Count);
        var before = Assert.IsType<ParagraphBlock>(blocks[0]);
        Assert.Equal((0, 33, 0), (before.Span.Start, before.Span.End, before.Line));
        Assert.Equal(3, before.Inline!.Count()); // "Text", line break, "on lines"
        Assert.Equal(2, Assert.IsType<BracketMathBlock>(blocks[1]).Line);
        var after = Assert.IsType<ParagraphBlock>(blocks[2]);
        Assert.Equal(5, after.Line);
        Assert.IsType<EmphasisInline>(Assert.Single(after.Inline!));
        Assert.Equal(6, Assert.IsType<BracketMathBlock>(blocks[3]).Line);
    }

    [Fact]
    public void With_precise_source_locations_the_split_blocks_also_get_their_spans_and_columns()
    {
        var blocks = Markdown.Parse("> Text\n> \\[x\\]\n> *More*", PrecisePipeline).Descendants<LeafBlock>().ToList();

        Assert.Equal(3, blocks.Count);
        var block = Assert.IsType<BracketMathBlock>(blocks[1]);
        Assert.Equal((9, 13, 1, 2), (block.Span.Start, block.Span.End, block.Line, block.Column));
        Assert.Equal("x", block.Lines.ToString());
        var after = Assert.IsType<ParagraphBlock>(blocks[2]);
        Assert.Equal((17, 22, 2, 2), (after.Span.Start, after.Span.End, after.Line, after.Column));
    }

    [Fact]
    public void Formula_opening_its_paragraph_replaces_it_and_the_text_after_it_gets_a_paragraph_of_its_own()
    {
        var blocks = Parse("\\[x\\]\ntext").ToList();

        Assert.Equal(2, blocks.Count);
        Assert.Equal(0, Assert.IsType<BracketMathBlock>(blocks[0]).Line);
        var after = Assert.IsType<ParagraphBlock>(blocks[1]);
        Assert.Equal(1, after.Line);
        Assert.Equal("text", Assert.IsType<LiteralInline>(Assert.Single(after.Inline!)).Content.ToString());
    }

    [Fact]
    public void Task_item_text_before_a_formula_keeps_the_position_its_checkbox_is_toggled_through()
    {
        var pipeline = new MarkdownPipelineBuilder().UseTaskLists().UseMathBracketDelimiters().Build();

        var item = Assert.IsType<ListItemBlock>(Assert.Single(Markdown.Parse("- [ ] Prove\n  \\[x\\]", pipeline).Descendants<ListItemBlock>()));

        Assert.Equal(2, item.Count);
        var paragraph = Assert.IsType<ParagraphBlock>(item[0]);
        Assert.Equal((2, 0), (paragraph.Span.Start, paragraph.Line));
        Assert.IsType<Markdig.Extensions.TaskLists.TaskList>(paragraph.Inline!.FirstChild);
        Assert.IsType<BracketMathBlock>(item[1]);
    }

    [AvaloniaFact]
    public void Consecutive_square_bracket_formulas_render_as_consecutive_blocks()
    {
        var root = Render("\\[a\\]\n\\[b\\]\ntext\n\\[c\\]");

        Assert.Equal(4, root.Children.Count);
        AssertFormulaBlock(root.Children[0]);
        AssertFormulaBlock(root.Children[1]);
        Assert.Equal("text", Text(Assert.IsType<MarkdownSelectableTextBlock>(root.Children[2])));
        AssertFormulaBlock(root.Children[3]);
    }

    [AvaloniaTheory]
    [InlineData("Text  \n\\[x\\]\nmore")]   // hard line break before
    [InlineData("Text\\\n\\[x\\]\\\nmore")] // backslash hard line breaks
    [InlineData("Text \n \\[x\\] \nmore")]   // single trailing and leading spaces
    [InlineData("Text\n\\[x\\]   \nmore")]   // hard line break after
    public void Hard_line_breaks_around_a_square_bracket_formula_also_set_it_apart(string markdown)
    {
        var root = Render(markdown);

        Assert.Equal(3, root.Children.Count);
        Assert.Equal("Text", Text(Assert.IsType<MarkdownSelectableTextBlock>(root.Children[0])));
        AssertFormulaBlock(root.Children[1]);
        Assert.Equal("more", Text(Assert.IsType<MarkdownSelectableTextBlock>(root.Children[2])));
    }

    [AvaloniaFact]
    public void Soft_line_breaks_rendered_as_hard_ones_also_set_a_formula_apart()
    {
        var pipeline = new MarkdownPipelineBuilder().UseSoftlineBreakAsHardlineBreak().UseMathBracketDelimiters().Build();

        var root = Render("Text\n\\[x\\]\nmore", pipeline);

        Assert.Equal(3, root.Children.Count);
        AssertFormulaBlock(root.Children[1]);
    }

    [AvaloniaTheory]
    [InlineData(@"a \[x\] b")]
    [InlineData("a \\[x\\]\nb")]
    [InlineData("a\n\\[x\\] b")]
    public void Square_bracket_formula_sharing_a_line_with_text_stays_inline(string markdown)
    {
        var paragraph = Paragraph(Render(markdown));

        Assert.NotNull(InlineFormula(paragraph).Source);
        Assert.Equal("a  b", Text(paragraph));
    }

    [Fact]
    public void Square_bracket_formula_inside_emphasis_stays_inline()
    {
        var document = Parse(@"*\[x\]*");

        Assert.IsType<EmphasisInline>(SingleFormulaIn(document).Parent);
        Assert.Empty(document.Descendants<BracketMathBlock>());
    }

    [Fact]
    public void Square_bracket_formula_may_span_lines_inside_a_paragraph()
    {
        var formula = SingleFormula("a \\[x +\ny\\] b");

        Assert.True(formula.IsDisplay);
        Assert.Equal("x +\ny", formula.Content.ToString());
    }

    [AvaloniaFact]
    public void Square_bracket_formula_in_a_blockquote_renders_as_a_block_inside_it()
    {
        var quote = Assert.IsType<Border>(Assert.Single(Render("> Note\n> \\[x\\]").Children));
        var panel = Assert.IsType<StackPanel>(quote.Child);

        Assert.Equal(2, panel.Children.Count);
        AssertFormulaBlock(panel.Children[1]);
    }

    [Fact]
    public void Promoted_block_replaces_the_paragraph_in_its_own_container()
    {
        var document = Parse("- item\n\n  \\[x\\]\n- other");

        var block = Assert.Single(document.Descendants<BracketMathBlock>());
        Assert.IsType<ListItemBlock>(block.Parent);
        Assert.Equal(1, block.Parent!.IndexOf(block));
    }

    // ---- pipeline ----

    [AvaloniaFact]
    public void Without_the_bracket_delimiters_extension_backslash_parentheses_are_escapes()
    {
        var pipeline = new MarkdownPipelineBuilder().UseMathematics().Build();

        var root = Render("\\(x\\) and\n\n\\[y\\]", pipeline);

        Assert.Equal("(x) and", Text(Assert.IsType<MarkdownSelectableTextBlock>(root.Children[0])));
        Assert.Equal("[y]", Text(Assert.IsType<MarkdownSelectableTextBlock>(root.Children[1])));
    }

    [AvaloniaFact]
    public void Bracket_delimiters_work_without_dollar_math()
    {
        var pipeline = new MarkdownPipelineBuilder().UseMathBracketDelimiters().Build();

        var paragraph = Paragraph(Render(@"Costs $5, or \(x\) dollars", pipeline));

        Assert.NotNull(InlineFormula(paragraph).Source);
        Assert.Equal("Costs $5, or  dollars", Text(paragraph));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bracket_and_dollar_delimiters_combine_in_either_order(bool bracketsFirst)
    {
        var builder = new MarkdownPipelineBuilder();
        if (bracketsFirst)
            builder.UseMathBracketDelimiters().UseMathematics();
        else
            builder.UseMathematics().UseMathBracketDelimiters();

        var root = Render("$a$ and \\(b\\)\n\n$$\nc\n$$\n\n\\[d\\]", builder.Build());

        Assert.Equal(2, Assert.IsType<MarkdownSelectableTextBlock>(root.Children[0]).Inlines!.OfType<InlineUIContainer>().Count());
        AssertFormulaBlock(root.Children[1]);
        AssertFormulaBlock(root.Children[2]);
    }

    [AvaloniaFact]
    public void Enabling_bracket_delimiters_twice_adds_them_once()
    {
        var builder = new MarkdownPipelineBuilder().UseMathBracketDelimiters();

        Assert.Same(builder, builder.UseMathBracketDelimiters());
        Assert.Single(builder.Extensions.OfType<BracketMathExtension>());
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bracket_formula_blocks_render_as_math_whatever_the_Mermaid_registration_order(bool mathFirst)
    {
        var renderer = new AvaloniaRenderer();
        if (mathFirst)
        {
            new MathExtension().Register(renderer);
            new MermaidExtension().Register(renderer);
        }
        else
        {
            new MermaidExtension().Register(renderer);
            new MathExtension().Register(renderer);
        }
        var document = Markdown.Parse(@"\[x^2\]", Pipeline);
        Pipeline.Setup(renderer);
        renderer.Render(document);

        AssertFormulaBlock(Assert.Single(renderer.RootPanel.Children));
    }

    [AvaloniaFact]
    public void Bracket_formula_block_that_fails_to_render_falls_back_to_its_source()
    {
        var nested = new string('{', 60) + "x" + new string('}', 60);

        var border = Assert.IsType<Border>(Assert.Single(Render($"\\[{nested}\\]").Children));

        Assert.Contains("markdown-math-fallback", border.Classes);
    }
}
