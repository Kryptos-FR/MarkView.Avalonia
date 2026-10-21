// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>Scrolling the document with the keyboard while the viewer itself has the focus.</summary>
public class MarkdownViewerKeyboardScrollingTests
{
    private static readonly string LongMarkdown =
        "[A link](https://example.com)\n\n" + string.Join("\n\n", Enumerable.Range(0, 80).Select(i => $"Paragraph {i}"));

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, ScrollViewer ScrollViewer) : IDisposable
    {
        /// <summary>Whether the last key pressed was handled, by anyone.</summary>
        public bool LastKeyHandled { get; set; }

        public void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPress(key, modifiers, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
        }

        public double Bottom => ScrollViewer.Extent.Height - ScrollViewer.Viewport.Height;

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown = "", bool focus = true)
    {
        var viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create(), Markdown = markdown };
        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();
        var f = new Fixture(window, viewer, viewer.GetVisualDescendants().OfType<ScrollViewer>().Single());
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => f.LastKeyHandled = e.Handled, RoutingStrategies.Bubble, true);
        if (focus)
            viewer.Focus();
        return f;
    }

    [AvaloniaFact]
    public void Down_and_Up_scroll_by_a_line()
    {
        using var f = Show(LongMarkdown);

        f.Press(Key.Down);
        var line = f.ScrollViewer.Offset.Y;
        Assert.True(f.LastKeyHandled);
        Assert.InRange(line, 1, f.ScrollViewer.Viewport.Height / 2);

        f.Press(Key.Down);
        Assert.Equal(2 * line, f.ScrollViewer.Offset.Y, 3);

        f.Press(Key.Up);
        Assert.Equal(line, f.ScrollViewer.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void PageDown_and_PageUp_scroll_by_a_page()
    {
        using var f = Show(LongMarkdown);

        f.Press(Key.PageDown);
        Assert.Equal(f.ScrollViewer.Viewport.Height, f.ScrollViewer.Offset.Y, 3);
        f.Press(Key.PageDown);
        Assert.Equal(2 * f.ScrollViewer.Viewport.Height, f.ScrollViewer.Offset.Y, 3);

        f.Press(Key.PageUp);
        Assert.Equal(f.ScrollViewer.Viewport.Height, f.ScrollViewer.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void Space_scrolls_down_a_page_and_Shift_Space_up_a_page()
    {
        using var f = Show(LongMarkdown);

        f.Press(Key.Space);
        f.Press(Key.Space);
        Assert.Equal(2 * f.ScrollViewer.Viewport.Height, f.ScrollViewer.Offset.Y, 3);

        f.Press(Key.Space, RawInputModifiers.Shift);
        Assert.Equal(f.ScrollViewer.Viewport.Height, f.ScrollViewer.Offset.Y, 3);
    }

    [AvaloniaTheory]
    [InlineData(RawInputModifiers.None)]
    [InlineData(RawInputModifiers.Control)]
    public void End_and_Home_scroll_to_the_bottom_and_the_top(RawInputModifiers modifiers)
    {
        using var f = Show(LongMarkdown);

        f.Press(Key.End, modifiers);
        Assert.True(f.Bottom > 0);
        Assert.Equal(f.Bottom, f.ScrollViewer.Offset.Y, 3);

        f.Press(Key.Home, modifiers);
        Assert.Equal(0, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public void Left_and_Right_scroll_a_document_wider_than_the_viewer()
    {
        using var f = Show(LongMarkdown);
        f.ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        Assert.IsType<Grid>(f.Viewer.Content).MinWidth = 2000;
        f.Window.UpdateLayout();

        f.Press(Key.Right);
        var line = f.ScrollViewer.Offset.X;
        Assert.True(line > 0);

        f.Press(Key.Left);
        Assert.Equal(0, f.ScrollViewer.Offset.X);
        Assert.True(f.LastKeyHandled);
    }

    [AvaloniaTheory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    public void Left_and_Right_are_left_to_the_parents_when_the_document_is_not_wider(Key key)
    {
        using var f = Show(LongMarkdown);

        f.Press(key);

        Assert.False(f.LastKeyHandled);
    }

    [AvaloniaTheory]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.PageUp)]
    [InlineData(Key.PageDown)]
    [InlineData(Key.Space)]
    [InlineData(Key.Home)]
    [InlineData(Key.End)]
    public void Keys_are_left_to_the_parents_when_the_document_fits(Key key)
    {
        using var f = Show("Short.");

        f.Press(key);

        Assert.False(f.LastKeyHandled);
        Assert.Equal(0, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaTheory]
    [InlineData(Key.Down, RawInputModifiers.Control)]
    [InlineData(Key.PageDown, RawInputModifiers.Shift)]
    [InlineData(Key.End, RawInputModifiers.Shift)]
    [InlineData(Key.Space, RawInputModifiers.Control)]
    [InlineData(Key.A, RawInputModifiers.None)]
    public void Other_keys_and_modifiers_do_not_scroll(Key key, RawInputModifiers modifiers)
    {
        using var f = Show(LongMarkdown);

        f.Press(key, modifiers);

        Assert.Equal(0, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaTheory]
    [InlineData(Key.Down)]
    [InlineData(Key.PageDown)]
    [InlineData(Key.End)]
    public void A_key_on_a_focused_link_scrolls_once(Key key)
    {
        using var expected = Show(LongMarkdown);
        expected.Press(key);
        using var f = Show(LongMarkdown, focus: false);
        f.Viewer.GetVisualDescendants().OfType<LinkFocusTarget>().Single().Focus();

        f.Press(key);

        // The scroll viewer handles it on its own on the way up; the viewer adds nothing.
        Assert.True(f.ScrollViewer.Offset.Y > 0);
        Assert.Equal(expected.ScrollViewer.Offset.Y, f.ScrollViewer.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void Space_on_a_focused_task_ticks_it_and_Down_scrolls()
    {
        using var f = Show("- [ ] Task\n\n" + LongMarkdown, focus: false);
        f.Viewer.AreTaskListsInteractive = true;
        var toggled = 0;
        f.Viewer.TaskToggled += (_, _) => toggled++;
        f.Viewer.GetVisualDescendants().OfType<ToggleButton>().Single().Focus();

        f.Press(Key.Space);
        f.Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);
        Assert.Equal(1, toggled);
        Assert.Equal(0, f.ScrollViewer.Offset.Y);

        f.Press(Key.Down);
        Assert.True(f.ScrollViewer.Offset.Y > 0);
    }

    private sealed class FakeImage : IImage
    {
        public Size Size { get; } = new(100, 50);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) => context.FillRectangle(Brushes.Transparent, destRect);
    }

    private sealed class FakeLoader : IImageLoader, IMarkViewExtension
    {
        public bool CanLoad(string url) => url.StartsWith("fake:", StringComparison.Ordinal);
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult<IImage?>(new FakeImage());
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, this);
    }

    [AvaloniaFact]
    public async Task The_keys_do_not_scroll_the_document_behind_the_lightbox()
    {
        var viewer = new MarkdownViewer { IsImageZoomEnabled = true, Template = ScrollingViewerTemplate.Create() };
        viewer.Extensions.Add(new FakeLoader());
        viewer.Markdown = $"![logo](fake://{Guid.NewGuid()}.png)\n\n" + LongMarkdown;
        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();
        try
        {
            var image = viewer.GetVisualDescendants().OfType<Image>().First(i => i.Classes.Contains("markdown-image"));
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
            window.UpdateLayout();
            var point = image.TranslatePoint(new Point(image.Bounds.Width / 2, image.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.NotNull(viewer.Lightbox);

            window.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, viewer.GetVisualDescendants().OfType<ScrollViewer>().First().Offset.Y);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_template_without_a_scroll_viewer_leaves_the_keys_to_the_parents()
    {
        var viewer = new MarkdownViewer
        {
            Template = new FuncControlTemplate((_, _) =>
            {
                var presenter = new ContentPresenter { Name = "PART_ContentPresenter" };
                presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty));
                return presenter;
            }),
            Markdown = LongMarkdown,
        };
        var outer = new ScrollViewer { Content = viewer };
        var window = new Window { Width = 400, Height = 200, Content = outer };
        window.Show();
        viewer.Focus();

        window.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(outer.Viewport.Height, outer.Offset.Y, 3);
        window.Close();
    }
}
