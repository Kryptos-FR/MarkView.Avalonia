// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
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
        var viewer = new MarkdownViewer { IsImageZoomEnabled = true };
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

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None) =>
        window.KeyPress(key, modifiers, PhysicalKey.None, null);

    private static void RaiseKey(InputElement element, Key key) =>
        element.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    [AvaloniaFact]
    public void IsImageZoomEnabled_defaults_to_false()
    {
        Assert.False(new MarkdownViewer().IsImageZoomEnabled);
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
    public async Task The_lightbox_takes_its_themes_from_the_viewer()
    {
        var themes = new Rendering.ThemeKeyTests.TaggedThemes();
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})", configure: v => themes.AddTo(v.Resources,
            "MarkdownImageLightboxBackingTheme", "MarkdownImageLightboxToolbarTheme", "MarkdownImageLightboxZoomLevelTheme"));

        Click(f.Window, f.Center(f.Image));

        var lightbox = Assert.IsType<ImageLightbox>(f.Lightbox);
        Assert.Equal("MarkdownImageLightboxBackingTheme", themes.KeyOf(lightbox.Backing));
        Assert.Equal("MarkdownImageLightboxToolbarTheme", themes.KeyOf(lightbox.Toolbar));
        Assert.Equal("MarkdownImageLightboxZoomLevelTheme", themes.KeyOf(lightbox.ZoomLevel));
    }

    [AvaloniaFact]
    public async Task The_lightbox_never_enlarges_an_image_past_its_natural_size()
    {
        using var f = await ShowAsync($"![small]({FakeUrl()})", width: 40, height: 20);

        Click(f.Window, f.Center(f.Image));
        f.Window.UpdateLayout();

        var image = f.Lightbox!.Image;
        Assert.Equal(new Size(40, 20), image.Bounds.Size);
        Assert.Equal(new Point(180, 140), image.Bounds.Position);
        Assert.Equal(1, f.Lightbox.Zoom);
        Assert.True(f.Lightbox.IsFitted);
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
        var viewer = new MarkdownViewer { IsImageZoomEnabled = true, Template = ScrollingViewerTemplate.Create() };
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
    public async Task Screen_readers_see_a_zoomable_image_without_alt_text_and_what_it_does()
    {
        using var f = await ShowAsync($"![]({FakeUrl()})", theme: true);
        var peer = ControlAutomationPeer.CreatePeerForElement(f.Image);

        // A Tab stop, so not hidden as decorative
        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
        Assert.True(peer.IsContentElement());
        Assert.Equal("Zoom", peer.GetHelpText());

        Click(f.Window, f.Center(f.Image));
        var lightbox = ControlAutomationPeer.CreatePeerForElement(f.Lightbox!);
        Assert.Equal(AutomationControlType.Image, lightbox.GetAutomationControlType());
        Assert.Equal("Image", lightbox.GetName());
    }

    [AvaloniaFact]
    public async Task The_lightbox_is_announced_by_the_image_alt_text()
    {
        using var f = await ShowAsync($"![A diagram]({FakeUrl()})", theme: true);
        Click(f.Window, f.Center(f.Image));

        Assert.Equal("A diagram", ControlAutomationPeer.CreatePeerForElement(f.Lightbox!).GetName());
    }

    [AvaloniaFact]
    public async Task Tab_reaches_a_zoomable_image_between_the_links_around_it()
    {
        using var f = await ShowAsync($"[before](https://example.com/b) ![A diagram]({FakeUrl()}) [after](https://example.com/a)");
        f.Viewer.Focus();

        Press(f.Window, Key.Tab);
        Assert.IsType<LinkFocusTarget>(f.Window.FocusManager!.GetFocusedElement());
        Press(f.Window, Key.Tab);
        Assert.True(f.Image.IsFocused);
        Press(f.Window, Key.Tab);
        Assert.Equal("after", Assert.IsType<LinkFocusTarget>(f.Window.FocusManager!.GetFocusedElement()).AccessibleName);
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
        // The right click opened the viewer's context menu, which would take the next click to close.
        f.Viewer.ContextFlyout!.Hide();

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

    // ── Zooming and panning in the lightbox ───────────────────────────────────

    private static async Task<Fixture> OpenAsync(double width = 800, double height = 600, bool theme = false, Action<Image>? configure = null)
    {
        var f = await ShowAsync($"![A diagram]({FakeUrl()})", width, height, theme);
        configure?.Invoke(f.Image);
        RaiseKey(f.Image, Key.Enter);
        f.Window.UpdateLayout();
        return f;
    }

    private static void AssertBounds(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Width, actual.Width, 6);
        Assert.Equal(expected.Height, actual.Height, 6);
    }

    private static void Drag(Window window, Point from, params Point[] to)
    {
        window.MouseDown(from, MouseButton.Left);
        foreach (var point in to)
            window.MouseMove(point, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to[^1], MouseButton.Left);
    }

    [AvaloniaFact]
    public async Task The_lightbox_opens_fitted_and_shows_the_zoom_level()
    {
        // The viewer is 400×300, so an 800×600 picture is fitted at half its size.
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;

        Assert.True(lightbox.IsFitted);
        Assert.Equal(0.5, lightbox.Zoom);
        Assert.Equal("50%", lightbox.ZoomLevel.Text);
        AssertBounds(new Rect(0, 0, 400, 300), lightbox.Image.Bounds);
        Assert.Equal(Stretch.Fill, lightbox.Image.Stretch);
        Assert.Equal(lightbox.Image.Bounds, lightbox.Backing.Bounds);
        // Presses land on the lightbox itself.
        Assert.False(lightbox.Image.IsHitTestVisible);
        Assert.False(lightbox.Backing.IsHitTestVisible);
        Assert.Contains("markdown-image-lightbox-backing", lightbox.Backing.Classes);
        Assert.Contains("markdown-image-lightbox-close", lightbox.CloseButton.Classes);
        Assert.Contains("markdown-image-lightbox-zoom-out", lightbox.ZoomOutButton.Classes);
        Assert.Contains("markdown-image-lightbox-zoom-in", lightbox.ZoomInButton.Classes);
        Assert.Contains("markdown-image-lightbox-zoom-fit", lightbox.FitButton.Classes);
        Assert.All([lightbox.ZoomOutButton, lightbox.ZoomInButton, lightbox.FitButton], b => Assert.Contains("markdown-image-lightbox-zoom", b.Classes));
        Assert.IsType<ImageLightbox.ZoomGlyph>(lightbox.ZoomInButton.Content);
        Assert.Contains("markdown-image-lightbox-zoom-level", lightbox.ZoomLevel.Classes);
        Assert.Equal(VerticalAlignment.Center, lightbox.ZoomLevel.VerticalAlignment);
        Assert.Equal(TextAlignment.Center, lightbox.ZoomLevel.TextAlignment);
        Assert.Contains("markdown-image-lightbox-toolbar", lightbox.Toolbar.Classes);
        Assert.Equal(HorizontalAlignment.Center, lightbox.Toolbar.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Bottom, lightbox.Toolbar.VerticalAlignment);
        Assert.Equal(300, lightbox.Toolbar.Bounds.Bottom);
        Assert.Equal(200, lightbox.Toolbar.Bounds.Center.X, 0);
        Assert.Equal(400, lightbox.CloseButton.Bounds.Right);
        Assert.True(lightbox.CloseButton.Bounds.Width > 0);
        Assert.Equal(AutomationControlType.Image, ControlAutomationPeer.CreatePeerForElement(lightbox).GetAutomationControlType());
        Assert.Equal(0, lightbox.CloseButton.Bounds.Top);
        Assert.True(lightbox.ClipToBounds);
    }

    [AvaloniaTheory]
    [InlineData(1600, 600, 0, 75, 400, 150)]
    [InlineData(320, 1200, 160, 0, 80, 300)]
    public async Task A_picture_is_fitted_to_the_side_of_the_lightbox_it_fills(
        double width, double height, double x, double y, double fittedWidth, double fittedHeight)
    {
        using var f = await OpenAsync(width, height);

        Assert.Equal(0.25, f.Lightbox!.Zoom);
        Assert.Equal(new Rect(x, y, fittedWidth, fittedHeight).Center, f.Lightbox.Image.Bounds.Center);
        Assert.Equal(fittedHeight, f.Lightbox.Image.Bounds.Height, 0);
    }

    [AvaloniaTheory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    public async Task A_picture_without_a_size_is_centred_and_does_not_zoom(double width, double height)
    {
        using var f = await OpenAsync(width, height);
        var lightbox = f.Lightbox!;

        lightbox.ZoomIn();
        f.Window.UpdateLayout();

        Assert.Equal(1, lightbox.Zoom);
        Assert.Equal(new Point(200, 150), lightbox.Image.Bounds.Center);
        Assert.Equal(default, lightbox.Backing.Bounds);
    }

    [AvaloniaFact]
    public void A_lightbox_not_laid_out_yet_does_not_zoom()
    {
        var lightbox = new ImageLightbox(new Image { Source = new FakeImage(100, 50) }, null);

        lightbox.ZoomIn();

        Assert.Equal(1, lightbox.Zoom);
        Assert.True(lightbox.IsFitted);
    }

    [AvaloniaFact]
    public async Task The_zoom_buttons_zoom_about_the_centre_and_fit_the_picture_again()
    {
        using var f = await OpenAsync(200, 120);
        var lightbox = f.Lightbox!;

        Click(f.Window, f.Center(lightbox.ZoomInButton));
        f.Window.UpdateLayout();

        Assert.Same(lightbox, f.Lightbox);
        Assert.False(lightbox.IsFitted);
        Assert.Equal(1.25, lightbox.Zoom);
        Assert.Equal("125%", lightbox.ZoomLevel.Text);
        AssertBounds(new Rect(75, 75, 250, 150), lightbox.Image.Bounds);

        Click(f.Window, f.Center(lightbox.ZoomOutButton));
        f.Window.UpdateLayout();
        Assert.True(lightbox.IsFitted);
        Assert.Equal(1, lightbox.Zoom);

        Click(f.Window, f.Center(lightbox.ZoomInButton));
        Click(f.Window, f.Center(lightbox.ZoomInButton));
        Click(f.Window, f.Center(lightbox.FitButton));
        f.Window.UpdateLayout();
        Assert.True(lightbox.IsFitted);
        AssertBounds(new Rect(100, 90, 200, 120), lightbox.Image.Bounds);
    }

    [AvaloniaFact]
    public async Task The_theme_names_the_zoom_buttons_and_rounds_the_backing()
    {
        using var f = await OpenAsync(theme: true);
        var lightbox = f.Lightbox!;

        Assert.Equal("Zoom out", AutomationProperties.GetName(lightbox.ZoomOutButton));
        Assert.Equal("Zoom in", AutomationProperties.GetName(lightbox.ZoomInButton));
        Assert.Equal("Fit to view", AutomationProperties.GetName(lightbox.FitButton));
        Assert.Equal(new CornerRadius(6), lightbox.Backing.CornerRadius);
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(lightbox.ZoomLevel.Foreground).Color);
    }

    [AvaloniaFact]
    public async Task Clicking_the_toolbar_does_not_close_the_lightbox()
    {
        using var f = await OpenAsync();
        // The theme paints the toolbar, which makes it hit-testable.
        f.Lightbox!.Toolbar.Background = Brushes.Transparent;

        Click(f.Window, f.Center(f.Lightbox.ZoomLevel));

        Assert.NotNull(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task Releasing_just_off_the_lightbox_does_not_close_it()
    {
        using var f = await OpenAsync();
        var edge = f.Viewer.TranslatePoint(new Point(1, 1), f.Window)!.Value;

        // Less than a drag.
        Drag(f.Window, edge, edge - new Vector(2, 0));

        Assert.NotNull(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task The_wheel_zooms_about_the_pointer()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;

        f.Window.MouseWheel(new Point(100, 75), new Vector(0, 1));
        f.Window.UpdateLayout();

        // The point under the pointer stays under it.
        Assert.Equal(0.6, lightbox.Zoom, 9);
        AssertBounds(new Rect(-20, -15, 480, 360), lightbox.Image.Bounds);

        f.Window.MouseWheel(new Point(100, 75), new Vector(0, -1));
        f.Window.UpdateLayout();

        Assert.True(lightbox.IsFitted);
        AssertBounds(new Rect(0, 0, 400, 300), lightbox.Image.Bounds);
    }

    [AvaloniaFact]
    public async Task A_sideways_scroll_does_not_zoom()
    {
        using var f = await OpenAsync();

        f.Window.MouseWheel(new Point(100, 75), new Vector(1, 0));

        Assert.True(f.Lightbox!.IsFitted);
    }

    [AvaloniaFact]
    public async Task Zooming_stays_between_the_fitted_size_and_eight_times_the_natural_size()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;

        lightbox.ZoomOut();
        Assert.Equal(0.5, lightbox.Zoom);
        Assert.True(lightbox.IsFitted);

        lightbox.ZoomBy(100, new Point(200, 150));
        Assert.Equal(ImageLightbox.MaxZoomFactor, lightbox.Zoom);
        lightbox.ZoomIn();
        Assert.Equal(ImageLightbox.MaxZoomFactor, lightbox.Zoom);
    }

    [AvaloniaFact]
    public async Task A_small_picture_is_zoomed_out_no_further_than_its_natural_size()
    {
        using var f = await OpenAsync(40, 20);
        var lightbox = f.Lightbox!;

        lightbox.ZoomOut();
        Assert.Equal(1, lightbox.Zoom);

        lightbox.ZoomBy(100, new Point(200, 150));
        Assert.Equal(ImageLightbox.MaxZoomFactor, lightbox.Zoom);
    }

    [AvaloniaFact]
    public async Task Dragging_pans_a_zoomed_picture_without_closing_the_lightbox()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        lightbox.ZoomBy(2, new Point(200, 150));
        f.Window.UpdateLayout();
        AssertBounds(new Rect(-200, -150, 800, 600), lightbox.Image.Bounds);

        Drag(f.Window, new Point(200, 150), new Point(202, 150), new Point(250, 170));
        f.Window.UpdateLayout();

        Assert.Same(lightbox, f.Lightbox);
        // The first move stays under the drag threshold, so the picture follows from where the drag started.
        AssertBounds(new Rect(-150, -130, 800, 600), lightbox.Image.Bounds);
        Assert.Null(lightbox.Cursor);

        // Never past the picture's edges.
        Drag(f.Window, new Point(200, 150), new Point(1200, 1150));
        f.Window.UpdateLayout();
        AssertBounds(new Rect(0, 0, 800, 600), lightbox.Image.Bounds);
        Drag(f.Window, new Point(200, 150), new Point(-1200, -1150));
        f.Window.UpdateLayout();
        AssertBounds(new Rect(-400, -300, 800, 600), lightbox.Image.Bounds);
    }

    [AvaloniaFact]
    public async Task Dragging_shows_the_move_cursor()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        lightbox.ZoomBy(2, new Point(200, 150));
        f.Window.UpdateLayout();

        f.Window.MouseDown(new Point(200, 150), MouseButton.Left);
        f.Window.MouseMove(new Point(203, 150), RawInputModifiers.LeftMouseButton);
        Assert.Null(lightbox.Cursor);
        f.Window.MouseMove(new Point(220, 150), RawInputModifiers.LeftMouseButton);
        Assert.NotNull(lightbox.Cursor);

        f.Window.MouseUp(new Point(220, 150), MouseButton.Left);
        Assert.Null(lightbox.Cursor);
        Assert.NotNull(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task A_picture_that_fits_along_an_axis_stays_centred_along_it()
    {
        using var f = await OpenAsync(200, 100);
        var lightbox = f.Lightbox!;
        // 600×300: wider than the viewer, but not taller.
        lightbox.ZoomBy(3, new Point(0, 0));
        f.Window.UpdateLayout();
        AssertBounds(new Rect(0, 0, 600, 300), lightbox.Image.Bounds);

        Drag(f.Window, new Point(200, 150), new Point(150, 100));
        f.Window.UpdateLayout();

        AssertBounds(new Rect(-50, 0, 600, 300), lightbox.Image.Bounds);
    }

    [AvaloniaFact]
    public async Task A_short_drag_still_closes_the_lightbox()
    {
        using var f = await OpenAsync();

        Drag(f.Window, new Point(200, 150), new Point(202, 152));

        Assert.Null(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task Moving_another_pointer_or_without_a_press_does_not_pan()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        lightbox.ZoomBy(2, new Point(200, 150));
        f.Window.UpdateLayout();

        f.Window.MouseMove(new Point(300, 250));
        f.Window.MouseDown(new Point(200, 150), MouseButton.Right);
        f.Window.MouseMove(new Point(300, 250), RawInputModifiers.RightMouseButton);
        f.Window.MouseUp(new Point(300, 250), MouseButton.Right);
        f.Window.UpdateLayout();

        AssertBounds(new Rect(-200, -150, 800, 600), lightbox.Image.Bounds);
        Assert.NotNull(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task Keys_zoom_pan_and_fit_the_picture()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;

        Press(f.Window, Key.OemPlus);
        Assert.Equal(0.625, lightbox.Zoom);
        Press(f.Window, Key.Add);
        Assert.Equal(0.78125, lightbox.Zoom);
        Press(f.Window, Key.Subtract);
        Press(f.Window, Key.OemMinus);
        Assert.True(lightbox.IsFitted);

        lightbox.ZoomBy(2, new Point(200, 150));
        // An arrow moves the view by a tenth of its size, so the picture the other way.
        Press(f.Window, Key.Left);
        Press(f.Window, Key.Up);
        f.Window.UpdateLayout();
        AssertBounds(new Rect(-160, -120, 800, 600), lightbox.Image.Bounds);
        Press(f.Window, Key.Right);
        Press(f.Window, Key.Right);
        Press(f.Window, Key.Down);
        Press(f.Window, Key.Down);
        f.Window.UpdateLayout();
        AssertBounds(new Rect(-240, -180, 800, 600), lightbox.Image.Bounds);

        Press(f.Window, Key.D0);
        f.Window.UpdateLayout();
        Assert.True(lightbox.IsFitted);
        AssertBounds(new Rect(0, 0, 400, 300), lightbox.Image.Bounds);
        lightbox.ZoomIn();
        Press(f.Window, Key.NumPad0);
        Assert.True(lightbox.IsFitted);
        Assert.Same(lightbox, f.Lightbox);
    }

    [AvaloniaFact]
    public async Task The_keys_that_zoom_and_pan_go_no_further()
    {
        using var f = await OpenAsync();
        var keys = 0;
        f.Window.AddHandler(InputElement.KeyDownEvent, (_, _) => keys++, RoutingStrategies.Bubble);

        foreach (var key in new[] { Key.OemPlus, Key.OemMinus, Key.Add, Key.Subtract, Key.D0, Key.NumPad0, Key.Left, Key.Right, Key.Up, Key.Down })
            Press(f.Window, key);

        Assert.Equal(0, keys);
    }

    [AvaloniaFact]
    public async Task Resizing_the_viewer_fits_a_fitted_picture_again_and_keeps_a_zoomed_one()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;

        f.Viewer.Width = 200;
        f.Viewer.Height = 150;
        f.Window.UpdateLayout();
        Assert.Equal(0.25, lightbox.Zoom);

        lightbox.ZoomBy(2, new Point(100, 75));
        f.Viewer.Width = 400;
        f.Viewer.Height = 300;
        f.Window.UpdateLayout();
        Assert.Equal(0.5, lightbox.Zoom);
        Assert.False(lightbox.IsFitted);
        AssertBounds(new Rect(0, 0, 400, 300), lightbox.Image.Bounds);

        // Never smaller than its fitted size.
        lightbox.ZoomBy(1.2, new Point(100, 75));
        f.Viewer.Width = 2000;
        f.Viewer.Height = 2000;
        f.Window.UpdateLayout();
        Assert.Equal(1, lightbox.Zoom);
        Assert.False(lightbox.IsFitted);
    }

    [AvaloniaFact]
    public async Task The_lightbox_follows_the_source_of_its_image()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        var other = new FakeImage(100, 50);

        f.Image.Source = other;
        f.Window.UpdateLayout();

        Assert.Same(other, lightbox.Image.Source);
        AssertBounds(new Rect(150, 125, 100, 50), lightbox.Image.Bounds);

        // A picture without a size has nothing behind it.
        ImageZoom.SetBackground(f.Image, Brushes.Red);
        f.Image.Source = new FakeImage(0, 0);
        f.Window.UpdateLayout();
        Assert.Equal(default, lightbox.Backing.Bounds);
        f.Image.Source = other;

        // An image losing its source (being removed from the document) leaves the lightbox as it is.
        f.Image.Source = null;
        Assert.Same(other, lightbox.Image.Source);
        f.Image.Width = 10;
        Assert.Same(other, lightbox.Image.Source);
    }

    [AvaloniaFact]
    public async Task The_backing_is_painted_with_the_background_of_the_image_behind_it()
    {
        using var f = await OpenAsync(configure: image => ImageZoom.SetBackground(image, Brushes.Red));
        var lightbox = f.Lightbox!;

        Assert.Same(Brushes.Red, lightbox.Backing.Background);
        AssertBounds(lightbox.Image.Bounds, lightbox.Backing.Bounds);
        Assert.Same(lightbox.Backing, lightbox.Children[0]);

        lightbox.ZoomIn();
        f.Window.UpdateLayout();
        AssertBounds(lightbox.Image.Bounds, lightbox.Backing.Bounds);

        ImageZoom.SetBackground(f.Image, Brushes.Blue);
        Assert.Same(Brushes.Blue, lightbox.Backing.Background);
        ImageZoom.SetBackground(f.Image, null);
        Assert.Null(lightbox.Backing.Background);
    }

    [AvaloniaFact]
    public async Task An_image_has_no_backing_by_default()
    {
        using var f = await OpenAsync();

        Assert.Null(f.Lightbox!.Backing.Background);
        Assert.False(ImageZoom.GetEnlargeToFit(f.Image));
    }

    [AvaloniaFact]
    public async Task A_closed_lightbox_no_longer_follows_its_image()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        Press(f.Window, Key.Escape);

        f.Image.Source = new FakeImage(100, 50);

        Assert.Same(f.Picture, lightbox.Image.Source);
    }

    [AvaloniaFact]
    public async Task Tab_moves_through_the_close_zoom_and_save_buttons()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;

        var order = new List<IInputElement?>();
        for (var i = 0; i < 6; i++)
        {
            Press(f.Window, Key.Tab);
            order.Add(f.Window.FocusManager!.GetFocusedElement());
        }
        Press(f.Window, Key.Tab, RawInputModifiers.Shift);
        order.Add(f.Window.FocusManager!.GetFocusedElement());
        Press(f.Window, Key.Tab, RawInputModifiers.Shift);
        order.Add(f.Window.FocusManager!.GetFocusedElement());

        // Round the lightbox and its buttons, never to the document behind.
        Assert.Equal<IInputElement?>(
            [lightbox.CloseButton, lightbox.ZoomOutButton, lightbox.ZoomInButton, lightbox.FitButton, lightbox.SaveButton, lightbox, lightbox.SaveButton, lightbox.FitButton],
            order);
    }

    [AvaloniaFact]
    public async Task A_pinch_zooms_about_its_centre_without_closing_the_lightbox()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;

        // About the point between the two touches when the pinch starts.
        var first = f.Window.TouchBegin(new Point(100, 100));
        var second = f.Window.TouchBegin(new Point(200, 100));
        f.Window.TouchMove(second, new Point(300, 100));
        f.Window.UpdateLayout();
        Assert.Equal(1, lightbox.Zoom, 9);
        AssertBounds(new Rect(-150, -100, 800, 600), lightbox.Image.Bounds);

        // The scale is relative to the start of the pinch.
        f.Window.TouchMove(second, new Point(250, 100));
        Assert.Equal(0.75, lightbox.Zoom, 9);
        f.Window.TouchEnd(second, new Point(250, 100));
        f.Window.TouchEnd(first, new Point(100, 100));

        Assert.Same(lightbox, f.Lightbox);
    }

    [AvaloniaFact]
    public async Task A_tap_closes_the_lightbox()
    {
        using var f = await OpenAsync();

        var touch = f.Window.TouchBegin(new Point(150, 150));
        f.Window.TouchEnd(touch, new Point(150, 150));

        Assert.Null(f.Lightbox);
    }

    [AvaloniaFact]
    public async Task A_touchpad_pinch_zooms_about_the_pointer()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;

        RaiseMagnify(f, lightbox);
        f.Window.UpdateLayout();

        Assert.Equal(0.6, lightbox.Zoom, 9);
        AssertBounds(new Rect(-20, -15, 480, 360), lightbox.Image.Bounds);
    }

    [AvaloniaFact]
    public async Task Zooming_a_panned_picture_keeps_the_point_under_the_pointer()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        lightbox.ZoomBy(2, new Point(200, 150));
        Drag(f.Window, new Point(200, 150), new Point(250, 150), new Point(300, 200));
        f.Window.UpdateLayout();
        AssertBounds(new Rect(-100, -100, 800, 600), lightbox.Image.Bounds);

        f.Window.MouseWheel(new Point(200, 150), new Vector(0, 1));
        f.Window.UpdateLayout();

        AssertBounds(new Rect(-160, -150, 960, 720), lightbox.Image.Bounds);
    }

    [AvaloniaFact]
    public async Task Dragging_back_from_past_an_edge_moves_the_picture_at_once()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        lightbox.ZoomBy(2, new Point(200, 150));
        f.Window.UpdateLayout();

        Drag(f.Window, new Point(200, 150), new Point(1200, 150), new Point(1100, 150));
        f.Window.UpdateLayout();

        AssertBounds(new Rect(-100, -150, 800, 600), lightbox.Image.Bounds);
    }

    [AvaloniaFact]
    public async Task A_move_of_the_drag_threshold_starts_a_drag()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        lightbox.ZoomBy(2, new Point(200, 150));
        f.Window.UpdateLayout();

        Drag(f.Window, new Point(200, 150), new Point(200 + ImageLightbox.DragThreshold, 150));
        f.Window.UpdateLayout();

        Assert.Same(lightbox, f.Lightbox);
        AssertBounds(new Rect(-196, -150, 800, 600), lightbox.Image.Bounds);
    }

    [AvaloniaFact]
    public async Task Zooming_back_to_an_enlarged_fit_fits_the_picture_again()
    {
        // Fitted at twice its natural size, which zooming out goes below.
        using var f = await OpenAsync(100, 75, configure: image => image.SetValue(ImageZoom.EnlargeToFitProperty, true));
        var lightbox = f.Lightbox!;
        Assert.Equal(ImageLightbox.MaxFitEnlargement, lightbox.Zoom);

        lightbox.ZoomOut();
        Assert.Equal(1.6, lightbox.Zoom, 9);
        lightbox.ZoomBy(Math.Pow(ImageLightbox.WheelZoomStep, 4), new Point(200, 150));
        lightbox.ZoomBy(Math.Pow(ImageLightbox.WheelZoomStep, -4), new Point(200, 150));

        Assert.False(lightbox.IsFitted);
        lightbox.ZoomIn();
        Assert.True(lightbox.IsFitted);
        Assert.Equal(ImageLightbox.MaxFitEnlargement, lightbox.Zoom);

        // Rounding: 1.2³ × 1.2⁻³ is not exactly 1.
        lightbox.ZoomBy(Math.Pow(ImageLightbox.WheelZoomStep, 3), new Point(200, 150));
        lightbox.ZoomBy(Math.Pow(ImageLightbox.WheelZoomStep, -3), new Point(200, 150));
        Assert.True(lightbox.IsFitted);
    }

    [AvaloniaFact]
    public async Task Panning_zooming_and_pinching_go_no_further()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        lightbox.ZoomBy(2, new Point(200, 150));
        f.Window.UpdateLayout();
        var events = 0;
        f.Window.AddHandler(InputElement.PointerMovedEvent, (_, _) => events++, RoutingStrategies.Bubble);
        f.Window.AddHandler(InputElement.PointerWheelChangedEvent, (_, _) => events++, RoutingStrategies.Bubble);
        f.Window.AddHandler(InputElement.PinchEvent, (_, _) => events++, RoutingStrategies.Bubble);
        f.Window.AddHandler(InputElement.PointerTouchPadGestureMagnifyEvent, (_, _) => events++, RoutingStrategies.Bubble);

        f.Window.MouseDown(new Point(200, 150), MouseButton.Left);
        f.Window.MouseMove(new Point(250, 150), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(new Point(250, 150), MouseButton.Left);
        f.Window.MouseWheel(new Point(200, 150), new Vector(0, 1));
        var first = f.Window.TouchBegin(new Point(100, 100));
        var second = f.Window.TouchBegin(new Point(200, 100));
        f.Window.TouchMove(second, new Point(250, 100));
        f.Window.TouchEnd(second, new Point(250, 100));
        f.Window.TouchEnd(first, new Point(100, 100));
        RaiseMagnify(f, lightbox);

        Assert.Equal(0, events);
    }

    [AvaloniaFact]
    public async Task A_second_touch_lifted_without_moving_does_not_close_the_lightbox()
    {
        using var f = await OpenAsync();

        var first = f.Window.TouchBegin(new Point(100, 100));
        var second = f.Window.TouchBegin(new Point(200, 100));
        f.Window.TouchEnd(second, new Point(200, 100));

        Assert.NotNull(f.Lightbox);
        f.Window.TouchEnd(first, new Point(100, 100));
    }

    [AvaloniaFact]
    public async Task Touches_that_meet_restart_the_pinch()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        var first = f.Window.TouchBegin(new Point(100, 100));
        var second = f.Window.TouchBegin(new Point(200, 100));

        f.Window.TouchMove(second, new Point(100, 100));
        f.Window.TouchMove(second, new Point(300, 100));
        Assert.Equal(0.5, lightbox.Zoom, 9);
        f.Window.TouchMove(second, new Point(400, 100));
        Assert.Equal(0.75, lightbox.Zoom, 9);

        f.Window.TouchEnd(second, new Point(400, 100));
        f.Window.TouchEnd(first, new Point(100, 100));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Losing_the_pointer_capture_ends_a_press_or_drag(bool drag)
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        lightbox.ZoomBy(2, new Point(200, 150));
        f.Window.UpdateLayout();
        IPointer? pointer = null;
        f.Window.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel);

        f.Window.MouseDown(new Point(200, 150), MouseButton.Left);
        if (drag)
            f.Window.MouseMove(new Point(250, 150), RawInputModifiers.LeftMouseButton);
        Assert.Same(lightbox, pointer!.Captured);
        pointer.Capture(null);
        Assert.Null(lightbox.Cursor);
        f.Window.MouseUp(new Point(250, 150), MouseButton.Left);
        Assert.Same(lightbox, f.Lightbox);

        // The next click is a click, whatever the press before it was.
        Click(f.Window, new Point(200, 150));
        Assert.Null(f.Lightbox);
    }

    private static void RaiseMagnify(Fixture f, ImageLightbox lightbox) =>
        lightbox.RaiseEvent(new PointerDeltaEventArgs(InputElement.PointerTouchPadGestureMagnifyEvent, lightbox,
            new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true), f.Window, new Point(100, 75), 0,
            new PointerPointProperties(), KeyModifiers.None, new Vector(0.2, 0)));

    [AvaloniaFact]
    public async Task Each_pinch_scales_from_where_it_starts()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        var first = f.Window.TouchBegin(new Point(100, 100));
        var second = f.Window.TouchBegin(new Point(200, 100));
        f.Window.TouchMove(second, new Point(300, 100));
        f.Window.TouchEnd(second, new Point(300, 100));
        f.Window.TouchEnd(first, new Point(100, 100));
        Assert.Equal(1, lightbox.Zoom, 9);

        first = f.Window.TouchBegin(new Point(100, 100));
        second = f.Window.TouchBegin(new Point(200, 100));
        f.Window.TouchMove(second, new Point(250, 100));
        f.Window.TouchEnd(second, new Point(250, 100));
        f.Window.TouchEnd(first, new Point(100, 100));

        Assert.Equal(1.5, lightbox.Zoom, 9);
    }

    [AvaloniaFact]
    public async Task Panning_is_clamped_at_once()
    {
        using var f = await OpenAsync();
        var lightbox = f.Lightbox!;
        lightbox.ZoomBy(2, new Point(200, 150));
        f.Window.UpdateLayout();

        // Past the edge, then back, before the lightbox is laid out again.
        lightbox.PanBy(new Vector(1000, 0));
        lightbox.PanBy(new Vector(-100, 0));
        f.Window.UpdateLayout();

        AssertBounds(new Rect(-100, -150, 800, 600), lightbox.Image.Bounds);
    }
}
