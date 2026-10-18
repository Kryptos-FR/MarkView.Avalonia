// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>Clicking an image opens it in the viewer's lightbox (<see cref="MarkdownViewer.IsImageZoomEnabled"/>).</summary>
public class MarkdownViewerImageZoomTests
{
    /// <summary>
    /// An image of a fixed size that draws a transparent rectangle where it is shown: the headless
    /// platform draws no bitmap, and an image that draws nothing cannot be hit.
    /// </summary>
    private sealed class FakeImage(double width, double height) : IImage
    {
        public Size Size { get; } = new(width, height);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) => context.FillRectangle(Brushes.Transparent, destRect);
    }

    private sealed class FakeLoader(IImage? image, bool never = false) : IImageLoader
    {
        public bool CanLoad(string url) => url.StartsWith("fake:", StringComparison.Ordinal);
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) =>
            never ? Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => (IImage?)null, TaskScheduler.Default) : Task.FromResult(image);
    }

    private sealed class LoaderExtension(IImageLoader loader) : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, loader);
    }

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, IImage Picture) : IDisposable
    {
        public Image Image => Viewer.GetVisualDescendants().OfType<Image>().First(i => i.Classes.Contains("markdown-image"));
        public ImageLightbox? Lightbox => Viewer.Lightbox;

        public Point Center(Visual visual) =>
            visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window)!.Value;

        public void Dispose() => Window.Close();
    }

    private static string FakeUrl() => $"fake://{Guid.NewGuid()}.png";

    private static async Task<Fixture> ShowAsync(string markdown, double width = 200, double height = 100,
        bool theme = false, Action<MarkdownViewer>? configure = null, IImageLoader? loader = null)
    {
        var picture = new FakeImage(width, height);
        var viewer = new MarkdownViewer();
        viewer.Extensions.Add(new LoaderExtension(loader ?? new FakeLoader(picture)));
        if (theme)
            viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        else
            viewer.Template = ScrollingViewerTemplate.Create();
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        // The headless text drawing contributes no hit-testable glyph geometry; a transparent
        // background makes each paragraph's whole row hit-testable instead.
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        window.Show();
        var f = new Fixture(window, viewer, picture);
        if (loader is null)
        {
            await AsyncTestHelpers.WaitUntilAsync(() => f.Image.Source is not null);
            window.UpdateLayout();
        }
        return f;
    }

    private static void Click(Window window, Point point, MouseButton button = MouseButton.Left)
    {
        window.MouseDown(point, button);
        window.MouseUp(point, button);
    }

    private static void Press(Window window, Key key) => window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);

    private static void RaiseKey(InputElement element, Key key) =>
        element.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    [AvaloniaFact]
    public void IsImageZoomEnabled_defaults_to_true()
    {
        Assert.True(new MarkdownViewer().IsImageZoomEnabled);
    }

    [AvaloniaFact]
    public async Task Clicking_an_image_opens_it_in_a_lightbox_over_the_viewer()
    {
        using var f = await ShowAsync($"Intro\n\n![A diagram]({FakeUrl()})");

        Click(f.Window, f.Center(f.Image));

        var lightbox = Assert.IsType<ImageLightbox>(f.Lightbox);
        Assert.Contains("markdown-image-lightbox", lightbox.Classes);
        Assert.Same(f.Picture, lightbox.Image.Source);
        Assert.Contains("markdown-image-lightbox-image", lightbox.Image.Classes);
        Assert.Equal("A diagram", AutomationProperties.GetName(lightbox));
        Assert.Same(f.Viewer, lightbox.GetVisualParent());
        Assert.Same(f.Viewer, lightbox.Parent);
        Assert.Same(lightbox, f.Viewer.GetVisualChildren().Last());
        Assert.True(lightbox.IsFocused);
        Assert.Equal(string.Empty, f.Viewer.GetSelectedText());

        f.Window.UpdateLayout();
        Assert.Equal(new Rect(f.Viewer.Bounds.Size), lightbox.Bounds);
    }

    [AvaloniaFact]
    public async Task The_lightbox_never_enlarges_an_image_past_its_natural_size()
    {
        using var f = await ShowAsync($"![small]({FakeUrl()})", width: 40, height: 20);

        Click(f.Window, f.Center(f.Image));
        f.Window.UpdateLayout();

        var image = f.Lightbox!.Image;
        Assert.Equal(Stretch.Uniform, image.Stretch);
        Assert.Equal(StretchDirection.DownOnly, image.StretchDirection);
        Assert.Equal(HorizontalAlignment.Center, image.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, image.VerticalAlignment);
        Assert.Equal(new Size(40, 20), image.Bounds.Size);
        Assert.Equal(new Point(180, 140), image.Bounds.Position);
    }

    [AvaloniaFact]
    public async Task The_lightbox_scales_a_large_image_down_to_fit_the_viewer()
    {
        using var f = await ShowAsync($"![large]({FakeUrl()})", width: 1600, height: 600, theme: true);

        Click(f.Window, f.Center(f.Image));
        f.Window.UpdateLayout();

        // The theme leaves a 48-pixel margin around it.
        Assert.Equal(new Size(304, 114), f.Lightbox!.Image.Bounds.Size);
    }

    [AvaloniaFact]
    public async Task An_open_lightbox_does_not_make_the_viewer_larger()
    {
        var picture = new FakeImage(1600, 1200);
        var viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create() };
        viewer.Extensions.Add(new LoaderExtension(new FakeLoader(picture)));
        viewer.Markdown = $"![large]({FakeUrl()} =100x50)";
        var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { viewer } } };
        window.Show();
        var image = viewer.GetVisualDescendants().OfType<Image>().Single();
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        window.UpdateLayout();
        var size = viewer.DesiredSize;

        RaiseKey(image, Key.Enter);
        window.UpdateLayout();

        Assert.NotNull(viewer.Lightbox);
        Assert.Equal(size, viewer.DesiredSize);
        Assert.Equal(new Rect(viewer.Bounds.Size), viewer.Lightbox!.Bounds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_zoomable_image_shows_the_zoom_cursor_and_takes_focus()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");

        Assert.Same(ZoomCursor.Instance, f.Image.Cursor);
        Assert.True(f.Image.Focusable);
        Assert.Equal("A diagram", AutomationProperties.GetName(f.Image));
    }

    [AvaloniaFact]
    public async Task An_image_without_alt_text_has_no_accessible_name()
    {
        using var f = await ShowAsync($"![]({FakeUrl()})");

        Assert.Null(AutomationProperties.GetName(f.Image));
        Assert.Null(ToolTip.GetTip(f.Image));

        Click(f.Window, f.Center(f.Image));
        Assert.Null(AutomationProperties.GetName(f.Lightbox!));
    }

    [AvaloniaFact]
    public async Task A_selection_drag_that_ends_on_an_image_does_not_open_the_lightbox()
    {
        using var f = await ShowAsync($"Intro text\n\n![A diagram]({FakeUrl()})");
        var intro = f.Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().First();

        f.Window.MouseDown(f.Center(intro) - new Vector(f.Center(intro).X - 2, 0), MouseButton.Left);
        f.Window.MouseMove(f.Center(f.Image), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(f.Center(f.Image), MouseButton.Left);

        Assert.Null(f.Lightbox);
        Assert.NotEqual(string.Empty, f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public async Task A_selection_drag_that_starts_on_an_image_does_not_open_the_lightbox()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})\n\nAfter text");
        var start = f.Center(f.Image);

        f.Window.MouseDown(start, MouseButton.Left);
        f.Window.MouseMove(start + new Vector(0, 10), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(start + new Vector(0, 10), MouseButton.Left);

        Assert.Null(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task Releasing_off_the_image_or_another_button_does_not_open_the_lightbox()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        var corner = f.Image.TranslatePoint(new Point(f.Image.Bounds.Width - 1, f.Image.Bounds.Height - 1), f.Window)!.Value;

        // A move within the drag threshold keeps it a click, but ends past the image's edge.
        f.Window.MouseDown(corner, MouseButton.Left);
        f.Window.MouseMove(corner + new Vector(2, 2), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(corner + new Vector(2, 2), MouseButton.Left);
        Assert.Null(f.Lightbox);

        Click(f.Window, corner, MouseButton.Right);
        Assert.Null(f.Lightbox);

        Click(f.Window, corner);
        Assert.NotNull(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task Clicking_an_image_inside_a_link_opens_the_link()
    {
        using var f = await ShowAsync($"[![A diagram]({FakeUrl()})](https://example.com/)");
        var urls = new List<string>();
        f.Viewer.LinkClicked += (_, e) => { urls.Add(e.Url); e.Handled = true; };

        Click(f.Window, f.Center(f.Image));

        Assert.Null(f.Lightbox);
        Assert.Equal(["https://example.com/"], urls);
        Assert.NotSame(ZoomCursor.Instance, f.Image.Cursor);
        Assert.False(f.Image.Focusable);
    }

    [AvaloniaFact]
    public async Task An_image_nested_in_a_link_does_not_zoom()
    {
        using var f = await ShowAsync($"[see *![A diagram]({FakeUrl()})*](https://example.com/)");

        RaiseKey(f.Image, Key.Enter);

        Assert.Null(f.Lightbox);
        Assert.False(f.Image.Focusable);
    }

    [AvaloniaFact]
    public async Task Turning_zoom_off_leaves_images_inert()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})", configure: v => v.IsImageZoomEnabled = false);

        Click(f.Window, f.Center(f.Image));
        RaiseKey(f.Image, Key.Enter);

        Assert.Null(f.Lightbox);
        Assert.Null(f.Image.Cursor);
        Assert.False(f.Image.Focusable);
    }

    [AvaloniaFact]
    public async Task Turning_zoom_off_re_renders_the_document()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        var before = f.Image;

        f.Viewer.IsImageZoomEnabled = false;

        Assert.NotSame(before, f.Image);
        Assert.False(f.Image.Focusable);
    }

    [AvaloniaFact]
    public async Task Escape_closes_the_lightbox_and_focus_returns_to_the_viewer()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        Click(f.Window, f.Center(f.Image));
        var lightbox = f.Lightbox!;

        Press(f.Window, Key.Enter);
        Assert.Same(lightbox, f.Lightbox);
        Press(f.Window, Key.Escape);

        Assert.Null(f.Lightbox);
        Assert.Null(lightbox.GetVisualParent());
        Assert.Null(lightbox.Parent);
        Assert.DoesNotContain(lightbox, f.Viewer.GetVisualChildren());
        Assert.True(f.Viewer.IsKeyboardFocusWithin);
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public async Task Enter_or_Space_on_a_focused_image_opens_the_lightbox_and_Escape_returns_focus_to_it(Key key)
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        f.Image.Focus(NavigationMethod.Tab);

        Press(f.Window, Key.A);
        Assert.Null(f.Lightbox);
        Press(f.Window, key);
        Assert.NotNull(f.Lightbox);
        Assert.True(f.Lightbox!.IsFocused);

        Press(f.Window, Key.Escape);

        Assert.Null(f.Lightbox);
        Assert.True(f.Image.IsFocused);
    }

    [AvaloniaFact]
    public async Task Only_Escape_closes_the_lightbox_from_the_keyboard()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        Click(f.Window, f.Center(f.Image));

        Press(f.Window, Key.A);

        Assert.NotNull(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task An_image_still_loading_does_not_zoom()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})", loader: new FakeLoader(null, never: true));

        // Sized, so that a release can fall on it.
        f.Image.Width = 50;
        f.Image.Height = 50;
        f.Window.UpdateLayout();

        RaiseKey(f.Image, Key.Enter);
        f.Image.RaiseEvent(new PointerReleasedEventArgs(f.Image, new Pointer(1, PointerType.Mouse, true), f.Window,
            f.Center(f.Image), 0, new PointerPointProperties(), KeyModifiers.None, MouseButton.Left));

        Assert.Null(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task Clicking_the_backdrop_or_the_image_closes_the_lightbox()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");

        Click(f.Window, f.Center(f.Image));
        f.Window.UpdateLayout();
        Click(f.Window, f.Viewer.TranslatePoint(new Point(5, f.Viewer.Bounds.Height - 5), f.Window)!.Value);
        Assert.Null(f.Lightbox);
        Assert.True(f.Viewer.IsKeyboardFocusWithin);

        Click(f.Window, f.Center(f.Image));
        f.Window.UpdateLayout();
        Click(f.Window, f.Center(f.Lightbox!.Image));
        Assert.Null(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task Releasing_off_the_lightbox_or_another_button_does_not_close_it()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        Click(f.Window, f.Center(f.Image));
        f.Window.UpdateLayout();
        var edge = f.Viewer.TranslatePoint(new Point(1, 1), f.Window)!.Value;

        Click(f.Window, edge, MouseButton.Right);
        Assert.NotNull(f.Lightbox);

        f.Window.MouseDown(edge, MouseButton.Left);
        f.Window.MouseMove(edge - new Vector(3, 3), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(edge - new Vector(3, 3), MouseButton.Left);
        Assert.NotNull(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task The_close_button_closes_the_lightbox()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})", theme: true);
        Click(f.Window, f.Center(f.Image));
        f.Window.UpdateLayout();
        var button = f.Lightbox!.CloseButton;
        Assert.Contains("markdown-image-lightbox-close", button.Classes);
        Assert.IsType<ImageLightbox.CloseGlyph>(button.Content);
        Assert.Equal(HorizontalAlignment.Right, button.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Top, button.VerticalAlignment);
        Assert.Equal("Close", AutomationProperties.GetName(button));

        Click(f.Window, f.Center(button));

        Assert.Null(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task The_close_button_works_from_the_keyboard_and_tab_stays_in_the_lightbox()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        Click(f.Window, f.Center(f.Image));
        var lightbox = f.Lightbox!;
        Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(lightbox));

        lightbox.CloseButton.Focus(NavigationMethod.Tab);
        Press(f.Window, Key.Enter);

        Assert.Null(f.Lightbox);
        Assert.True(f.Viewer.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public async Task Only_one_lightbox_opens_at_a_time()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        RaiseKey(f.Image, Key.Enter);
        var lightbox = f.Lightbox;

        RaiseKey(f.Image, Key.Enter);

        Assert.Same(lightbox, f.Lightbox);
        Assert.Single(f.Viewer.GetVisualChildren().OfType<ImageLightbox>());
    }

    [AvaloniaFact]
    public async Task Rendering_another_document_closes_the_lightbox_and_keeps_focus_in_the_viewer()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        f.Image.Focus(NavigationMethod.Tab);
        Press(f.Window, Key.Enter);
        var lightbox = f.Lightbox!;

        f.Viewer.Markdown = "Something else";

        Assert.Null(f.Lightbox);
        Assert.Null(lightbox.GetVisualParent());
        // The image that had focus is gone.
        Assert.True(f.Viewer.IsFocused);
    }

    [AvaloniaFact]
    public async Task Closing_the_lightbox_leaves_focus_alone_once_it_has_moved_elsewhere()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        var other = new TextBox();
        f.Window.Content = null;
        f.Window.Content = new StackPanel { Children = { other, f.Viewer } };
        f.Window.UpdateLayout();
        RaiseKey(f.Image, Key.Enter);
        Assert.NotNull(f.Lightbox);

        other.Focus();
        f.Viewer.Markdown = "Something else";

        Assert.Null(f.Lightbox);
        Assert.True(other.IsFocused);
    }

    [AvaloniaFact]
    public async Task Clearing_the_document_closes_the_lightbox()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        RaiseKey(f.Image, Key.Enter);
        var lightbox = f.Lightbox!;

        f.Viewer.Markdown = null;

        Assert.Null(f.Lightbox);
        Assert.Null(lightbox.GetVisualParent());
        Assert.True(f.Viewer.IsFocused);
    }

    [AvaloniaFact]
    public async Task The_clicks_and_keys_that_open_and_close_the_lightbox_go_no_further()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        var released = 0;
        var keys = 0;
        var clicks = 0;
        f.Window.AddHandler(InputElement.PointerReleasedEvent, (_, _) => released++, RoutingStrategies.Bubble);
        f.Window.AddHandler(InputElement.KeyDownEvent, (_, _) => keys++, RoutingStrategies.Bubble);
        f.Window.AddHandler(Button.ClickEvent, (_, _) => clicks++, RoutingStrategies.Bubble);

        Click(f.Window, f.Center(f.Image));
        f.Window.UpdateLayout();
        Click(f.Window, f.Center(f.Lightbox!.Image));
        f.Image.Focus(NavigationMethod.Tab);
        Press(f.Window, Key.Enter);
        Press(f.Window, Key.Escape);
        RaiseKey(f.Image, Key.Space);
        f.Window.UpdateLayout();
        f.Lightbox!.CloseButton.Focus(NavigationMethod.Tab);
        Press(f.Window, Key.Enter);

        Assert.Null(f.Lightbox);
        Assert.Equal(0, released);
        Assert.Equal(0, keys);
        Assert.Equal(0, clicks);
    }

    [AvaloniaFact]
    public async Task Applying_another_template_keeps_the_lightbox_open_on_top()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        RaiseKey(f.Image, Key.Enter);
        var lightbox = f.Lightbox!;

        f.Viewer.Template = ScrollingViewerTemplate.Create();
        f.Window.UpdateLayout();

        Assert.Same(lightbox, f.Lightbox);
        Assert.Equal(2, f.Viewer.GetVisualChildren().Count());
        Assert.Same(lightbox, f.Viewer.GetVisualChildren().Last());
    }

    [AvaloniaFact]
    public async Task Without_the_theme_the_backdrop_is_still_dimmed()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})");
        RaiseKey(f.Image, Key.Enter);

        Assert.Equal(Color.FromArgb(0xCC, 0, 0, 0), Assert.IsAssignableFrom<ISolidColorBrush>(f.Lightbox!.Background).Color);
    }

    [AvaloniaTheory]
    [InlineData("Light", "#CC000000")]
    [InlineData("Dark", "#E0000000")]
    public async Task The_theme_colours_the_lightbox(string variant, string backdrop)
    {
        using var theme = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})", theme: true);
        RaiseKey(f.Image, Key.Enter);
        f.Window.UpdateLayout();
        var lightbox = f.Lightbox!;

        Assert.Equal(Color.Parse(backdrop), Assert.IsAssignableFrom<ISolidColorBrush>(lightbox.Background).Color);
        Assert.Equal(new Thickness(48), lightbox.Image.Margin);
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(lightbox.CloseButton.Foreground).Color);
        Assert.Equal(Color.Parse("#66000000"), Assert.IsAssignableFrom<ISolidColorBrush>(lightbox.CloseButton.Background).Color);
    }
}
