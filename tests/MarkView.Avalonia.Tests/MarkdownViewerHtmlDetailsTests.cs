// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// HTML <c>&lt;details&gt;</c> blocks inside a full <see cref="MarkdownViewer"/> with its default pipeline:
/// summary clicks, document selection and anchor navigation.
/// </summary>
public class MarkdownViewerHtmlDetailsTests
{
    private const string HiddenBody = """
        Before

        <details>
        <summary>More</summary>

        Hidden one

        Hidden two

        </details>

        After
        """;

    private static Border Details(MarkdownViewer viewer) =>
        viewer.GetLogicalDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-details"));

    private static TextBlock Summary(Border details) =>
        details.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-details-summary"));

    [AvaloniaFact]
    public void Default_pipeline_renders_details()
    {
        using var _ = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.Pipeline = null;

        var viewer = new MarkdownViewer { Markdown = HiddenBody };

        Assert.False(CollapsibleAlert.Get(Details(viewer))!.IsExpanded);
    }

    [AvaloniaFact]
    public void Selection_skips_the_body_of_collapsed_details_until_they_are_expanded()
    {
        var viewer = new MarkdownViewer { Markdown = HiddenBody };

        viewer.SelectAll();
        Assert.Equal("Before\nAfter", viewer.GetSelectedText());

        CollapsibleAlert.Get(Details(viewer))!.Toggle();
        viewer.SelectAll();
        Assert.Equal("Before\nHidden one\nHidden two\nAfter", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Clicking_the_summary_toggles_the_body_without_selecting()
    {
        var viewer = new MarkdownViewer();
        viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        viewer.Markdown = HiddenBody;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        try
        {
            var details = Details(viewer);
            var summary = Summary(details);
            var point = summary.TranslatePoint(new Point(summary.Bounds.Width / 2, summary.Bounds.Height / 2), window)!.Value;

            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);

            Assert.True(CollapsibleAlert.Get(details)!.IsExpanded);
            Assert.Equal(string.Empty, viewer.GetSelectedText());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScrollToAnchor_expands_the_details_around_a_heading_and_scrolls_to_it()
    {
        var leading = string.Join("\n\n", Enumerable.Range(0, 30).Select(i => $"Paragraph {i}"));
        var trailing = string.Join("\n\n", Enumerable.Range(0, 30).Select(i => $"Trailing paragraph {i}"));
        var viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create() };
        viewer.Markdown = $"""
            {leading}

            <details>
            <summary>Outer</summary>

            <details>
            <summary>Inner</summary>

            ## Inside heading

            </details>

            </details>

            {trailing}
            """;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        try
        {
            var scrollViewer = viewer.GetVisualDescendants().OfType<ScrollViewer>().Single();
            var heading = viewer.GetVisualDescendants().OfType<TextBlock>().Single(tb => tb.Classes.Contains("markdown-h2"));

            viewer.ScrollToAnchor("inside-heading");
            window.UpdateLayout();

            Assert.True(heading.IsEffectivelyVisible);
            Assert.Equal(16, heading.TranslatePoint(new Point(0, 0), scrollViewer)!.Value.Y, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Headings_inside_collapsed_details_are_listed_in_the_table_of_contents()
    {
        var viewer = new MarkdownViewer { Markdown = "<details>\n<summary>S</summary>\n\n## Inside heading\n\n</details>" };

        Assert.Equal(["Inside heading"], viewer.TableOfContents.Select(e => e.Text));
    }
}
