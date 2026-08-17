// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerTemplateTests
{
    private static StyleInclude MarkdownThemeInclude() => new((Uri?)null)
    {
        Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml")
    };

    [AvaloniaFact]
    public void ScrollToAnchor_moves_PART_ScrollViewer_offset_with_default_template()
    {
        var viewer = new MarkdownViewer();
        viewer.Styles.Add(MarkdownThemeInclude());
        viewer.Markdown = string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Paragraph {i}"))
            + "\n\n## Target Heading\n\n"
            + string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Trailing paragraph {i}"));

        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();

        var scrollViewer = viewer.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Assert.Equal(0, scrollViewer.Offset.Y);

        var heading = viewer.GetVisualDescendants().OfType<TextBlock>()
            .Single(tb => tb.Classes.Contains("markdown-h2"));
        var expectedPoint = heading.TranslatePoint(new Point(0, 0), (Visual)viewer.Content!);
        Assert.NotNull(expectedPoint);
        var expectedY = Math.Max(0, expectedPoint.Value.Y - 16);

        viewer.ScrollToAnchor("target-heading");

        // Exact match against the anchor's own translated position (minus the 16px top
        // margin ScrollToAnchor applies) — not just "some scroll happened". A generic
        // Control.BringIntoView() (today's fallback, still reachable through the ancestor
        // PART_ScrollViewer via the bubbling RequestBringIntoViewEvent) also produces a
        // nonzero offset here, but not this exact one, so this assertion only passes once
        // ScrollToAnchor is actually driven by the cached PART_ScrollViewer reference.
        Assert.Equal(expectedY, scrollViewer.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void ScrollToAnchor_does_not_throw_when_template_has_no_PART_ScrollViewer()
    {
        var viewer = new MarkdownViewer
        {
            Template = new FuncControlTemplate((_, ns) => new TextBlock()),
            Markdown = "# Target Heading\n\nBody."
        };

        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();

        Assert.Empty(viewer.GetVisualDescendants().OfType<ScrollViewer>());

        var exception = Record.Exception(() => viewer.ScrollToAnchor("target-heading"));
        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void RenderMarkdown_resets_PART_ScrollViewer_offset_to_top_on_new_document()
    {
        var viewer = new MarkdownViewer();
        viewer.Styles.Add(MarkdownThemeInclude());
        viewer.Markdown = string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Paragraph {i}"))
            + "\n\n## Target Heading\n\n"
            + string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Trailing paragraph {i}"));

        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();

        viewer.ScrollToAnchor("target-heading");
        var scrollViewer = viewer.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Assert.True(scrollViewer.Offset.Y > 0);

        viewer.Markdown = "# New Document\n\nBrand new content.";

        Assert.Equal(0, scrollViewer.Offset.Y);
        Assert.Equal(0, scrollViewer.Offset.X);
    }

    [AvaloniaFact]
    public void ScrollToAnchor_lands_at_top_margin_when_viewer_has_non_default_padding()
    {
        // Regression test: Padding is applied as PART_ContentPresenter's Margin *inside*
        // PART_ScrollViewer, so Content's origin is offset from the scrollable extent's
        // origin by Padding.Top. ScrollToAnchor must account for that offset, or it
        // undershoots by exactly Padding.Top.
        var viewer = new MarkdownViewer { Padding = new Thickness(0, 100, 0, 0) };
        viewer.Styles.Add(MarkdownThemeInclude());
        viewer.Markdown = string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Paragraph {i}"))
            + "\n\n## Target Heading\n\n"
            + string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Trailing paragraph {i}"));

        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();

        viewer.ScrollToAnchor("target-heading");
        Dispatcher.UIThread.RunJobs();

        var scrollViewer = viewer.GetVisualDescendants().OfType<ScrollViewer>().Single();
        var heading = viewer.GetVisualDescendants().OfType<TextBlock>()
            .Single(tb => tb.Classes.Contains("markdown-h2"));

        // The heading lands ~16px below the viewport top (the intended top margin),
        // independent of Padding.
        var headingInViewport = heading.TranslatePoint(new Point(0, 0), scrollViewer);
        Assert.NotNull(headingInViewport);
        Assert.Equal(16, headingInViewport.Value.Y, 3);
    }

    [AvaloniaFact]
    public void ScrollViewer_attached_properties_pass_through_to_PART_ScrollViewer()
    {
        var viewer = new MarkdownViewer { Markdown = "Hello" };
        viewer.Styles.Add(MarkdownThemeInclude());
        ScrollViewer.SetVerticalScrollBarVisibility(viewer, ScrollBarVisibility.Hidden);

        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();

        var scrollViewer = viewer.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Assert.Equal(ScrollBarVisibility.Hidden, scrollViewer.VerticalScrollBarVisibility);
    }
}
