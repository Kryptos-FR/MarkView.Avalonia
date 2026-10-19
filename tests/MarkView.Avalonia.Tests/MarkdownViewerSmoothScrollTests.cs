// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerSmoothScrollTests
{
    private static readonly string Paragraphs = string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Paragraph {i}"));

    private static readonly string LongMarkdown = $"""
        {Paragraphs}

        ## Target Heading

        {Paragraphs}

        ## Second Heading

        {Paragraphs}
        """;

    private static (Window Window, ScrollViewer ScrollViewer, MarkdownViewer Viewer) Show(bool smooth = true)
    {
        var viewer = new MarkdownViewer
        {
            Template = ScrollingViewerTemplate.Create(),
            IsSmoothScrollingEnabled = smooth,
            Markdown = LongMarkdown,
        };
        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();
        window.UpdateLayout();
        return (window, viewer.GetVisualDescendants().OfType<ScrollViewer>().Single(), viewer);
    }

    /// <summary>The offset that shows the heading <paramref name="text"/> 16 px below the top.</summary>
    private static double TargetOffset(MarkdownViewer viewer, ScrollViewer scrollViewer, string text)
    {
        var heading = viewer.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>()
            .Single(t => t.Classes.Contains("markdown-h2") && MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!) == text);
        return scrollViewer.Offset.Y + heading.TranslatePoint(default, scrollViewer)!.Value.Y - 16;
    }

    [AvaloniaFact]
    public void Smooth_scrolling_is_off_by_default()
    {
        Assert.False(new MarkdownViewer().IsSmoothScrollingEnabled);
    }

    [AvaloniaFact]
    public void Without_smooth_scrolling_an_anchor_is_shown_at_once()
    {
        var (window, scrollViewer, viewer) = Show(smooth: false);
        var expected = TargetOffset(viewer, scrollViewer, "Target Heading");

        viewer.ScrollToAnchor("target-heading");

        Assert.Null(viewer.ActiveSmoothScroll);
        Assert.Equal(expected, scrollViewer.Offset.Y, 3);
        window.Close();
    }

    [AvaloniaFact]
    public void Smooth_scroll_eases_from_the_current_offset_to_the_anchor()
    {
        var (window, scrollViewer, viewer) = Show();
        var expected = TargetOffset(viewer, scrollViewer, "Target Heading");
        Assert.True(expected > 100);

        viewer.ScrollToAnchor("target-heading");
        var scroll = viewer.ActiveSmoothScroll;
        Assert.NotNull(scroll);
        // Nothing moves before the first frame.
        Assert.Equal(0, scrollViewer.Offset.Y);

        var start = TimeSpan.FromSeconds(10);
        Assert.True(scroll.Step(start));
        Assert.Equal(0, scrollViewer.Offset.Y, 3);

        Assert.True(scroll.Step(start + SmoothScroll.Duration / 2));
        Assert.Equal(expected * SmoothScroll.Ease(0.5), scrollViewer.Offset.Y, 3);

        Assert.False(scroll.Step(start + SmoothScroll.Duration));
        Assert.Equal(expected, scrollViewer.Offset.Y, 3);
        Assert.Null(viewer.ActiveSmoothScroll);
        window.Close();
    }

    [AvaloniaFact]
    public void Smooth_scroll_runs_on_the_rendered_frames()
    {
        var (window, scrollViewer, viewer) = Show();
        var expected = TargetOffset(viewer, scrollViewer, "Target Heading");

        viewer.ScrollToAnchor("target-heading");
        // The frames' times are the clock's.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (viewer.ActiveSmoothScroll is not null && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Null(viewer.ActiveSmoothScroll);
        // Laid out between frames at fractional offsets, the heading's position is rounded to device pixels.
        Assert.InRange(scrollViewer.Offset.Y, expected - 1, expected + 1);
        window.Close();
    }

    [AvaloniaFact]
    public void A_scroll_by_the_user_stops_the_animation()
    {
        var (window, scrollViewer, viewer) = Show();
        viewer.ScrollToAnchor("target-heading");
        var scroll = viewer.ActiveSmoothScroll!;
        var start = TimeSpan.Zero;
        scroll.Step(start);
        scroll.Step(start + SmoothScroll.Duration / 3);

        scrollViewer.Offset = new Vector(0, 42);

        Assert.False(scroll.Step(start + SmoothScroll.Duration / 2));
        Assert.Equal(42, scrollViewer.Offset.Y);
        Assert.Null(viewer.ActiveSmoothScroll);
        window.Close();
    }

    [AvaloniaFact]
    public void The_animation_follows_content_that_grows_above_the_anchor()
    {
        var (window, scrollViewer, viewer) = Show();
        viewer.ScrollToAnchor("target-heading");
        var scroll = viewer.ActiveSmoothScroll!;
        scroll.Step(TimeSpan.Zero);
        scroll.Step(SmoothScroll.Duration / 2);

        var first = viewer.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>().First();
        first.Margin = new Thickness(0, 0, 0, 500);
        window.UpdateLayout();
        var expected = TargetOffset(viewer, scrollViewer, "Target Heading");

        Assert.False(scroll.Step(SmoothScroll.Duration));
        Assert.Equal(expected, scrollViewer.Offset.Y, 3);
        window.Close();
    }

    [AvaloniaFact]
    public void A_new_jump_replaces_the_running_animation()
    {
        var (window, scrollViewer, viewer) = Show();
        viewer.ScrollToAnchor("target-heading");
        var first = viewer.ActiveSmoothScroll!;
        first.Step(TimeSpan.Zero);

        viewer.ScrollToAnchor("second-heading");
        var second = viewer.ActiveSmoothScroll;

        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.False(first.IsRunning);
        Assert.False(first.Step(SmoothScroll.Duration));
        window.Close();
    }

    [AvaloniaFact]
    public void A_new_document_stops_the_animation()
    {
        var (window, scrollViewer, viewer) = Show();
        viewer.ScrollToAnchor("target-heading");
        var scroll = viewer.ActiveSmoothScroll!;
        scroll.Step(TimeSpan.Zero);

        viewer.Markdown = "# Other\n\nShort.";

        Assert.False(scroll.Step(SmoothScroll.Duration / 2));
        Assert.False(scroll.IsRunning);
        Assert.Equal(0, scrollViewer.Offset.Y);
        window.Close();
    }

    [AvaloniaFact]
    public void An_anchor_link_scrolls_smoothly()
    {
        var (window, scrollViewer, viewer) = Show();
        viewer.Markdown = "[Go](#target-heading)\n\n" + LongMarkdown;
        window.UpdateLayout();
        var expected = TargetOffset(viewer, scrollViewer, "Target Heading");

        viewer.GetVisualDescendants().OfType<LinkFocusTarget>().Single().Activate();

        var scroll = viewer.ActiveSmoothScroll;
        Assert.NotNull(scroll);
        scroll.Step(TimeSpan.Zero);
        scroll.Step(SmoothScroll.Duration);
        Assert.Equal(expected, scrollViewer.Offset.Y, 3);
        window.Close();
    }

    [AvaloniaFact]
    public void A_source_fragment_is_shown_at_once()
    {
        var viewer = new MarkdownViewer
        {
            Template = ScrollingViewerTemplate.Create(),
            IsSmoothScrollingEnabled = true,
        };
        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();
        var scrollViewer = viewer.GetVisualDescendants().OfType<ScrollViewer>().Single();

        viewer.Source = new Uri("avares://MarkView.Avalonia.Tests/TestAssets/long.md#target-heading");
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(viewer.ActiveSmoothScroll);
        Assert.True(scrollViewer.Offset.Y > 0);
        window.Close();
    }

    [AvaloniaFact]
    public void A_viewer_that_is_not_shown_does_not_animate()
    {
        var viewer = new MarkdownViewer { IsSmoothScrollingEnabled = true, Markdown = LongMarkdown };

        viewer.ScrollToAnchor("target-heading");

        Assert.Null(viewer.ActiveSmoothScroll);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.875)]
    [InlineData(1, 1)]
    public void Ease_is_a_cubic_ease_out(double progress, double expected)
    {
        Assert.Equal(expected, SmoothScroll.Ease(progress), 9);
    }
}
