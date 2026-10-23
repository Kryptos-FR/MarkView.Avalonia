// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>A drag that selects text scrolls the viewer while the pointer is near or past its top or bottom edge.</summary>
public class MarkdownViewerSelectionAutoScrollTests
{
    private static readonly string Paragraphs = string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Paragraph {i}"));

    // The viewer is 200 px high, with 50 px of window above and below it to drag the pointer to.
    private const double ViewerTop = 50;
    private const double ViewerHeight = 200;
    private const double ViewerBottom = ViewerTop + ViewerHeight;

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, ScrollViewer ScrollViewer) : IDisposable
    {
        public MarkdownSelectableTextBlock[] Blocks =>
            Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().ToArray();

        /// <summary>The point at character <paramref name="index"/> of <paramref name="block"/>, in the window.</summary>
        public Point At(TextBlock block, int index)
        {
            var caret = block.TextLayout.HitTestTextPosition(index);
            var origin = block.TranslatePoint(default, Window)!.Value;
            return new Point(origin.X + block.Padding.Left + caret.X + 0.5, origin.Y + block.Padding.Top + caret.Y + caret.Height / 2);
        }

        /// <summary>The numbers of the paragraphs shown at least in part in the viewport.</summary>
        public int[] ParagraphsInView() => Blocks
            .Select((block, i) => (i, Top: block.TranslatePoint(default, ScrollViewer)!.Value.Y, block.Bounds.Height))
            .Where(b => b.Top < ScrollViewer.Bounds.Height && b.Top + b.Height > 0)
            .Select(b => b.i)
            .ToArray();

        /// <summary>
        /// Scrolls the document so that the middle of the first line of the first paragraph below
        /// <paramref name="offset"/> is <paramref name="y"/> pixels below the top of the viewport, and returns that paragraph.
        /// </summary>
        public TextBlock ScrollParagraphTo(double offset, double y)
        {
            ScrollViewer.Offset = new Vector(0, offset);
            Window.UpdateLayout();
            var block = Blocks.First(b => At(b, 1).Y > ViewerTop);
            ScrollViewer.Offset = new Vector(0, offset + At(block, 1).Y - ViewerTop - y);
            Window.UpdateLayout();
            return block;
        }

        /// <summary>Presses on paragraph <paramref name="paragraph"/> and drags past the drag threshold.</summary>
        public void StartDrag(int paragraph)
        {
            var start = At(Blocks[paragraph], 1);
            Window.MouseDown(start, MouseButton.Left);
            Window.MouseMove(At(Blocks[paragraph], 5), RawInputModifiers.LeftMouseButton);
        }

        public void Dispose() => Window.Close();
    }

    // The tests run the frames themselves, with Step, except when realFrames.
    private static Fixture Show(string? markdown = null, bool realFrames = false)
    {
        var viewer = new MarkdownViewer
        {
            RequestAnimationFrame = realFrames ? null : _ => { },
            Template = ScrollingViewerTemplate.Create(),
            Markdown = markdown ?? Paragraphs,
            Height = ViewerHeight,
            Margin = new Thickness(0, ViewerTop, 0, 0),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
        };
        var window = new Window { Width = 400, Height = ViewerBottom + 50, Content = viewer };
        // The headless text drawing has no hit-testable glyphs; a transparent background stands in.
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        window.Show();
        window.UpdateLayout();
        return new Fixture(window, viewer, viewer.GetVisualDescendants().OfType<ScrollViewer>().Single());
    }

    private static readonly TimeSpan Start = TimeSpan.FromSeconds(10);

    // ── Speed ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(100, 200, 0)]
    [InlineData(24, 200, 0)]
    [InlineData(176, 200, 0)]
    [InlineData(14, 200, -10 * SelectionAutoScroll.SpeedPerPixel)]
    [InlineData(0, 200, -24 * SelectionAutoScroll.SpeedPerPixel)]
    [InlineData(-26, 200, -50 * SelectionAutoScroll.SpeedPerPixel)]
    [InlineData(186, 200, 10 * SelectionAutoScroll.SpeedPerPixel)]
    [InlineData(250, 200, 74 * SelectionAutoScroll.SpeedPerPixel)]
    [InlineData(-1000, 200, -SelectionAutoScroll.MaxSpeed)]
    [InlineData(1000, 200, SelectionAutoScroll.MaxSpeed)]
    // A viewport too short for two full bands keeps a quarter of its height for each.
    [InlineData(10, 40, 0)]
    [InlineData(5, 40, -5 * SelectionAutoScroll.SpeedPerPixel)]
    [InlineData(35, 40, 5 * SelectionAutoScroll.SpeedPerPixel)]
    public void The_speed_grows_with_how_far_the_pointer_is_into_or_past_an_edge(double y, double height, double expected)
    {
        Assert.Equal(expected, SelectionAutoScroll.SpeedAt(y, height), 6);
    }

    // ── Scrolling ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Dragging_below_the_viewer_scrolls_down_on_each_frame_and_selects_the_text_brought_into_view()
    {
        using var f = Show();
        f.StartDrag(0);
        var lastInView = f.ParagraphsInView().Max();

        f.Window.MouseMove(new Point(300, ViewerBottom + 26), RawInputModifiers.LeftMouseButton);
        var scroll = f.Viewer.ActiveSelectionAutoScroll;
        Assert.NotNull(scroll);
        // Past the edge, the selection ends at the text at the edge, not at text out of view.
        Assert.Contains($"Paragraph {lastInView}", f.Viewer.SelectedText);
        Assert.DoesNotContain($"Paragraph {lastInView + 1}", f.Viewer.SelectedText);

        // The first frame starts the clock; each next one scrolls by the speed times the time since the last.
        Assert.True(scroll.Step(Start));
        Assert.Equal(0, f.ScrollViewer.Offset.Y);
        Assert.True(scroll.Step(Start + TimeSpan.FromMilliseconds(50)));
        Assert.Equal(50 * SelectionAutoScroll.SpeedPerPixel * 0.05, f.ScrollViewer.Offset.Y, 3);
        Assert.True(scroll.Step(Start + TimeSpan.FromMilliseconds(100)));
        Assert.Equal(2 * 50 * SelectionAutoScroll.SpeedPerPixel * 0.05, f.ScrollViewer.Offset.Y, 3);
        f.Window.UpdateLayout();

        var lastNowInView = f.ParagraphsInView().Max();
        Assert.True(lastNowInView > lastInView);
        Assert.StartsWith("aragraph 0", f.Viewer.SelectedText);
        Assert.Contains($"Paragraph {lastNowInView}", f.Viewer.SelectedText);
        Assert.DoesNotContain($"Paragraph {lastNowInView + 1}", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void Dragging_above_the_viewer_scrolls_up()
    {
        using var f = Show();
        f.ScrollViewer.Offset = new Vector(0, 300);
        f.Window.UpdateLayout();
        var firstInView = f.ParagraphsInView().Min();
        f.StartDrag(firstInView + 3);

        f.Window.MouseMove(new Point(5, ViewerTop - 26), RawInputModifiers.LeftMouseButton);
        var scroll = f.Viewer.ActiveSelectionAutoScroll!;
        scroll.Step(Start);
        scroll.Step(Start + TimeSpan.FromMilliseconds(50));
        f.Window.UpdateLayout();

        Assert.Equal(300 - 50 * SelectionAutoScroll.SpeedPerPixel * 0.05, f.ScrollViewer.Offset.Y, 3);
        var firstNowInView = f.ParagraphsInView().Min();
        Assert.True(firstNowInView < firstInView);
        Assert.StartsWith($"Paragraph {firstNowInView}\n", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void Holding_the_pointer_in_the_edge_band_inside_the_viewer_scrolls_too()
    {
        using var f = Show();
        f.StartDrag(0);
        f.Window.MouseMove(new Point(100, ViewerTop + 100), RawInputModifiers.LeftMouseButton);

        f.Window.MouseMove(new Point(100, ViewerBottom - 4), RawInputModifiers.LeftMouseButton);
        var scroll = f.Viewer.ActiveSelectionAutoScroll!;
        scroll.Step(Start);
        scroll.Step(Start + TimeSpan.FromMilliseconds(100));

        Assert.Equal(20 * SelectionAutoScroll.SpeedPerPixel * 0.1, f.ScrollViewer.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void A_drag_that_starts_in_an_edge_band_scrolls_once_it_has_left_the_band()
    {
        using var f = Show();
        var top = f.ScrollParagraphTo(300, 10);
        f.Window.MouseDown(f.At(top, 1), MouseButton.Left);
        f.Window.MouseMove(f.At(top, 5), RawInputModifiers.LeftMouseButton);
        Assert.Null(f.Viewer.ActiveSelectionAutoScroll);

        f.Window.MouseMove(new Point(100, ViewerTop + 100), RawInputModifiers.LeftMouseButton);
        f.Window.MouseMove(f.At(top, 5), RawInputModifiers.LeftMouseButton);
        Assert.NotNull(f.Viewer.ActiveSelectionAutoScroll);
    }

    [AvaloniaFact]
    public void A_drag_that_starts_in_an_edge_band_scrolls_once_past_the_edge()
    {
        using var f = Show();
        var top = f.ScrollParagraphTo(300, 10);
        f.Window.MouseDown(f.At(top, 1), MouseButton.Left);
        f.Window.MouseMove(f.At(top, 5), RawInputModifiers.LeftMouseButton);

        f.Window.MouseMove(new Point(100, ViewerTop - 1), RawInputModifiers.LeftMouseButton);
        var scroll = f.Viewer.ActiveSelectionAutoScroll!;
        var offset = f.ScrollViewer.Offset.Y;
        scroll.Step(Start);
        scroll.Step(Start + TimeSpan.FromMilliseconds(100));

        Assert.Equal(offset - 25 * SelectionAutoScroll.SpeedPerPixel * 0.1, f.ScrollViewer.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void Each_drag_starts_with_the_edge_bands_ignored_again()
    {
        using var f = Show();
        var top = f.ScrollParagraphTo(300, 10);
        f.Window.MouseDown(new Point(100, ViewerTop + 100), MouseButton.Left);
        f.Window.MouseMove(new Point(100, ViewerTop + 110), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(new Point(100, ViewerTop + 110), MouseButton.Left);

        f.Window.MouseDown(f.At(top, 1), MouseButton.Left);
        f.Window.MouseMove(f.At(top, 5), RawInputModifiers.LeftMouseButton);

        Assert.Null(f.Viewer.ActiveSelectionAutoScroll);
    }

    [AvaloniaFact]
    public void Scrolling_stops_at_the_end_of_the_document_and_goes_on_while_the_pointer_stays()
    {
        using var f = Show();
        var end = f.ScrollViewer.Extent.Height - f.ScrollViewer.Viewport.Height;
        f.ScrollViewer.Offset = new Vector(0, end - 10);
        f.Window.UpdateLayout();
        f.StartDrag(f.ParagraphsInView().Min() + 1);

        f.Window.MouseMove(new Point(300, ViewerBottom + 26), RawInputModifiers.LeftMouseButton);
        var scroll = f.Viewer.ActiveSelectionAutoScroll!;
        scroll.Step(Start);
        Assert.True(scroll.Step(Start + TimeSpan.FromMilliseconds(100)));
        f.Window.UpdateLayout();

        Assert.Equal(end, f.ScrollViewer.Offset.Y, 3);
        Assert.EndsWith("Paragraph 39", f.Viewer.SelectedText);
        Assert.Same(scroll, f.Viewer.ActiveSelectionAutoScroll);
    }

    [AvaloniaFact]
    public void A_frame_that_comes_late_scrolls_no_further_than_a_tenth_of_a_second()
    {
        using var f = Show();
        f.StartDrag(0);

        f.Window.MouseMove(new Point(300, ViewerBottom + 26), RawInputModifiers.LeftMouseButton);
        var scroll = f.Viewer.ActiveSelectionAutoScroll!;
        scroll.Step(Start);
        scroll.Step(Start + TimeSpan.FromSeconds(2));

        Assert.Equal(50 * SelectionAutoScroll.SpeedPerPixel * 0.1, f.ScrollViewer.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void Moving_back_away_from_the_edges_stops_scrolling()
    {
        using var f = Show();
        f.StartDrag(0);
        f.Window.MouseMove(new Point(300, ViewerBottom + 26), RawInputModifiers.LeftMouseButton);
        var scroll = f.Viewer.ActiveSelectionAutoScroll!;
        scroll.Step(Start);

        f.Window.MouseMove(new Point(100, ViewerTop + 100), RawInputModifiers.LeftMouseButton);

        Assert.False(scroll.Step(Start + TimeSpan.FromMilliseconds(50)));
        Assert.Equal(0, f.ScrollViewer.Offset.Y);
        Assert.Null(f.Viewer.ActiveSelectionAutoScroll);
    }

    [AvaloniaFact]
    public void Releasing_the_button_stops_scrolling()
    {
        using var f = Show();
        f.StartDrag(0);
        f.Window.MouseMove(new Point(300, ViewerBottom + 26), RawInputModifiers.LeftMouseButton);
        var scroll = f.Viewer.ActiveSelectionAutoScroll!;
        scroll.Step(Start);

        f.Window.MouseUp(new Point(300, ViewerBottom + 26), MouseButton.Left);

        Assert.Null(f.Viewer.ActiveSelectionAutoScroll);
        Assert.False(scroll.Step(Start + TimeSpan.FromMilliseconds(50)));
        Assert.Equal(0, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public void Moving_the_pointer_near_an_edge_without_a_drag_scrolls_nothing()
    {
        using var f = Show();

        f.Window.MouseMove(new Point(100, ViewerBottom - 4));
        f.Window.MouseDown(f.At(f.Blocks[0], 1), MouseButton.Left);
        // Within the drag threshold, it is still a click.
        f.Window.MouseMove(f.At(f.Blocks[0], 1) + new Vector(0, 1), RawInputModifiers.LeftMouseButton);

        Assert.Null(f.Viewer.ActiveSelectionAutoScroll);
    }

    [AvaloniaFact]
    public void The_selection_follows_the_text_under_a_still_pointer_when_the_document_scrolls_during_a_drag()
    {
        using var f = Show();
        f.StartDrag(0);
        var pointer = new Point(100, ViewerTop + 100);
        f.Window.MouseMove(pointer, RawInputModifiers.LeftMouseButton);
        var before = f.Viewer.SelectedText;

        // As the mouse wheel does.
        f.ScrollViewer.Offset = new Vector(0, 200);
        f.Window.UpdateLayout();

        Assert.True(f.Viewer.SelectedText.Length > before.Length);
        Assert.StartsWith(before, f.Viewer.SelectedText);
        Assert.Null(f.Viewer.ActiveSelectionAutoScroll);
    }

    [AvaloniaFact]
    public void Scrolling_after_the_drag_leaves_the_selection_as_it_was()
    {
        using var f = Show();
        f.StartDrag(0);
        f.Window.MouseMove(new Point(100, ViewerTop + 100), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(new Point(100, ViewerTop + 100), MouseButton.Left);
        var selected = f.Viewer.SelectedText;

        f.ScrollViewer.Offset = new Vector(0, 200);
        f.Window.UpdateLayout();

        Assert.Equal(selected, f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void Moving_within_an_edge_band_while_scrolling_asks_for_no_second_run_of_frames()
    {
        using var f = Show();
        var requests = 0;
        f.Viewer.RequestAnimationFrame = _ => requests++;
        f.StartDrag(0);
        f.Window.MouseMove(new Point(300, ViewerTop + 100), RawInputModifiers.LeftMouseButton);

        f.Window.MouseMove(new Point(300, ViewerBottom + 26), RawInputModifiers.LeftMouseButton);
        f.Window.MouseMove(new Point(300, ViewerBottom + 40), RawInputModifiers.LeftMouseButton);
        f.Window.MouseMove(new Point(300, ViewerBottom - 4), RawInputModifiers.LeftMouseButton);

        Assert.Equal(1, requests);
    }

    [AvaloniaFact]
    public void Scrolling_again_after_moving_away_from_the_edges_starts_its_clock_again()
    {
        using var f = Show();
        f.StartDrag(0);
        f.Window.MouseMove(new Point(300, ViewerBottom + 26), RawInputModifiers.LeftMouseButton);
        var scroll = f.Viewer.ActiveSelectionAutoScroll!;
        scroll.Step(Start);
        f.Window.MouseMove(new Point(300, ViewerTop + 100), RawInputModifiers.LeftMouseButton);
        Assert.False(scroll.Step(Start + TimeSpan.FromMilliseconds(10)));

        f.Window.MouseMove(new Point(300, ViewerBottom + 26), RawInputModifiers.LeftMouseButton);
        var later = Start + TimeSpan.FromSeconds(5);
        scroll.Step(later);
        scroll.Step(later + TimeSpan.FromMilliseconds(20));

        Assert.Same(scroll, f.Viewer.ActiveSelectionAutoScroll);
        Assert.Equal(50 * SelectionAutoScroll.SpeedPerPixel * 0.02, f.ScrollViewer.Offset.Y, 3);
    }

    // The document leaves the old template's tree, which takes the pointer capture from it and so ends the drag.
    [AvaloniaFact]
    public void A_new_template_stops_the_scrolling_of_the_old_one()
    {
        using var f = Show();
        f.StartDrag(0);
        f.Window.MouseMove(new Point(300, ViewerBottom + 26), RawInputModifiers.LeftMouseButton);
        var scroll = f.Viewer.ActiveSelectionAutoScroll!;

        f.Viewer.Template = ScrollingViewerTemplate.Create();
        f.Window.UpdateLayout();

        Assert.False(scroll.IsRunning);
        Assert.Null(f.Viewer.ActiveSelectionAutoScroll);
    }

    [AvaloniaFact]
    public async Task Scrolling_runs_on_the_rendered_frames()
    {
        using var f = Show(realFrames: true);
        // See MarkdownViewerSmoothScrollTests.Smooth_scroll_runs_on_the_rendered_frames: the window's first render
        // is committed first, so that the frames come from the dispatcher's loop.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        f.StartDrag(0);

        f.Window.MouseMove(new Point(300, ViewerBottom + 26), RawInputModifiers.LeftMouseButton);
        await AsyncTestHelpers.WaitUntilAsync(() => f.ScrollViewer.Offset.Y > 100, timeoutMs: 10_000);
        // The selection follows in the layout pass after the frame that moved the offset.
        f.Window.UpdateLayout();

        Assert.Contains($"Paragraph {f.ParagraphsInView().Max()}", f.Viewer.SelectedText);
        f.Window.MouseUp(new Point(300, ViewerBottom + 26), MouseButton.Left);
    }
}
