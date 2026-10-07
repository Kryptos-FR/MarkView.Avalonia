// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerInteractionTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer, MarkdownSelectableTextBlock[] Blocks) : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    // A text block only receives pointer input where it paints; the headless text drawing
    // contributes no hit-testable geometry, so a transparent background makes each block's
    // whole row hit-testable, as it is under a real renderer.
    private static void MakeTextBlocksHitTestable(Window window) =>
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });

    private static Fixture Show(string markdown)
    {
        var viewer = new MarkdownViewer { Markdown = markdown };
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        MakeTextBlocksHitTestable(window);
        window.Show();
        return new Fixture(window, viewer, viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().ToArray());
    }

    private static Point At(Window window, TextBlock block, int index)
    {
        var caret = block.TextLayout.HitTestTextPosition(index);
        var origin = block.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(origin.X + block.Padding.Left + caret.X + 0.5, origin.Y + block.Padding.Top + caret.Y + caret.Height / 2);
    }

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public void Dragging_across_paragraphs_selects_the_spanned_text()
    {
        using var f = Show("First paragraph\n\nSecond paragraph");

        f.Window.MouseDown(At(f.Window, f.Blocks[0], 3), MouseButton.Left);
        f.Window.MouseMove(At(f.Window, f.Blocks[1], 5), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(At(f.Window, f.Blocks[1], 5), MouseButton.Left);

        Assert.Equal("st paragraph\nSecon", f.Viewer.GetSelectedText());
        Assert.True(f.Viewer.IsFocused);
    }

    [AvaloniaFact]
    public void Click_with_tiny_jitter_on_a_link_raises_LinkClicked_and_selects_nothing()
    {
        using var f = Show("Intro\n\n[link](https://example.com/) tail");
        var urls = new List<string>();
        f.Viewer.LinkClicked += (_, e) => { urls.Add(e.Url); e.Handled = true; };
        var start = At(f.Window, f.Blocks[1], 1);

        f.Window.MouseDown(start, MouseButton.Left);
        f.Window.MouseMove(start + new Vector(1, 0), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(start + new Vector(1, 0), MouseButton.Left);

        Assert.Equal(["https://example.com/"], urls);
        Assert.Equal(string.Empty, f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Dragging_over_a_link_selects_instead_of_clicking()
    {
        using var f = Show("Intro\n\n[link](https://example.com/) tail");
        var clicks = 0;
        f.Viewer.LinkClicked += (_, e) => { clicks++; e.Handled = true; };

        f.Window.MouseDown(At(f.Window, f.Blocks[1], 0), MouseButton.Left);
        f.Window.MouseMove(At(f.Window, f.Blocks[1], 3), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(At(f.Window, f.Blocks[1], 3), MouseButton.Left);

        Assert.Equal(0, clicks);
        Assert.Equal("lin", f.Viewer.GetSelectedText());
        Assert.Null(f.Blocks[1].Cursor);
    }

    [AvaloniaFact]
    public void Moving_exactly_the_drag_threshold_is_still_a_click()
    {
        using var f = Show("Intro\n\n[link](https://example.com/) tail");
        var urls = new List<string>();
        f.Viewer.LinkClicked += (_, e) => { urls.Add(e.Url); e.Handled = true; };
        var start = At(f.Window, f.Blocks[1], 1);

        f.Window.MouseDown(start, MouseButton.Left);
        f.Window.MouseMove(start + new Vector(3, 0), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(start + new Vector(3, 0), MouseButton.Left);

        Assert.Equal(["https://example.com/"], urls);
    }

    [AvaloniaFact]
    public void Moving_past_the_drag_threshold_vertically_turns_the_press_into_a_drag()
    {
        using var f = Show("Intro\n\n[link](https://example.com/) tail");
        var clicks = 0;
        f.Viewer.LinkClicked += (_, e) => { clicks++; e.Handled = true; };
        var start = At(f.Window, f.Blocks[1], 1);
        var end = start + new Vector(0, 4);
        Assert.True(end.Y < f.Blocks[1].TranslatePoint(new Point(0, f.Blocks[1].Bounds.Height), f.Window)!.Value.Y);

        f.Window.MouseDown(start, MouseButton.Left);
        f.Window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(end, MouseButton.Left);

        Assert.Equal(0, clicks);
    }

    [AvaloniaFact]
    public void Drag_moves_are_handled_but_hover_moves_are_not()
    {
        using var f = Show("First paragraph\n\nSecond paragraph");
        var bubbled = 0;
        f.Viewer.AddHandler(InputElement.PointerMovedEvent, (_, _) => bubbled++, RoutingStrategies.Bubble);

        f.Window.MouseMove(At(f.Window, f.Blocks[0], 2));
        Assert.Equal(1, bubbled);

        f.Window.MouseDown(At(f.Window, f.Blocks[0], 3), MouseButton.Left);
        f.Window.MouseMove(At(f.Window, f.Blocks[1], 5), RawInputModifiers.LeftMouseButton);
        Assert.Equal(1, bubbled);
    }

    [AvaloniaFact]
    public void Right_button_press_keeps_the_current_selection()
    {
        using var f = Show("Alpha\n\nBeta");
        f.Viewer.SelectAll();

        f.Window.MouseDown(At(f.Window, f.Blocks[1], 1), MouseButton.Right);
        f.Window.MouseUp(At(f.Window, f.Blocks[1], 1), MouseButton.Right);

        Assert.Equal("Alpha\nBeta", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Hovering_a_link_shows_the_hand_cursor_and_plain_text_resets_it()
    {
        using var f = Show("Intro\n\n[link](https://example.com/) plain words");

        f.Window.MouseMove(At(f.Window, f.Blocks[1], 1));
        Assert.NotSame(Cursor.Default, f.Blocks[1].Cursor);
        Assert.NotNull(f.Blocks[1].Cursor);

        f.Window.MouseMove(At(f.Window, f.Blocks[1], 8));
        Assert.Same(Cursor.Default, f.Blocks[1].Cursor);
    }

    [AvaloniaFact]
    public async Task Select_all_and_copy_hotkeys_select_and_copy_the_document()
    {
        using var f = Show("Alpha\n\nBeta");
        var hotkeys = Application.Current!.PlatformSettings!.HotkeyConfiguration;
        var selectAll = hotkeys.SelectAll.First();
        var copy = hotkeys.Copy.First();
        f.Viewer.Focus();

        f.Window.KeyPress(selectAll.Key, (RawInputModifiers)selectAll.KeyModifiers, PhysicalKey.None, null);
        Assert.Equal("Alpha\nBeta", f.Viewer.GetSelectedText());

        f.Window.KeyPress(copy.Key, (RawInputModifiers)copy.KeyModifiers, PhysicalKey.None, null);
        await Dispatcher.UIThread.InvokeAsync(() => { });
        Assert.Equal("Alpha\nBeta", await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public void Handled_hotkeys_do_not_bubble_but_other_keys_do()
    {
        using var f = Show("Alpha\n\nBeta");
        var hotkeys = Application.Current!.PlatformSettings!.HotkeyConfiguration;
        var bubbled = new List<Key>();
        f.Window.AddHandler(InputElement.KeyDownEvent, (_, e) => bubbled.Add(e.Key), RoutingStrategies.Bubble);
        f.Viewer.Focus();

        foreach (var gesture in hotkeys.SelectAll.Concat(hotkeys.Copy))
            f.Window.KeyPress(gesture.Key, (RawInputModifiers)gesture.KeyModifiers, PhysicalKey.None, null);
        Assert.Empty(bubbled);

        f.Window.KeyPress(Key.Q, RawInputModifiers.None, PhysicalKey.None, null);
        Assert.Equal([Key.Q], bubbled);
    }

    [AvaloniaFact]
    public void CopyToClipboardAsync_outside_a_window_is_a_no_op()
    {
        var viewer = new MarkdownViewer { Markdown = "Alpha" };
        viewer.SelectAll();

        Assert.True(viewer.CopyToClipboardAsync().IsCompletedSuccessfully);
    }

    [AvaloniaFact]
    public void Same_document_link_scrolls_to_anchor_instead_of_raising_LinkClicked()
    {
        var viewer = new MarkdownViewer();
        viewer.Template = ScrollingViewerTemplate.Create();
        viewer.Markdown = "[go](#target-heading)\n\n"
            + string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Paragraph {i}"))
            + "\n\n## Target Heading\n\n"
            + string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Trailing paragraph {i}"));
        var window = new Window { Width = 400, Height = 200, Content = viewer };
        MakeTextBlocksHitTestable(window);
        window.Show();
        try
        {
            var clicks = 0;
            viewer.LinkClicked += (_, _) => clicks++;
            var scrollViewer = viewer.GetVisualDescendants().OfType<ScrollViewer>().Single();
            var link = viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().First();
            var heading = viewer.GetVisualDescendants().OfType<TextBlock>().Single(tb => tb.Classes.Contains("markdown-h2"));
            var expectedY = heading.TranslatePoint(new Point(0, 0), scrollViewer)!.Value.Y - 16;

            Click(window, At(window, link, 0));

            Assert.Equal(0, clicks);
            Assert.Equal(expectedY, scrollViewer.Offset.Y, 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("target-heading", true)]
    [InlineData("no-such-anchor", false)]
    public void ScrollToAnchor_without_a_templated_scroll_viewer_brings_the_anchor_into_view(string anchor, bool scrolls)
    {
        var viewer = new MarkdownViewer
        {
            Markdown = string.Join("\n\n", Enumerable.Range(0, 40).Select(i => $"Paragraph {i}")) + "\n\n## Target Heading",
        };
        var outer = new ScrollViewer { Content = viewer };
        var window = new Window { Width = 400, Height = 200, Content = outer };
        window.Show();
        try
        {
            viewer.ScrollToAnchor(anchor);
            window.UpdateLayout();

            Assert.Equal(scrolls, outer.Offset.Y > 0);
        }
        finally
        {
            window.Close();
        }
    }
}
