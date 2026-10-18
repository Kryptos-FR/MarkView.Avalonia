// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>The direction <see cref="AvaloniaRenderer"/> gives the controls of each block.</summary>
public class TextDirectionRenderingTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();

    private static StackPanel Render(string markdown, bool detect = true, MarkdownPipeline? pipeline = null)
    {
        pipeline ??= Pipeline;
        var renderer = new AvaloniaRenderer { DetectTextDirection = detect };
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    private static FlowDirection? LocalDirection(Visual visual) =>
        visual.IsSet(Visual.FlowDirectionProperty) ? visual.FlowDirection : null;

    [AvaloniaFact]
    public void Detection_is_on_by_default() => Assert.True(new AvaloniaRenderer().DetectTextDirection);

    [AvaloniaFact]
    public void Each_paragraph_takes_the_direction_of_its_text()
    {
        var root = Render("שלום עולם\n\nHello world\n\n123");

        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(root.Children[0]));
        Assert.Equal(FlowDirection.LeftToRight, LocalDirection(root.Children[1]));
        Assert.Null(LocalDirection(root.Children[2]));
    }

    [AvaloniaFact]
    public void A_heading_takes_the_direction_of_its_text()
    {
        var heading = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Render("## مرحبا").Children));

        Assert.Equal(FlowDirection.RightToLeft, heading.FlowDirection);
        // Start alignment: a right-to-left line starts on the right.
        Assert.Equal(TextAlignment.Start, heading.TextAlignment);
    }

    [AvaloniaFact]
    public void A_list_is_mirrored_as_a_whole_and_its_paragraphs_take_their_own_direction()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("- 1\n- שלום\n- Hello").Children));

        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(list));
        var items = list.Children.Cast<Grid>().ToArray();
        // The items follow the list, so their markers all sit on the same side.
        Assert.All(items, item => Assert.Null(LocalDirection(item)));
        Assert.All(items, item => Assert.Equal(FlowDirection.RightToLeft, item.FlowDirection));
        var paragraphs = items.Select(item => ((StackPanel)item.Children[1]).Children[0]).ToArray();
        Assert.Null(LocalDirection(paragraphs[0]));
        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(paragraphs[1]));
        Assert.Equal(FlowDirection.LeftToRight, LocalDirection(paragraphs[2]));
    }

    [AvaloniaFact]
    public void A_paragraph_that_reads_against_its_list_is_aligned_to_the_marker()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("- שלום\n- Hello\n- 123").Children));
        var paragraphs = list.Children.Cast<Grid>().Select(item => (TextBlock)((StackPanel)item.Children[1]).Children[0]).ToArray();

        Assert.False(paragraphs[0].IsSet(TextBlock.TextAlignmentProperty));
        // Left to right, so its end is on the right, where the marker of the right-to-left list is.
        Assert.Equal(TextAlignment.End, paragraphs[1].TextAlignment);
        Assert.False(paragraphs[2].IsSet(TextBlock.TextAlignmentProperty));
    }

    [AvaloniaFact]
    public void A_paragraph_that_reads_against_its_table_is_aligned_with_its_column()
    {
        var grid = Assert.IsType<Grid>(Assert.Single(Render("| שם | Name |\n|---|:--|\n| Hello | Hello |").Children));
        var paragraphs = grid.Children.Cast<Border>().Select(cell => (TextBlock)((StackPanel)cell.Child!).Children[0]).ToArray();

        Assert.Equal(TextAlignment.End, paragraphs[2].TextAlignment);
        // An aligned column places its cells itself.
        Assert.False(paragraphs[1].IsSet(TextBlock.TextAlignmentProperty));
        Assert.False(paragraphs[3].IsSet(TextBlock.TextAlignmentProperty));
    }

    [AvaloniaFact]
    public void A_paragraph_that_reads_with_its_list_or_outside_one_keeps_its_alignment()
    {
        var root = Render("- Hello\n- World\n\nשלום");
        var list = Assert.IsType<StackPanel>(root.Children[0]);

        Assert.All(list.Children.Cast<Grid>(), item => Assert.False(((StackPanel)item.Children[1]).Children[0].IsSet(TextBlock.TextAlignmentProperty)));
        Assert.False(root.Children[1].IsSet(TextBlock.TextAlignmentProperty));
    }

    [AvaloniaFact]
    public void A_nested_list_takes_its_own_direction()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("- שלום\n  - Hello").Children));
        var content = (StackPanel)((Grid)list.Children[0]).Children[1];

        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(list));
        Assert.Equal(FlowDirection.LeftToRight, LocalDirection(content.Children[1]));
    }

    [AvaloniaFact]
    public void A_quote_is_mirrored_and_its_code_block_stays_left_to_right()
    {
        var quote = Assert.IsType<Border>(Assert.Single(Render("> שלום\n>\n> ```\n> code\n> ```").Children));

        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(quote));
        var content = Assert.IsType<StackPanel>(quote.Child);
        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(content.Children[0]));
        Assert.Equal(FlowDirection.LeftToRight, LocalDirection(content.Children[1]));
    }

    [AvaloniaTheory]
    [InlineData("```\nשלום\n```")]
    [InlineData("    שלום")]
    public void A_code_block_is_left_to_right_whatever_its_text(string markdown)
    {
        Assert.Equal(FlowDirection.LeftToRight, LocalDirection(Assert.Single(Render(markdown).Children)));
        Assert.Equal(FlowDirection.LeftToRight, LocalDirection(Assert.Single(Render(markdown, detect: false).Children)));
    }

    [AvaloniaFact]
    public void An_alert_takes_the_direction_of_its_content()
    {
        var alert = Assert.Single(Render("> [!NOTE]\n> הערה חשובה").Children);

        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(alert));
    }

    [AvaloniaFact]
    public void A_right_to_left_table_is_mirrored()
    {
        var grid = Assert.IsType<Grid>(Assert.Single(Render("| שם | Name |\n|---|---|\n| 1 | 2 |").Children));

        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(grid));
        var cells = grid.Children.Cast<Border>().ToArray();
        var cellParagraphs = cells.Select(cell => ((StackPanel)cell.Child!).Children[0]).ToArray();
        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(cellParagraphs[0]));
        Assert.Equal(FlowDirection.LeftToRight, LocalDirection(cellParagraphs[1]));
        Assert.Null(LocalDirection(cellParagraphs[2]));
    }

    [AvaloniaTheory]
    [InlineData(":---", HorizontalAlignment.Right)]
    [InlineData("---:", HorizontalAlignment.Left)]
    [InlineData(":---:", HorizontalAlignment.Center)]
    public void A_right_to_left_table_keeps_its_aligned_columns_on_the_side_they_name(string separator, HorizontalAlignment mirrored)
    {
        // The grid is mirrored, so the alignment inside it is the opposite of the side the cell shows on.
        var grid = Assert.IsType<Grid>(Assert.Single(Render($"| שם |\n|{separator}|\n| א |").Children));

        Assert.All(grid.Children.Cast<Border>(), cell => Assert.Equal(mirrored, cell.Child!.HorizontalAlignment));
    }

    [AvaloniaTheory]
    [InlineData(":---", HorizontalAlignment.Left)]
    [InlineData("---:", HorizontalAlignment.Right)]
    public void A_left_to_right_table_aligns_its_columns_as_before(string separator, HorizontalAlignment alignment)
    {
        var grid = Assert.IsType<Grid>(Assert.Single(Render($"| Name |\n|{separator}|\n| a |").Children));

        Assert.All(grid.Children.Cast<Border>(), cell => Assert.Equal(alignment, cell.Child!.HorizontalAlignment));
    }

    [AvaloniaFact]
    public void Without_detection_blocks_keep_the_direction_around_them()
    {
        var root = Render("שלום\n\n- Hello\n\n| שם |\n|:--|\n| א |", detect: false);

        Assert.All(root.Children, child => Assert.Null(LocalDirection(child)));
        var grid = Assert.IsType<Grid>(root.Children[2]);
        Assert.All(grid.Children.Cast<Border>(), cell => Assert.Equal(HorizontalAlignment.Left, cell.Child!.HorizontalAlignment));
    }

    [AvaloniaFact]
    public void A_direction_a_renderer_chose_is_kept()
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        renderer.ObjectRenderers.Insert(0, new FixedDirectionParagraphRenderer());
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse("שלום", pipeline));

        Assert.Equal(FlowDirection.LeftToRight, Assert.Single(renderer.RootPanel.Children).FlowDirection);
    }

    private sealed class FixedDirectionParagraphRenderer : AvaloniaObjectRenderer<Markdig.Syntax.ParagraphBlock>
    {
        protected override void Write(AvaloniaRenderer renderer, Markdig.Syntax.ParagraphBlock obj) =>
            renderer.WriteBlock(new TextBlock { Text = "x", FlowDirection = FlowDirection.LeftToRight });
    }

    // ── HTML dir attribute ────────────────────────────────────────────────────

    [AvaloniaFact]
    public void A_div_with_dir_sets_the_direction_of_the_markdown_inside_it()
    {
        var root = Render("<div dir=\"rtl\">\n\nHello\n\n123\n\n</div>\n\nAfter");

        var panel = Assert.IsType<StackPanel>(root.Children[0]);
        Assert.Empty(panel.Classes);
        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(panel));
        // Markdown inside still reads its own text, as GitHub's dir="auto" paragraphs do.
        Assert.Equal(FlowDirection.LeftToRight, LocalDirection(panel.Children[0]));
        Assert.Null(LocalDirection(panel.Children[1]));
        Assert.Equal(FlowDirection.RightToLeft, panel.Children[1].FlowDirection);
        Assert.Equal(FlowDirection.LeftToRight, LocalDirection(root.Children[1]));
    }

    [AvaloniaFact]
    public void A_div_with_align_and_dir_does_both()
    {
        var panel = Assert.IsType<StackPanel>(Assert.Single(Render("<div align=\"center\" dir=\"rtl\">\n\n123\n\n</div>").Children));

        Assert.Equal(["markdown-html-align", "markdown-html-align-center"], panel.Classes);
        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(panel));
    }

    [AvaloniaTheory]
    [InlineData("<p dir=\"rtl\">Hello</p>", FlowDirection.RightToLeft)]
    [InlineData("<p dir=\"LTR\">שלום</p>", FlowDirection.LeftToRight)]
    [InlineData("<h2 dir=\"rtl\">Hello</h2>", FlowDirection.RightToLeft)]
    public void A_paragraph_or_heading_with_dir_takes_that_direction(string html, FlowDirection direction) =>
        Assert.Equal(direction, LocalDirection(Assert.Single(Render(html).Children)));

    [AvaloniaFact]
    public void An_aligned_paragraph_with_dir_gives_the_direction_to_the_paragraph()
    {
        var panel = Assert.IsType<StackPanel>(Assert.Single(Render("<p align=\"center\" dir=\"rtl\">Hello</p>").Children));

        Assert.Null(LocalDirection(panel));
        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(Assert.Single(panel.Children)));
    }

    [AvaloniaTheory]
    [InlineData("auto")]
    [InlineData("sideways")]
    public void Dir_auto_or_an_unknown_value_leaves_the_direction_to_the_text(string value)
    {
        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(Assert.Single(Render($"<p dir=\"{value}\">שלום</p>").Children)));
        Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Render($"<div dir=\"{value}\">\n\nשלום\n\n</div>").Children));
    }

    [AvaloniaFact]
    public void A_dir_attribute_applies_without_detection()
    {
        var root = Render("<div dir=\"rtl\">\n\nHello\n\n</div>", detect: false);

        var panel = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        Assert.Equal(FlowDirection.RightToLeft, LocalDirection(panel));
        Assert.Null(LocalDirection(panel.Children[0]));
    }

    // ── Inline code in right-to-left text ─────────────────────────────────────

    private static Inline[] Inlines(StackPanel root) =>
        [.. Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children)).Inlines!];

    [AvaloniaFact]
    public void Inline_code_in_right_to_left_text_is_marked()
    {
        var inlines = Inlines(Render("השתמשו ב-`foo()` כאן"));

        Assert.Equal(5, inlines.Length);
        Assert.Equal("\u200E", Assert.IsType<DirectionMark>(inlines[1]).Text);
        var code = Assert.IsType<Run>(inlines[2]);
        Assert.Equal("foo()", code.Text);
        Assert.Contains("markdown-code-inline", code.Classes);
        Assert.Equal("\u200E", Assert.IsType<DirectionMark>(inlines[3]).Text);
    }

    [AvaloniaFact]
    public void Inline_code_in_a_neutral_paragraph_of_a_right_to_left_list_is_marked()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("- `foo()`\n- שלום").Children));
        var paragraph = (MarkdownSelectableTextBlock)((StackPanel)((Grid)list.Children[0]).Children[1]).Children[0];

        Assert.Equal(3, paragraph.Inlines!.Count);
        Assert.IsType<DirectionMark>(paragraph.Inlines[0]);
    }

    [AvaloniaTheory]
    [InlineData("Use `foo()` here")]
    [InlineData("`foo()`")]
    public void Inline_code_in_left_to_right_or_neutral_text_is_not_marked(string markdown) =>
        Assert.DoesNotContain(Inlines(Render(markdown)), inline => inline is DirectionMark);

    [AvaloniaFact]
    public void Inline_code_is_not_marked_without_detection() =>
        Assert.DoesNotContain(Inlines(Render("השתמשו ב-`foo()` כאן", detect: false)), inline => inline is DirectionMark);

    [AvaloniaFact]
    public void Inline_code_in_a_paragraph_with_dir_rtl_is_marked_without_detection() =>
        Assert.Contains(Inlines(Render("<p dir=\"rtl\">Use <code>foo()</code></p>", detect: false)), inline => inline is DirectionMark);

    [AvaloniaFact]
    public void The_direction_marks_are_left_out_of_the_copied_text()
    {
        var text = MarkdownSelectableTextBlock.ExtractPlainText(new InlineCollection
        {
            new Run("א "),
            DirectionMark.LeftToRight(),
            new Run("foo()"),
            DirectionMark.LeftToRight(),
        }, out var embedded);

        Assert.Equal("א \u200Efoo()\u200E", text);
        Assert.Equal(new Dictionary<int, string> { [2] = "", [8] = "" }, embedded);
    }

    [AvaloniaFact]
    public void Each_character_of_right_to_left_text_is_hit_tested_in_its_own_place()
    {
        var heading = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Render("# שלום").Children));
        heading.Measure(new Size(400, double.PositiveInfinity));
        var layout = heading.TextLayout;

        var characters = Enumerable.Range(0, 4).Select(i => Assert.Single(layout.HitTestTextRange(i, 1))).ToList();
        Assert.All(characters, rect => Assert.True(rect.Width > 0));
        // Right to left: each character is left of the one before it.
        Assert.All(characters.Zip(characters.Skip(1)), pair => Assert.Equal(pair.First.Left, pair.Second.Right, 3));
        Assert.Equal(layout.WidthIncludingTrailingWhitespace, layout.HitTestTextRange(0, 4).Sum(rect => rect.Width), 3);
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(4)]
    public void Right_to_left_text_keeps_its_line_spacing(double lineSpacing)
    {
        var text = string.Join(" ", Enumerable.Repeat("שלום", 40));
        var markdownText = new MarkdownSelectableTextBlock { LineSpacing = lineSpacing, TextWrapping = TextWrapping.Wrap, Inlines = [new Run(text)] };
        var plainText = new TextBlock { LineSpacing = lineSpacing, TextWrapping = TextWrapping.Wrap, Inlines = [new Run(text)] };
        markdownText.Measure(new Size(200, double.PositiveInfinity));
        plainText.Measure(new Size(200, double.PositiveInfinity));

        Assert.True(markdownText.TextLayout.TextLines.Count > 1);
        Assert.Equal(plainText.DesiredSize, markdownText.DesiredSize);
    }

    [AvaloniaFact]
    public void Right_to_left_text_is_shaped_again_when_it_changes()
    {
        var run = new Run("שלום");
        var text = new MarkdownSelectableTextBlock { Inlines = [run] };
        text.Measure(new Size(400, double.PositiveInfinity));
        _ = text.TextLayout;

        run.Text = "עולם";
        text.Measure(new Size(400, double.PositiveInfinity));

        var shaped = text.TextLayout.TextLines.SelectMany(line => line.TextRuns).OfType<ShapedTextRun>();
        Assert.Equal("עולם", string.Concat(shaped.Select(shapedRun => shapedRun.Text.ToString())));
    }
}
