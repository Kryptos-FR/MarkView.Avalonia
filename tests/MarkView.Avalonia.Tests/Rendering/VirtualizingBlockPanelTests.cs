// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// <see cref="VirtualizingBlockPanel"/> on its own, in a scroll viewer, with blocks of known sizes.
/// </summary>
public class VirtualizingBlockPanelTests
{
    private sealed class Fixture : IDisposable
    {
        public VirtualizingBlockPanel Panel { get; } = new() { Spacing = 8 };
        public ScrollViewer ScrollViewer { get; }
        public Window Window { get; }

        public Fixture(int count, double height = 50, double width = 400)
        {
            for (var i = 0; i < count; i++)
                Panel.Children.Add(new Border { Height = height, Tag = i });
            ScrollViewer = new ScrollViewer { Content = Panel, Width = width, Height = 300, VerticalAlignment = VerticalAlignment.Top };
            Window = new Window { Width = 1000, Height = 1000, Content = ScrollViewer };
            Window.Show();
            Settle();
        }

        public bool IsAttached(int index) => Panel.Children[index].GetVisualParent() == Panel;

        public void ScrollTo(double y)
        {
            ScrollViewer.Offset = new Vector(0, y);
            Settle();
        }

        public void Settle()
        {
            Window.UpdateLayout();
            Window.UpdateLayout();
        }

        public void Dispose() => Window.Close();
    }

    [AvaloniaFact]
    public void Attached_blocks_are_stacked_with_the_spacing_between_them()
    {
        using var f = new Fixture(10);

        Assert.Equal([0, 58, 116], f.Panel.Children.Take(3).Select(c => c.Bounds.Top));
        Assert.Equal(10 * 50 + 9 * 8, f.Panel.Bounds.Height);
    }

    [AvaloniaFact]
    public void Blocks_never_laid_out_take_the_height_of_those_that_were()
    {
        using var f = new Fixture(1000);

        Assert.False(f.IsAttached(999));
        Assert.Equal(1000 * 50 + 999 * 8, f.Panel.Bounds.Height, 1);
    }

    [AvaloniaFact]
    public void A_block_keeps_the_height_it_was_laid_out_with_once_detached()
    {
        using var f = new Fixture(200);
        f.Panel.Children[1].Height = 500;
        f.Settle();
        var height = f.Panel.Bounds.Height;

        f.ScrollTo(f.Panel.Bounds.Height);

        Assert.False(f.IsAttached(1));
        Assert.Equal(height, f.Panel.Bounds.Height, 1);
    }

    [AvaloniaFact]
    public void Its_width_is_that_of_its_widest_block_laid_out()
    {
        using var f = new Fixture(3);
        var wide = (Border)f.Panel.Children[1];
        wide.Width = 300;
        wide.HorizontalAlignment = HorizontalAlignment.Left;

        f.Panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Assert.Equal(300, f.Panel.DesiredSize.Width);
    }

    [AvaloniaFact]
    public void A_taller_viewport_attaches_more_blocks()
    {
        using var f = new Fixture(200);

        f.ScrollViewer.Height = 900;
        f.Settle();

        // The 900 pixels in view and as many below them: 31 blocks of 50 with 8 between.
        Assert.Equal(Enumerable.Range(0, 32), f.Panel.AttachedChildren.Select(c => (int)c.Tag!));
    }

    [AvaloniaFact]
    public void A_scroll_within_the_lead_measures_nothing_new()
    {
        using var f = new Fixture(200);
        var attached = f.Panel.AttachedChildren.ToList();

        f.ScrollTo(100);

        Assert.True(f.Panel.IsMeasureValid);
        Assert.Equal(attached, f.Panel.AttachedChildren);
    }

    [AvaloniaFact]
    public void A_panel_out_of_view_keeps_the_blocks_it_attached()
    {
        var panel = new VirtualizingBlockPanel();
        for (var i = 0; i < 200; i++)
            panel.Children.Add(new Border { Height = 50 });
        var scrollViewer = new ScrollViewer
        {
            Content = new StackPanel { Children = { new Border { Height = 400 }, new Border { Height = 300, Child = new ScrollViewer { Content = panel } } } },
        };
        var window = new Window { Width = 400, Height = 300, Content = scrollViewer };
        window.Show();
        window.UpdateLayout();
        // Out of view below: no viewport, so the top of the document is attached as when nothing is known.
        var attached = panel.AttachedChildren.ToList();
        Assert.Contains(panel.Children[0], attached);

        scrollViewer.Offset = new Vector(0, 400);
        window.UpdateLayout();
        scrollViewer.Offset = new Vector(0, 0);
        window.UpdateLayout();

        Assert.Equal(attached, panel.AttachedChildren);
        window.Close();
    }

    [AvaloniaFact]
    public void Realize_attaches_a_block_far_away_in_document_order_and_lets_go_once_it_has_been_in_view()
    {
        using var f = new Fixture(200);
        var target = (Border)f.Panel.Children[150];
        var child = new Border();
        target.Child = child;

        Assert.True(f.Panel.Realize(child));
        Assert.False(f.Panel.Realize(child));
        f.Settle();

        Assert.True(f.IsAttached(150));
        Assert.Same(target, f.Panel.AttachedChildren[^1]);
        Assert.Equal(f.Panel.AttachedChildren.OrderBy(c => (int)c.Tag!), f.Panel.AttachedChildren);

        f.ScrollTo(target.Bounds.Top);
        f.ScrollTo(0);

        Assert.False(f.IsAttached(150));
    }

    [AvaloniaFact]
    public void A_block_realized_before_those_attached_goes_first()
    {
        using var f = new Fixture(200);
        f.ScrollTo(f.Panel.Bounds.Height);

        Assert.True(f.Panel.Realize(f.Panel.Children[5]));

        Assert.Same(f.Panel.Children[5], f.Panel.AttachedChildren[0]);
    }

    [AvaloniaFact]
    public void Only_a_block_of_the_panel_can_be_realized()
    {
        using var f = new Fixture(3);

        Assert.False(f.Panel.Realize(new Border()));
        Assert.Null(f.Panel.BlockOf(new Border()));
        Assert.False(f.Panel.IsAttached(new Border()));
    }

    [AvaloniaFact]
    public void Removing_a_block_detaches_it()
    {
        using var f = new Fixture(10);
        var first = f.Panel.Children[0];

        f.Panel.Children.RemoveAt(0);
        f.Settle();

        Assert.Null(first.GetVisualParent());
        Assert.Equal(9, f.Panel.AttachedChildren.Count);
        Assert.False(f.Panel.IsAttached(first));
    }

    [AvaloniaFact]
    public void A_narrower_width_lays_the_blocks_out_again()
    {
        using var f = new Fixture(10);
        var text = new TextBlock { Text = string.Join(' ', Enumerable.Repeat("word", 60)), TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        f.Panel.Children.Add(text);
        f.Settle();
        var height = text.Bounds.Height;

        f.ScrollViewer.Width = 150;
        f.Settle();

        Assert.True(text.Bounds.Height > height);
    }
}
