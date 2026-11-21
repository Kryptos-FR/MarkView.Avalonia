// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// A press beside the text (past a line's end, between blocks, above or below everything) starts
/// the selection at the nearest text, as in a browser; a press a control in the document takes starts none.
/// </summary>
public class MarkdownViewerSelectBesideTextTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
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

        /// <summary>The top and bottom of <paramref name="block"/> in the window.</summary>
        public (double Top, double Bottom) Extent(Control block)
        {
            var top = block.TranslatePoint(default, Window)!.Value.Y;
            return (top, top + block.Bounds.Height);
        }

        public void Drag(Point from, Point to)
        {
            Window.MouseDown(from, MouseButton.Left);
            Window.MouseMove(from + new Vector(0, 5), RawInputModifiers.LeftMouseButton);
            Window.MouseMove(to, RawInputModifiers.LeftMouseButton);
            Window.MouseUp(to, MouseButton.Left);
        }

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown, Thickness padding = default)
    {
        var viewer = new MarkdownViewer
        {
            Padding = padding,
            Markdown = markdown,
        };
        viewer.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml"),
        });
        var window = new Window { Width = 500, Height = 400, Content = viewer };
        // The headless text drawing has no hit-testable glyphs; a transparent background stands in.
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        window.Show();
        window.UpdateLayout();
        return new Fixture(window, viewer);
    }

    private static Point InRightPadding(Fixture fixture, double y)
    {
        var origin = fixture.Viewer.TranslatePoint(default, fixture.Window)!.Value;
        return new Point(origin.X + fixture.Viewer.Bounds.Width - fixture.Viewer.Padding.Right / 2, y);
    }

    [AvaloniaFact]
    public void A_press_past_the_end_of_a_short_line_starts_at_its_end()
    {
        using var f = Show("Short\n\nSecond paragraph", new Thickness(0, 0, 180, 0));
        var block = f.Blocks[0];
        var end = f.At(block, 5);
        var press = InRightPadding(f, end.Y);
        var blockRight = block.TranslatePoint(new Point(block.Bounds.Width, 0), f.Window)!.Value.X;
        Assert.True(press.X > blockRight, "the press is outside the paragraph's TextBlock");

        f.Drag(press, f.At(f.Blocks[1], 6));

        Assert.Equal("\nSecond", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void A_press_in_the_right_padding_of_an_rtl_paragraph_starts_at_its_logical_start()
    {
        using var f = Show("שלום עולם", new Thickness(0, 0, 180, 0));
        var block = f.Blocks[0];
        var press = InRightPadding(f, f.At(block, 0).Y);

        f.Drag(press, f.At(block, 4));

        Assert.Equal("שלום", f.Viewer.SelectedText);
    }

    [AvaloniaTheory]
    [InlineData("<p align=\"center\">Centre text</p>", 7)]
    [InlineData("<p align=\"right\">Right text</p>", 6)]
    public void A_press_in_the_right_padding_of_an_aligned_paragraph_starts_at_its_end(string markdown, int target)
    {
        using var f = Show(markdown, new Thickness(0, 0, 180, 0));
        var block = Assert.Single(f.Blocks);
        var press = InRightPadding(f, f.At(block, 0).Y);

        f.Drag(press, f.At(block, target));

        Assert.Equal("text", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void A_parent_sees_a_press_in_the_viewer_padding_unhandled()
    {
        using var f = Show("Short\n\nSecond paragraph", new Thickness(0, 0, 180, 0));
        var sawPress = false;
        var wasHandled = true;
        f.Window.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            sawPress = true;
            wasHandled = e.Handled;
        }, RoutingStrategies.Bubble);
        var press = InRightPadding(f, f.At(f.Blocks[0], 5).Y);

        f.Drag(press, f.At(f.Blocks[1], 6));

        Assert.True(sawPress);
        Assert.False(wasHandled);
        Assert.Equal("\nSecond", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void A_press_between_two_paragraphs_starts_at_the_nearer_one()
    {
        using var f = Show("First paragraph\n\nSecond paragraph");
        var (_, firstBottom) = f.Extent(f.Blocks[0]);
        var (secondTop, _) = f.Extent(f.Blocks[1]);
        Assert.True(secondTop - firstBottom > 2, "the paragraphs have a gap between them");

        f.Drag(new Point(20, secondTop - 1), f.At(f.Blocks[1], 6));

        Assert.Equal("Second", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void A_press_below_all_text_starts_at_the_end_of_the_document()
    {
        using var f = Show("First paragraph\n\nSecond paragraph");
        var (_, bottom) = f.Extent(f.Blocks[1]);

        f.Drag(new Point(20, bottom + 50), f.At(f.Blocks[1], 7));

        Assert.Equal("paragraph", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void A_drag_through_a_gap_ends_at_the_nearest_text()
    {
        using var f = Show("First paragraph\n\nSecond paragraph");
        var (_, firstBottom) = f.Extent(f.Blocks[0]);

        f.Drag(f.At(f.Blocks[0], 6), new Point(20, firstBottom + 1));

        Assert.Equal("paragraph", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void A_click_beside_the_text_without_dragging_clears_the_selection_and_selects_nothing()
    {
        using var f = Show("First paragraph\n\nSecond paragraph");
        f.Viewer.SelectAll();
        var end = f.At(f.Blocks[0], 15);

        f.Window.MouseDown(new Point(450, end.Y), MouseButton.Left);
        f.Window.MouseUp(new Point(450, end.Y), MouseButton.Left);

        Assert.Equal(string.Empty, f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void A_press_on_the_copy_button_of_a_code_block_starts_no_selection()
    {
        using var f = Show("Before\n\n```\ncode\n```\n\nAfter");
        f.Viewer.ShowCodeBlockCopyButton = true;
        f.Window.UpdateLayout();
        var button = f.Viewer.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible);
        var centre = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), f.Window)!.Value;

        f.Drag(centre, f.At(f.Blocks[^1], 3));

        Assert.Equal(string.Empty, f.Viewer.SelectedText);
    }
}
