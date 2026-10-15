// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Parsers.Inlines;

using MarkView.Avalonia.Parsing;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

/// <summary>
/// Obsidian-style alert extras: fold markers (<c>[!KIND]-</c> / <c>[!KIND]+</c>) and custom titles.
/// </summary>
public class FoldableAlertTests : RenderTestBase
{
    private sealed record RenderedAlert(Border Border, TextBlock Header, StackPanel Content);

    private static MarkdownPipeline AlertPipeline() =>
        new MarkdownPipelineBuilder().UseAlertBlocks().Build();

    private static RenderedAlert RenderAlert(string markdown, MarkdownPipeline? pipeline = null)
    {
        var border = Assert.IsType<Border>(Assert.Single(Render(markdown, pipeline ?? AlertPipeline()).Children));
        var outer = Assert.IsType<StackPanel>(border.Child);
        Assert.Equal(2, outer.Children.Count);
        var header = Assert.IsType<TextBlock>(outer.Children[0]);
        Assert.Contains("markdown-alert-header", header.Classes);
        var content = Assert.IsType<StackPanel>(outer.Children[1]);
        Assert.Contains("markdown-alert-content", content.Classes);
        return new RenderedAlert(border, header, content);
    }

    private static string[] StyleClasses(StyledElement element) =>
        element.Classes.Where(c => !c.StartsWith(':')).ToArray();

    private static string HeaderText(TextBlock header) =>
        header.Inlines is { Count: > 0 } inlines ? InlineText(inlines) : header.Text ?? string.Empty;

    private static string InlineText(InlineCollection inlines) =>
        string.Concat(inlines.Select(i => i switch
        {
            Run run => run.Text,
            Span span => InlineText(span.Inlines),
            _ => string.Empty,
        }));

    private static string[] ParagraphTexts(Panel content) =>
        content.Children.OfType<MarkdownSelectableTextBlock>()
            .Select(tb => MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!))
            .ToArray();

    private static Border[] Alerts(Visual root) =>
        root.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("markdown-alert")).ToArray();

    [AvaloniaFact]
    public void Plain_alert_header_is_plain_text_and_not_foldable()
    {
        var alert = RenderAlert("> [!NOTE]\n> Content");

        Assert.Equal("NOTE", alert.Header.Text);
        Assert.Empty(alert.Header.Inlines!);
        Assert.False(alert.Header.Focusable);
        Assert.Equal(["markdown-alert", "markdown-alert-note"], StyleClasses(alert.Border));
        Assert.DoesNotContain(":expanded", alert.Border.Classes);
        Assert.Null(CollapsibleAlert.Get(alert.Border));
        Assert.True(alert.Content.IsVisible);
        Assert.Equal(["Content"], ParagraphTexts(alert.Content));
    }

    [AvaloniaTheory]
    [InlineData("-", false, CollapsibleAlert.CollapsedGlyph)]
    [InlineData("+", true, CollapsibleAlert.ExpandedGlyph)]
    public void Fold_marker_makes_the_alert_foldable_in_its_initial_state(string marker, bool expanded, string glyph)
    {
        var alert = RenderAlert($"> [!WARNING]{marker}\n> Watch out");

        Assert.Equal(["markdown-alert", "markdown-alert-warning", "markdown-alert-collapsible"], StyleClasses(alert.Border));
        Assert.Equal(expanded, alert.Border.Classes.Contains(":expanded"));
        Assert.Equal(expanded, alert.Content.IsVisible);
        Assert.True(alert.Header.Focusable);
        Assert.Equal($"{glyph} WARNING", HeaderText(alert.Header));
        var chevron = Assert.IsType<Run>(alert.Header.Inlines![0]);
        Assert.Equal(["markdown-alert-chevron"], chevron.Classes.ToArray());
        Assert.Equal(glyph, chevron.Text);
        Assert.Equal(["Watch out"], ParagraphTexts(alert.Content));
        Assert.Equal(expanded, CollapsibleAlert.Get(alert.Border)!.IsExpanded);
    }

    [AvaloniaFact]
    public void Toggle_shows_and_hides_the_body_and_updates_chevron_and_pseudo_class()
    {
        var alert = RenderAlert("> [!NOTE]-\n> Body");
        var state = CollapsibleAlert.Get(alert.Border)!;
        var chevron = Assert.IsType<Run>(alert.Header.Inlines![0]);

        state.Toggle();

        Assert.True(state.IsExpanded);
        Assert.True(alert.Content.IsVisible);
        Assert.Contains(":expanded", alert.Border.Classes);
        Assert.Equal(CollapsibleAlert.ExpandedGlyph, chevron.Text);

        state.Toggle();

        Assert.False(state.IsExpanded);
        Assert.False(alert.Content.IsVisible);
        Assert.DoesNotContain(":expanded", alert.Border.Classes);
        Assert.Equal(CollapsibleAlert.CollapsedGlyph, chevron.Text);
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter, true)]
    [InlineData(Key.Space, true)]
    [InlineData(Key.A, false)]
    public void Enter_or_Space_on_the_header_toggles_the_body(Key key, bool toggles)
    {
        var alert = RenderAlert("> [!NOTE]-\n> Body");
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key };

        alert.Header.RaiseEvent(args);

        Assert.Equal(toggles, alert.Content.IsVisible);
        Assert.Equal(toggles, args.Handled);
    }

    [AvaloniaFact]
    public void Custom_title_replaces_the_kind_in_the_header()
    {
        var alert = RenderAlert("> [!TIP] My custom title\n> Body text");

        Assert.Equal("My custom title", HeaderText(alert.Header));
        Assert.Equal(["markdown-alert", "markdown-alert-tip"], StyleClasses(alert.Border));
        Assert.False(alert.Header.Focusable);
        Assert.Null(CollapsibleAlert.Get(alert.Border));
        Assert.Equal(["Body text"], ParagraphTexts(alert.Content));
    }

    [AvaloniaFact]
    public void Custom_title_keeps_its_inline_formatting()
    {
        var alert = RenderAlert("> [!TIP] Use *this* `flag`\n> Body");

        Assert.Equal("Use this flag", HeaderText(alert.Header));
        Assert.Contains(alert.Header.Inlines!, i => i is Italic);
    }

    [AvaloniaTheory]
    [InlineData("-", false)]
    [InlineData("+", true)]
    public void Fold_marker_and_custom_title_combine(string marker, bool expanded)
    {
        var alert = RenderAlert($"> [!TIP]{marker} My title\n> Body");

        var glyph = expanded ? CollapsibleAlert.ExpandedGlyph : CollapsibleAlert.CollapsedGlyph;
        Assert.Equal($"{glyph} My title", HeaderText(alert.Header));
        Assert.Contains("markdown-alert-collapsible", alert.Border.Classes);
        Assert.Equal(expanded, alert.Content.IsVisible);
        Assert.Equal(["Body"], ParagraphTexts(alert.Content));
    }

    [AvaloniaTheory]
    [InlineData("> [!TIP] Title\n> line one\n> line two", "line one line two")]
    [InlineData("> [!TIP] Title\r\n> line one\r\n> line two", "line one line two")]
    [InlineData("> [!TIP]\tTitle   \n> line one", "line one")]
    public void Rest_of_the_title_paragraph_is_the_first_body_paragraph(string markdown, string body)
    {
        var alert = RenderAlert(markdown);

        Assert.Equal("Title", HeaderText(alert.Header).Trim());
        Assert.Equal([body], ParagraphTexts(alert.Content));
    }

    [AvaloniaFact]
    public void Title_followed_by_hard_line_breaks_splits_at_the_first_one()
    {
        var pipeline = new MarkdownPipelineBuilder().UseAlertBlocks().UseHardlineBreaks().Build();

        var alert = RenderAlert("> [!TIP] Title\n> line one\n> line two", pipeline);

        Assert.Equal("Title", HeaderText(alert.Header));
        Assert.Equal(["line one\nline two"], ParagraphTexts(alert.Content));
    }

    [AvaloniaFact]
    public void Title_alone_on_its_paragraph_leaves_the_following_blocks_as_the_body()
    {
        var alert = RenderAlert("> [!TIP] Title\n>\n> First\n>\n> Second");

        Assert.Equal("Title", HeaderText(alert.Header));
        Assert.Equal(["First", "Second"], ParagraphTexts(alert.Content));
    }

    [AvaloniaFact]
    public void Title_without_body_renders_an_empty_body()
    {
        var alert = RenderAlert("> [!TIP]- Title only");

        Assert.Equal($"{CollapsibleAlert.CollapsedGlyph} Title only", HeaderText(alert.Header));
        Assert.Empty(alert.Content.Children);
    }

    [AvaloniaTheory]
    [InlineData("> [!NOTE]-\n> line one\n> line two")]
    [InlineData("> [!NOTE]-\r\n> line one\r\n> line two")]
    [InlineData("> [!NOTE]- \t\n> line one\n> line two")]
    public void Fold_marker_line_is_consumed_like_a_plain_kind_line(string markdown)
    {
        var alert = RenderAlert(markdown);

        Assert.Equal($"{CollapsibleAlert.CollapsedGlyph} NOTE", HeaderText(alert.Header));
        Assert.Equal(["line one line two"], ParagraphTexts(alert.Content));
    }

    [AvaloniaTheory]
    [InlineData("> [!NOTE]", "NOTE")]
    [InlineData("> [!NOTE]+", $"{CollapsibleAlert.ExpandedGlyph} NOTE")]
    public void Marker_line_alone_renders_an_empty_body(string markdown, string header)
    {
        var alert = RenderAlert(markdown);

        Assert.Equal(header, HeaderText(alert.Header));
        Assert.Empty(alert.Content.Children);
    }

    [AvaloniaTheory]
    [InlineData("> [!NOTE]\n>\n> Body")]
    [InlineData("> [!NOTE]-\n>\n> Body")]
    public void Marker_line_followed_by_a_blank_line_leaves_no_empty_paragraph(string markdown)
    {
        var alert = RenderAlert(markdown);

        Assert.Equal(["Body"], ParagraphTexts(alert.Content));
        Assert.Single(alert.Content.Children);
    }

    [AvaloniaTheory]
    [InlineData("> [!QUESTION]\n> Why?", "QUESTION", false)]
    [InlineData("> [!question]- Why?\n> Because.", "Why?", true)]
    public void Unknown_kind_renders_with_its_own_lower_case_class(string markdown, string headerText, bool foldable)
    {
        var alert = RenderAlert(markdown);

        Assert.Contains("markdown-alert", alert.Border.Classes);
        Assert.Contains("markdown-alert-question", alert.Border.Classes);
        Assert.Equal(foldable, alert.Border.Classes.Contains("markdown-alert-collapsible"));
        Assert.EndsWith(headerText, HeaderText(alert.Header));
    }

    [AvaloniaTheory]
    [InlineData("> [!NOTE]x\n> Body")]
    [InlineData("> [!NOTE]-x\n> Body")]
    [InlineData("> [!NOTE]+-\n> Body")]
    [InlineData("> [!]- Title\n> Body")]
    [InlineData("> [!NO TE]-\n> Body")]
    [InlineData("> [!NOTE-\n> Body")]
    [InlineData("> [!NOTE")]
    [InlineData("> [NOTE]-\n> Body")]
    [InlineData("> Hello [!NOTE]- Title")]
    [InlineData("Para [!NOTE]- Title")]
    [InlineData("- > [!NOTE]- Title")]
    public void Malformed_or_misplaced_markers_are_not_alerts(string markdown)
    {
        Assert.Empty(Alerts(Render(markdown, AlertPipeline())));
    }

    [AvaloniaFact]
    public void Alert_marker_inside_an_alert_stays_text()
    {
        var alert = RenderAlert("> [!NOTE] Title\n>\n> [!TIP]- Not a title");

        Assert.Equal(["[!TIP]- Not a title"], ParagraphTexts(alert.Content));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Nested_foldable_alerts_follow_AllowNestedAlerts(bool allowNested)
    {
        var builder = new MarkdownPipelineBuilder();
        builder.Extensions.Add(new AlertExtension { AllowNestedAlerts = allowNested });
        var pipeline = builder.UseAlertBlocks().Build();

        var root = Render("- > [!NOTE]- Title\n  > Body", pipeline);

        Assert.Equal(allowNested ? 1 : 0, Alerts(root).Length);
    }

    [AvaloniaFact]
    public void Foldable_alert_inside_a_nested_alert_stays_a_blockquote()
    {
        var builder = new MarkdownPipelineBuilder();
        builder.Extensions.Add(new AlertExtension { AllowNestedAlerts = true });
        var pipeline = builder.UseAlertBlocks().Build();

        var alert = RenderAlert("> [!NOTE]\n> > [!TIP]- Inner\n> > Body", pipeline);

        var quote = Assert.IsType<Border>(Assert.Single(alert.Content.Children));
        Assert.Contains("markdown-blockquote", quote.Classes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UseAlertBlocks_installs_a_single_extended_alert_parser_whatever_the_order(bool alertExtensionFirst)
    {
        var builder = new MarkdownPipelineBuilder();
        if (alertExtensionFirst)
            builder.Extensions.Add(new AlertExtension { AllowNestedAlerts = true });
        builder.UseAlertBlocks().UseAlertBlocks();
        if (!alertExtensionFirst)
            builder.Extensions.Find<AlertExtension>()!.AllowNestedAlerts = true;
        builder.Build();

        var parser = Assert.Single(builder.InlineParsers.OfType<AlertInlineParser>());
        Assert.IsType<ExtendedAlertInlineParser>(parser);
        Assert.True(parser.AllowNestedAlerts);
        var parsers = builder.InlineParsers.ToList();
        Assert.Equal(parsers.FindIndex(p => p is LinkInlineParser) - 1, parsers.IndexOf(parser));
    }

    [Fact]
    public void Plain_alert_is_parsed_by_the_base_parser_and_extended_ones_carry_fold_and_title()
    {
        var pipeline = AlertPipeline();

        Assert.IsType<AlertBlock>(Assert.Single(Markdig.Markdown.Parse("> [!NOTE]\n> Body", pipeline)));
        var collapsed = Assert.IsType<ExtendedAlertBlock>(Assert.Single(Markdig.Markdown.Parse("> [!NOTE]-\n> Body", pipeline)));
        Assert.Equal((AlertFold.Collapsed, false, "NOTE"), (collapsed.Fold, collapsed.HasTitle, collapsed.Kind.ToString()));
        var titled = Assert.IsType<ExtendedAlertBlock>(Assert.Single(Markdig.Markdown.Parse("> [!Tip]+ Title\n> Body", pipeline)));
        Assert.Equal((AlertFold.Expanded, true, "Tip"), (titled.Fold, titled.HasTitle, titled.Kind.ToString()));
        var titleOnly = Assert.IsType<ExtendedAlertBlock>(Assert.Single(Markdig.Markdown.Parse("> [!TIP] Title", pipeline)));
        Assert.Equal((AlertFold.None, true), (titleOnly.Fold, titleOnly.HasTitle));
    }

    [Fact]
    public void Extended_alert_keeps_the_source_position_of_its_quote()
    {
        var alert = Assert.IsType<ExtendedAlertBlock>(
            Markdig.Markdown.Parse("Intro\n\n  > [!TIP]- Title\n  > Body", AlertPipeline())[1]);

        Assert.Equal((2, 2), (alert.Line, alert.Column));
        Assert.Equal(9, alert.Span.Start);
    }

    [Fact]
    public void Html_rendering_of_extended_alerts_keeps_the_alert_classes()
    {
        var html = Markdig.Markdown.ToHtml("> [!Tip]- Title\n> Body", AlertPipeline());

        Assert.Contains("<div class=\"markdown-alert markdown-alert-tip\">", html);
    }
}
