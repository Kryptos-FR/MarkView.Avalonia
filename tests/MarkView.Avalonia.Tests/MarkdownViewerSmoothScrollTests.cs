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

    /// <summary>Two headings far enough apart that a jump between them is longer than <see cref="SmoothScroll.LongDistance"/>.</summary>
    private static readonly string VeryLongMarkdown = $"""
        {Paragraphs}

        ## Top Heading

        {string.Join("\n\n", Enumerable.Repeat(Paragraphs, 6))}

        ## Bottom Heading

        {Paragraphs}
        """;

    private static (Window Window, ScrollViewer ScrollViewer, MarkdownViewer Viewer) Show(bool smooth = true, string? markdown = null)
    {
        var viewer = new MarkdownViewer
        {
            Template = ScrollingViewerTemplate.Create(),
            IsSmoothScrollingEnabled = smooth,
            Markdown = markdown ?? LongMarkdown,
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

        Assert.True(scroll.Step(start + scroll.Duration / 2));
        Assert.Equal(expected * SmoothScroll.Ease(0.5), scrollViewer.Offset.Y, 3);

        Assert.False(scroll.Step(start + scroll.Duration));
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
        scroll.Step(start + scroll.Duration / 3);

        scrollViewer.Offset = new Vector(0, 42);

        Assert.False(scroll.Step(start + scroll.Duration / 2));
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
        scroll.Step(scroll.Duration / 2);

        var first = viewer.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>().First();
        first.Margin = new Thickness(0, 0, 0, 500);
        window.UpdateLayout();
        var expected = TargetOffset(viewer, scrollViewer, "Target Heading");

        Assert.False(scroll.Step(scroll.Duration));
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
        Assert.False(first.Step(SmoothScroll.MaxDuration));
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

        Assert.False(scroll.Step(scroll.Duration / 2));
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
        scroll.Step(scroll.Duration);
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
    [InlineData(0, 150)]
    [InlineData(100, 150)]
    [InlineData(400, 160)]
    [InlineData(2500, 400)]
    [InlineData(10000, 500)]
    public void The_duration_grows_with_the_square_root_of_the_distance_between_a_minimum_and_a_maximum(double distance, double milliseconds)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), SmoothScroll.DurationFor(distance));
    }

    [AvaloniaFact]
    public void The_first_step_sets_the_duration_from_the_distance_to_the_anchor()
    {
        var (window, scrollViewer, viewer) = Show();
        var expected = TargetOffset(viewer, scrollViewer, "Target Heading");
        viewer.ScrollToAnchor("target-heading");
        var scroll = viewer.ActiveSmoothScroll!;

        scroll.Step(TimeSpan.Zero);

        Assert.Equal(SmoothScroll.DurationFor(expected), scroll.Duration);
        Assert.True(scroll.Duration > SmoothScroll.MinDuration);
        window.Close();
    }

    [AvaloniaFact]
    public void A_long_jump_down_goes_at_once_to_the_long_distance_above_the_anchor_and_animates_the_rest()
    {
        var (window, scrollViewer, viewer) = Show(markdown: VeryLongMarkdown);
        var expected = TargetOffset(viewer, scrollViewer, "Bottom Heading");
        var distance = SmoothScroll.LongDistance;
        Assert.True(expected > distance + 100);
        viewer.ScrollToAnchor("bottom-heading");
        var scroll = viewer.ActiveSmoothScroll!;

        Assert.True(scroll.Step(TimeSpan.Zero));
        // The jump lands on a whole pixel.
        Assert.Equal(Math.Round(expected - distance), scrollViewer.Offset.Y, 3);
        Assert.Equal(SmoothScroll.MaxDuration.TotalMilliseconds, scroll.Duration.TotalMilliseconds, tolerance: 0.1);

        Assert.True(scroll.Step(scroll.Duration / 2));
        Assert.Equal(Math.Round(expected - distance) + (expected - Math.Round(expected - distance)) * SmoothScroll.Ease(0.5), scrollViewer.Offset.Y, 3);
        Assert.False(scroll.Step(scroll.Duration));
        // The target is measured from a layout at the fractional offset of the previous frame, which rounds to a quarter pixel.
        Assert.Equal(expected, scrollViewer.Offset.Y, tolerance: 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void A_long_jump_up_goes_at_once_to_the_long_distance_below_the_anchor()
    {
        var (window, scrollViewer, viewer) = Show(markdown: VeryLongMarkdown);
        scrollViewer.Offset = new Vector(0, scrollViewer.Extent.Height);
        window.UpdateLayout();
        var expected = TargetOffset(viewer, scrollViewer, "Top Heading");
        Assert.True(scrollViewer.Offset.Y - expected > SmoothScroll.LongDistance + 100);
        viewer.ScrollToAnchor("top-heading");
        var scroll = viewer.ActiveSmoothScroll!;

        Assert.True(scroll.Step(TimeSpan.Zero));
        Assert.Equal(Math.Round(expected + SmoothScroll.LongDistance), scrollViewer.Offset.Y, 3);
        Assert.False(scroll.Step(scroll.Duration));
        Assert.Equal(expected, scrollViewer.Offset.Y, 3);
        window.Close();
    }

    [AvaloniaFact]
    public void A_jump_shorter_than_the_long_distance_animates_all_the_way()
    {
        var (window, scrollViewer, viewer) = Show();
        Assert.True(TargetOffset(viewer, scrollViewer, "Target Heading") < SmoothScroll.LongDistance);
        viewer.ScrollToAnchor("target-heading");

        viewer.ActiveSmoothScroll!.Step(TimeSpan.Zero);

        Assert.Equal(0, scrollViewer.Offset.Y);
        window.Close();
    }

    [AvaloniaFact]
    public void A_short_jump_far_down_the_document_animates_all_the_way()
    {
        var (window, scrollViewer, viewer) = Show(markdown: VeryLongMarkdown);
        var expected = TargetOffset(viewer, scrollViewer, "Bottom Heading");
        // Far from the top, so the start and target offsets add up to more than the long distance.
        scrollViewer.Offset = new Vector(0, expected - 500);
        window.UpdateLayout();
        viewer.ScrollToAnchor("bottom-heading");
        var scroll = viewer.ActiveSmoothScroll!;

        scroll.Step(TimeSpan.Zero);

        Assert.Equal(expected - 500, scrollViewer.Offset.Y, 3);
        Assert.Equal(SmoothScroll.DurationFor(500).TotalMilliseconds, scroll.Duration.TotalMilliseconds, tolerance: 1);
        window.Close();
    }

    [Fact]
    public void The_long_distance_is_where_the_duration_reaches_its_maximum()
    {
        Assert.Equal(SmoothScroll.MaxDuration, SmoothScroll.DurationFor(SmoothScroll.LongDistance));
        Assert.True(SmoothScroll.DurationFor(SmoothScroll.LongDistance - 100) < SmoothScroll.MaxDuration);
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
