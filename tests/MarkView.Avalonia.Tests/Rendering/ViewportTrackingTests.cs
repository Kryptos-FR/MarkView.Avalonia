// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class ViewportTrackingTests
{
    // A 300 px high viewport over a 1000 px spacer and a 10 px element: the element starts 1000 px
    // down, so it is within a viewport height (300 px) of the visible area from a 400 px offset on.
    private sealed class Scrolled : IDisposable
    {
        public Border Element { get; } = new() { Height = 10 };
        public ScrollViewer ScrollViewer { get; }
        public Window Window { get; }
        public int Realized { get; private set; }

        public Scrolled(bool track = true)
        {
            if (track)
                ViewportTracking.RunWhenNearViewport(Element, () => Realized++);
            ScrollViewer = new ScrollViewer
            {
                Content = new StackPanel { Children = { new Border { Height = 1000 }, Element } },
            };
            Window = new Window { Width = 400, Height = 300, Content = ScrollViewer };
        }

        public void ScrollTo(double y)
        {
            ScrollViewer.Offset = new Vector(0, y);
            Window.UpdateLayout();
        }

        public void Dispose() => Window.Close();
    }

    [AvaloniaFact]
    public void Element_more_than_a_viewport_below_the_visible_area_is_not_realized()
    {
        using var scrolled = new Scrolled();
        scrolled.Window.Show();

        Assert.Equal(0, scrolled.Realized);
    }

    [AvaloniaTheory]
    [InlineData(399, 0)]
    [InlineData(400, 1)]
    [InlineData(1000, 1)]
    public void Element_is_realized_from_one_viewport_height_above_its_top(double offset, int realized)
    {
        using var scrolled = new Scrolled();
        scrolled.Window.Show();

        scrolled.ScrollTo(offset);

        Assert.Equal(realized, scrolled.Realized);
    }

    [AvaloniaFact]
    public void Element_is_realized_only_once()
    {
        using var scrolled = new Scrolled();
        scrolled.Window.Show();

        scrolled.ScrollTo(700);
        scrolled.ScrollTo(0);
        scrolled.ScrollTo(700);

        Assert.Equal(1, scrolled.Realized);
    }

    [AvaloniaFact]
    public void Element_scrolled_past_is_realized_up_to_one_viewport_height_below_its_bottom()
    {
        using var scrolled = new Scrolled();
        scrolled.Window.Show();
        // The document ends 10 px under the element, so the furthest offset leaves it in view;
        // a taller tail lets the element go more than a viewport height above the visible area.
        ((StackPanel)scrolled.ScrollViewer.Content!).Children.Add(new Border { Height = 2000 });
        scrolled.Window.UpdateLayout();

        scrolled.ScrollTo(1311);
        Assert.Equal(0, scrolled.Realized);

        scrolled.ScrollTo(1310);
        Assert.Equal(1, scrolled.Realized);
    }

    [AvaloniaFact]
    public void Element_in_view_is_realized_in_the_layout_pass_that_shows_it()
    {
        var element = new Border();
        ViewportTracking.RunWhenNearViewport(element, () => element.Height = 50);
        var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { element } } };
        try
        {
            window.Show();

            // Laid out with the realized content, with no further layout pass requested.
            Assert.Equal(50, element.Bounds.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Element_never_attached_is_never_realized()
    {
        var realized = 0;
        ViewportTracking.RunWhenNearViewport(new Border(), () => realized++);

        await AsyncTestHelpers.PumpAsync();

        Assert.Equal(0, realized);
    }

    [AvaloniaFact]
    public void Element_attached_after_being_removed_is_realized_where_it_lands()
    {
        using var scrolled = new Scrolled();
        scrolled.Window.Show();
        var panel = (StackPanel)scrolled.ScrollViewer.Content!;

        panel.Children.Remove(scrolled.Element);
        panel.Children.Insert(0, scrolled.Element);
        scrolled.Window.UpdateLayout();

        Assert.Equal(1, scrolled.Realized);
    }

    [Theory]
    [InlineData(0, 0, true)]      // in view
    [InlineData(0, 600, true)]    // a viewport height below: touching
    [InlineData(0, 601, false)]   // further below
    [InlineData(0, -310, true)]   // a viewport height above (the element is 10 high): touching
    [InlineData(0, -311, false)]  // further above
    [InlineData(-400, 0, true)]   // the element's right edge touches the viewport
    [InlineData(-401, 0, false)]  // left of it
    [InlineData(400, 0, true)]    // the element's left edge touches the viewport
    [InlineData(401, 0, false)]   // right of it
    public void Near_viewport_is_within_one_viewport_height_vertically_and_touching_horizontally(double x, double y, bool near)
    {
        // The element, 400 x 10 at the origin of its own coordinates, sees a 400 x 300 viewport at (x, y).
        Assert.Equal(near, ViewportTracking.IsNearViewport(new Size(400, 10), new Rect(-x, -y, 400, 300)));
    }
}
