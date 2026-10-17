// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

/// <summary>
/// Rendering of HTML <c>&lt;details&gt;</c> / <c>&lt;summary&gt;</c> as a collapsible block.
/// </summary>
public class HtmlDetailsTests : RenderTestBase
{
    private sealed record RenderedDetails(Border Border, TextBlock Summary, StackPanel Content);

    // Built for each test, so that a mutant active while it is built does not outlive its own run.
    private static MarkdownPipeline Pipeline => new MarkdownPipelineBuilder().UseHtmlDetails().Build();

    private static RenderedDetails RenderDetails(string markdown) => AsDetails(Assert.Single(Render(markdown, Pipeline).Children));

    private static RenderedDetails AsDetails(Control control)
    {
        var border = Assert.IsType<Border>(control);
        var outer = Assert.IsType<StackPanel>(border.Child);
        Assert.Equal(2, outer.Children.Count);
        var summary = Assert.IsType<TextBlock>(outer.Children[0]);
        var content = Assert.IsType<StackPanel>(outer.Children[1]);
        return new RenderedDetails(border, summary, content);
    }

    private static string[] StyleClasses(StyledElement element) =>
        element.Classes.Where(c => !c.StartsWith(':')).ToArray();

    // The summary text shows the chevron as the glyph it draws.
    private const string Collapsed = "▸";
    private const string Expanded = "▾";

    private static AlertChevron Chevron(TextBlock summary) =>
        Assert.IsType<AlertChevron>(Assert.IsType<InlineUIContainer>(summary.Inlines![0]).Child);

    private static string SummaryText(TextBlock summary) =>
        string.Concat(summary.Inlines!.Select(i => i switch
        {
            Run run => run.Text,
            InlineUIContainer { Child: AlertChevron chevron } => chevron.IsExpanded ? Expanded : Collapsed,
            _ => string.Empty,
        }));

    private static string[] ParagraphTexts(Panel content) =>
        content.Children.OfType<MarkdownSelectableTextBlock>()
            .Select(tb => MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!))
            .ToArray();

    private const string Collapsible = "<details>\n<summary>Title</summary>\n\nBody\n\n</details>";

    [AvaloniaTheory]
    [InlineData("<details>", false, Collapsed)]
    [InlineData("<details open>", true, Expanded)]
    public void Details_render_as_a_collapsible_block_in_their_initial_state(string tag, bool expanded, string glyph)
    {
        var details = RenderDetails($"{tag}\n<summary>Title</summary>\n\nBody\n\n</details>");

        Assert.Equal(["markdown-details"], StyleClasses(details.Border));
        Assert.Equal(["markdown-details-summary"], StyleClasses(details.Summary));
        Assert.Equal(["markdown-details-content"], StyleClasses(details.Content));
        Assert.Equal(expanded, details.Border.Classes.Contains(":expanded"));
        Assert.Equal(expanded, details.Content.IsVisible);
        Assert.True(details.Summary.Focusable);
        Assert.Equal(TextWrapping.Wrap, details.Summary.TextWrapping);
        Assert.Equal($"{glyph} Title", SummaryText(details.Summary));
        var chevron = Chevron(details.Summary);
        Assert.Equal(["markdown-details-chevron"], chevron.Classes.ToArray());
        Assert.Equal(expanded, chevron.IsExpanded);
        Assert.Equal(["Body"], ParagraphTexts(details.Content));
        Assert.Equal(expanded, CollapsibleAlert.Get(details.Border)!.IsExpanded);
    }

    [AvaloniaFact]
    public void Missing_summary_shows_Details()
    {
        var details = RenderDetails("<details>Body</details>");

        Assert.Equal($"{Collapsed} Details", SummaryText(details.Summary));
        Assert.Equal(["Body"], ParagraphTexts(details.Content));
    }

    [AvaloniaFact]
    public void Body_and_summary_space_like_the_document()
    {
        var details = RenderDetails(Collapsible);

        Assert.Equal(8, details.Content.Spacing);
        Assert.Equal(8, Assert.IsType<StackPanel>(details.Border.Child).Spacing);
    }

    [AvaloniaFact]
    public void Toggle_shows_and_hides_the_body_and_updates_chevron_and_pseudo_class()
    {
        var details = RenderDetails(Collapsible);
        var state = CollapsibleAlert.Get(details.Border)!;

        state.Toggle();

        Assert.True(details.Content.IsVisible);
        Assert.Contains(":expanded", details.Border.Classes);
        Assert.Equal($"{Expanded} Title", SummaryText(details.Summary));

        state.Toggle();

        Assert.False(details.Content.IsVisible);
        Assert.DoesNotContain(":expanded", details.Border.Classes);
        Assert.Equal($"{Collapsed} Title", SummaryText(details.Summary));
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter, true)]
    [InlineData(Key.Space, true)]
    [InlineData(Key.Tab, false)]
    public void Enter_or_Space_on_the_summary_toggles_the_body(Key key, bool toggles)
    {
        var details = RenderDetails(Collapsible);
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key };

        details.Summary.RaiseEvent(args);

        Assert.Equal(toggles, details.Content.IsVisible);
        Assert.Equal(toggles, args.Handled);
    }

    [AvaloniaFact]
    public void Left_click_on_the_summary_toggles_the_body()
    {
        var root = Render(Collapsible, Pipeline);
        var window = new Window { Width = 400, Height = 300, Content = root };
        window.Show();
        try
        {
            var details = AsDetails(Assert.Single(root.Children));
            var size = details.Summary.Bounds.Size;
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
            var args = new PointerReleasedEventArgs(details.Summary, pointer, details.Summary,
                new Point(size.Width / 2, size.Height / 2), 1, properties, KeyModifiers.None, MouseButton.Left);

            details.Summary.RaiseEvent(args);

            Assert.True(details.Content.IsVisible);
            Assert.True(args.Handled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nested_details_render_nested_collapsible_blocks()
    {
        var outer = RenderDetails("""
            <details open>
            <summary>Outer</summary>

            <details>
            <summary>Inner</summary>

            ## Inside heading

            </details>

            </details>
            """);

        var inner = AsDetails(Assert.Single(outer.Content.Children));
        Assert.Equal($"{Collapsed} Inner", SummaryText(inner.Summary));
        var heading = Assert.IsAssignableFrom<TextBlock>(Assert.Single(inner.Content.Children));
        Assert.Contains("markdown-h2", heading.Classes);

        Assert.True(CollapsibleAlert.ExpandAncestors(heading));
        Assert.True(inner.Content.IsVisible);
        Assert.True(outer.Content.IsVisible);
    }

    [AvaloniaFact]
    public void Collapsed_ancestors_are_all_expanded()
    {
        var outer = RenderDetails("<details><summary>A</summary>\n\n<details><summary>B</summary>\n\nDeep\n\n</details>\n\n</details>");
        var inner = AsDetails(Assert.Single(outer.Content.Children));
        var paragraph = Assert.Single(inner.Content.Children);

        Assert.True(CollapsibleAlert.ExpandAncestors(paragraph));

        Assert.True(outer.Content.IsVisible);
        Assert.True(inner.Content.IsVisible);
    }

    [AvaloniaFact]
    public void Html_text_body_renders_as_a_paragraph_with_line_breaks()
    {
        var details = RenderDetails("<details><summary>S</summary>one<br>two</details>");

        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(details.Content.Children));
        Assert.Contains("markdown-paragraph", paragraph.Classes);
        Assert.Contains(paragraph.Inlines!, i => i is LineBreak);
    }

    [AvaloniaFact]
    public void Other_html_blocks_still_render_nothing()
    {
        var root = Render("<div>\nraw\n</div>\n\n<details><summary>S</summary>\n\n<p>kept</p>\n\n</details>", Pipeline);

        var details = AsDetails(Assert.Single(root.Children));
        Assert.Empty(details.Content.Children);
    }

    [AvaloniaFact]
    public void Unmatched_tags_render_as_before()
    {
        var root = Render("<details>\n<summary>S</summary>\n\nBody", Pipeline);

        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children));
        Assert.Equal("Body", MarkdownSelectableTextBlock.ExtractPlainText(paragraph.Inlines!));
    }
}
