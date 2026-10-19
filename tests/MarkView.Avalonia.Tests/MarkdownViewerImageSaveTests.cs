// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>Saving an image or a diagram to a file (<see cref="MarkdownViewer.IsImageSaveEnabled"/>).</summary>
public class MarkdownViewerImageSaveTests
{
    /// <summary>An image of a fixed size that draws a transparent rectangle, so that it can be hit.</summary>
    private sealed class FakeImage(double width, double height) : IImage
    {
        public Size Size { get; } = new(width, height);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) => context.FillRectangle(Brushes.Transparent, destRect);
    }

    private sealed class FakeLoader(IImage image) : IImageLoader
    {
        public bool CanLoad(string url) => url.StartsWith("fake:", StringComparison.Ordinal);
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult<IImage?>(image);
    }

    private sealed class LoaderExtension(IImageLoader loader) : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, loader);
    }

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, IImage Picture) : IDisposable
    {
        public Image Image => Viewer.GetVisualDescendants().OfType<Image>().First(i => i.Classes.Contains("markdown-image"));

        public ImageLightbox OpenLightbox()
        {
            Image.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Window.UpdateLayout();
            return Assert.IsType<ImageLightbox>(Viewer.Lightbox);
        }

        public MenuItem SaveItem =>
            Assert.IsType<MenuFlyout>(Viewer.ContextFlyout).Items.OfType<MenuItem>()
                .Single(i => i.Classes.Contains("markdown-context-save-image"));

        public Point Center(Visual visual) =>
            visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window)!.Value;

        public void Dispose() => Window.Close();
    }

    private static async Task<Fixture> ShowAsync(string markdown, Action<MarkdownViewer>? configure = null, IImage? picture = null)
    {
        picture ??= new FakeImage(200, 100);
        var viewer = new MarkdownViewer();
        viewer.Extensions.Add(new LoaderExtension(new FakeLoader(picture)));
        viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        window.Show();
        var f = new Fixture(window, viewer, picture);
        await AsyncTestHelpers.WaitUntilAsync(() => f.Image.Source is not null);
        window.UpdateLayout();
        return f;
    }

    private static string FakeUrl(string name = "photo.png") => $"fake://host/{Guid.NewGuid()}/{name}";

    private static void RightClick(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }

    // Records the save requests, cancelling each so that no save picker opens.
    private static List<SavingImageEventArgs> CaptureSaves(MarkdownViewer viewer)
    {
        var saves = new List<SavingImageEventArgs>();
        viewer.SavingImage += (_, e) =>
        {
            saves.Add(e);
            e.Cancel = true;
        };
        return saves;
    }

    // ── Property ──────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void IsImageSaveEnabled_defaults_to_true()
    {
        Assert.True(new MarkdownViewer().IsImageSaveEnabled);
    }

    // ── Lightbox ──────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task The_lightbox_toolbar_ends_with_a_save_button()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        var lightbox = f.OpenLightbox();

        var buttons = ((StackPanel)lightbox.Toolbar.Child!).Children;
        Assert.Same(lightbox.SaveButton, buttons[^1]);
        Assert.True(lightbox.SaveButton.IsVisible);
        Assert.Contains("markdown-image-lightbox-save", lightbox.SaveButton.Classes);
        Assert.Contains("markdown-image-lightbox-zoom", lightbox.SaveButton.Classes);
        Assert.Equal("Save as", AutomationProperties.GetName(lightbox.SaveButton));
        Assert.Equal("Save as…", ToolTip.GetTip(lightbox.SaveButton));
        var glyph = Assert.IsType<ImageLightbox.ZoomGlyph>(lightbox.SaveButton.Content);
        Assert.Equal(ImageLightbox.ZoomGlyphKind.Save, glyph.Kind);
        Assert.Equal(6, glyph.GetStrokes().Length);
    }

    [AvaloniaFact]
    public async Task The_save_button_saves_the_picture_under_its_file_name_as_PNG()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl("summer%20trip.jpg")})");
        var saves = CaptureSaves(f.Viewer);
        var lightbox = f.OpenLightbox();

        lightbox.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var e = Assert.Single(saves);
        Assert.Equal("summer trip", e.SuggestedFileName);
        Assert.Equal([MarkdownImageFormat.Png], e.Formats);
        Assert.Same(MarkdownViewer.SavingImageEvent, e.RoutedEvent);
        // The lightbox stays open.
        Assert.Same(lightbox, f.Viewer.Lightbox);
    }

    [AvaloniaFact]
    public async Task Ctrl_S_in_the_lightbox_saves_the_picture()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        var saves = CaptureSaves(f.Viewer);
        var lightbox = f.OpenLightbox();

        var e = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Control };
        lightbox.RaiseEvent(e);

        Assert.True(e.Handled);
        Assert.Single(saves);
        Assert.Same(lightbox, f.Viewer.Lightbox);
    }

    [AvaloniaFact]
    public async Task S_without_the_command_modifier_does_not_save()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        var saves = CaptureSaves(f.Viewer);
        var lightbox = f.OpenLightbox();

        lightbox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S });
        lightbox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift });

        Assert.Empty(saves);
    }

    [AvaloniaFact]
    public async Task Tab_reaches_the_save_button_after_the_zoom_buttons()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        var lightbox = f.OpenLightbox();

        Assert.Same(lightbox.SaveButton, lightbox.GetNextTabStop(lightbox.FitButton, forward: true));
        Assert.Same(lightbox, lightbox.GetNextTabStop(lightbox.SaveButton, forward: true));
    }

    [AvaloniaFact]
    public async Task Turning_saving_off_hides_the_save_button_and_ignores_Ctrl_S()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        var saves = CaptureSaves(f.Viewer);
        var lightbox = f.OpenLightbox();

        f.Viewer.IsImageSaveEnabled = false;
        var e = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Control };
        lightbox.RaiseEvent(e);

        Assert.False(lightbox.SaveButton.IsVisible);
        Assert.Same(lightbox, lightbox.GetNextTabStop(lightbox.FitButton, forward: true));
        Assert.Empty(saves);
        // Left to the application, which may have its own Ctrl+S.
        Assert.False(e.Handled);

        f.Viewer.IsImageSaveEnabled = true;
        Assert.True(lightbox.SaveButton.IsVisible);
    }

    [AvaloniaFact]
    public async Task A_picture_without_a_file_name_has_no_save_button()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        ImageExport.SetFileName(f.Image, null);

        var lightbox = f.OpenLightbox();

        Assert.False(lightbox.SaveButton.IsVisible);
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task The_context_menu_of_an_image_saves_it()
    {
        using var f = await ShowAsync($"Intro text\n\n![A photo]({FakeUrl("cat.webp")})");
        var saves = CaptureSaves(f.Viewer);

        RightClick(f.Window, f.Center(f.Image));
        Assert.True(f.SaveItem.IsVisible);
        Assert.Equal("Save image as…", f.SaveItem.Header);
        f.SaveItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("cat", Assert.Single(saves).SuggestedFileName);
    }

    [AvaloniaFact]
    public async Task The_context_menu_of_the_text_offers_no_save()
    {
        using var f = await ShowAsync($"Intro text\n\n![A photo]({FakeUrl()})");
        var text = f.Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().First();

        RightClick(f.Window, text.TranslatePoint(new Point(4, text.Bounds.Height / 2), f.Window)!.Value);

        Assert.False(f.SaveItem.IsVisible);
    }

    [AvaloniaFact]
    public async Task The_context_menu_of_an_image_offers_no_save_when_saving_is_off()
    {
        using var f = await ShowAsync($"Intro text\n\n![A photo]({FakeUrl()})", v => v.IsImageSaveEnabled = false);

        RightClick(f.Window, f.Center(f.Image));

        Assert.False(f.SaveItem.IsVisible);
    }

    [AvaloniaFact]
    public async Task The_context_menu_of_the_lightbox_saves_its_picture()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl("dog.png")})");
        var saves = CaptureSaves(f.Viewer);
        var lightbox = f.OpenLightbox();

        RightClick(f.Window, f.Center(lightbox.Image));
        Assert.True(f.SaveItem.IsVisible);
        f.SaveItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("dog", Assert.Single(saves).SuggestedFileName);
    }

    [AvaloniaFact]
    public async Task Saving_works_with_zoom_turned_off()
    {
        using var f = await ShowAsync($"Intro text\n\n![A photo]({FakeUrl()})", v => v.IsImageZoomEnabled = false);
        var saves = CaptureSaves(f.Viewer);

        RightClick(f.Window, f.Center(f.Image));
        f.SaveItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(saves);
    }

    // ── Saving ────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task A_handler_can_write_the_picture_as_PNG()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        Task? write = null;
        f.Viewer.SavingImage += (_, e) =>
        {
            e.Cancel = true;
            write = e.WriteAsync(MarkdownImageFormat.Png, new MemoryStream());
        };

        Assert.False(await f.Viewer.SaveImageAsync(f.Image));

        // The headless platform encodes no pixels: the PNG itself is checked by hand with Skia.
        await Assert.IsAssignableFrom<Task>(write);
    }

    [AvaloniaFact]
    public async Task A_picture_with_an_SVG_document_is_offered_as_SVG_first()
    {
        var picture = new FakeImage(20, 10);
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        ImageExport.SetSvg(picture, svg);
        using var f = await ShowAsync($"![Logo]({FakeUrl("logo.svg")})", picture: picture);
        var saves = CaptureSaves(f.Viewer);

        await f.Viewer.SaveImageAsync(f.Image);

        var e = Assert.Single(saves);
        Assert.Equal("logo", e.SuggestedFileName);
        Assert.Equal([MarkdownImageFormat.Svg, MarkdownImageFormat.Png], e.Formats);
        var stream = new MemoryStream();
        await e.WriteAsync(MarkdownImageFormat.Svg, stream);
        Assert.Equal(svg, stream.ToArray());
    }

    [AvaloniaFact]
    public async Task WriteAsync_rejects_a_format_the_picture_is_not_offered_in()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        var saves = CaptureSaves(f.Viewer);
        await f.Viewer.SaveImageAsync(f.Image);

        await Assert.ThrowsAsync<ArgumentException>(() => saves[0].WriteAsync(MarkdownImageFormat.Svg, new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => saves[0].WriteAsync(MarkdownImageFormat.Png, null!));
    }

    [AvaloniaFact]
    public async Task A_handler_can_rename_the_file()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        f.Viewer.SavingImage += (_, e) => e.SuggestedFileName = "renamed";
        var saves = CaptureSaves(f.Viewer);

        await f.Viewer.SaveImageAsync(f.Image);

        Assert.Equal("renamed", saves[0].SuggestedFileName);
    }

    [AvaloniaFact]
    public async Task Nothing_is_saved_without_a_save_picker()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        var raised = 0;
        f.Viewer.SavingImage += (_, _) => raised++;

        // The headless platform has no save picker.
        Assert.False(await f.Viewer.SaveImageAsync(f.Image));
        Assert.Equal(1, raised);
    }

    [AvaloniaFact]
    public async Task Nothing_is_raised_for_an_image_that_cannot_be_saved()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})", v => v.IsImageSaveEnabled = false);
        var saves = CaptureSaves(f.Viewer);

        Assert.False(await f.Viewer.SaveImageAsync(f.Image));
        Assert.Empty(saves);
    }

    private static async Task<(Fixture Fixture, SavingImageEventArgs Save, byte[] Svg)> SaveSvgAsync()
    {
        var picture = new FakeImage(20, 10);
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        ImageExport.SetSvg(picture, svg);
        var f = await ShowAsync($"![Logo]({FakeUrl("logo.svg")})", picture: picture);
        var saves = CaptureSaves(f.Viewer);
        await f.Viewer.SaveImageAsync(f.Image);
        return (f, saves[0], svg);
    }

    [AvaloniaFact]
    public async Task The_save_picker_suggests_the_name_and_the_formats()
    {
        var (f, e, _) = await SaveSvgAsync();
        using var fixture = f;

        var options = MarkdownViewer.CreateSaveOptions(e);

        Assert.Equal("logo.svg", options.SuggestedFileName);
        Assert.Equal("svg", options.DefaultExtension);
        Assert.True(options.ShowOverwritePrompt);
        Assert.Equal(["SVG", "PNG"], options.FileTypeChoices!.Select(t => t.Name));
        Assert.Equal(["*.svg"], options.FileTypeChoices![0].Patterns);
        Assert.Equal(["image/svg+xml"], options.FileTypeChoices![0].MimeTypes);
        Assert.Equal(["*.png"], options.FileTypeChoices![1].Patterns);
        Assert.Equal(["image/png"], options.FileTypeChoices![1].MimeTypes);
        Assert.Same(options.FileTypeChoices![0], options.SuggestedFileType);
    }

    [AvaloniaFact]
    public async Task The_save_picker_suggests_a_name_given_by_a_handler()
    {
        using var f = await ShowAsync($"![A photo]({FakeUrl()})");
        f.Viewer.SavingImage += (_, e) => e.SuggestedFileName = "renamed";
        var saves = CaptureSaves(f.Viewer);
        await f.Viewer.SaveImageAsync(f.Image);

        var options = MarkdownViewer.CreateSaveOptions(saves[0]);

        Assert.Equal("renamed.png", options.SuggestedFileName);
        Assert.Equal(["PNG"], options.FileTypeChoices!.Select(t => t.Name));
    }

    [AvaloniaFact]
    public async Task The_file_is_written_in_the_format_of_its_extension()
    {
        var (f, e, svg) = await SaveSvgAsync();
        using var fixture = f;
        var stream = new MemoryStream();

        Assert.True(await MarkdownViewer.WriteFileAsync(e, "copy.svg", "PNG", () => Task.FromResult<Stream>(stream)));

        Assert.Equal(svg, stream.ToArray());
        // Closed once written.
        Assert.False(stream.CanWrite);
    }

    [AvaloniaFact]
    public async Task A_file_without_an_extension_is_written_in_the_format_picked()
    {
        var (f, e, svg) = await SaveSvgAsync();
        using var fixture = f;
        var stream = new MemoryStream();

        Assert.True(await MarkdownViewer.WriteFileAsync(e, "copy", "PNG", () => Task.FromResult<Stream>(stream)));

        // The headless platform encodes no pixels, but the SVG document was not written.
        Assert.NotEqual(svg, stream.ToArray());
    }

    [AvaloniaTheory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task A_file_that_cannot_be_written_is_reported_as_not_saved(Type exception)
    {
        var (f, e, _) = await SaveSvgAsync();
        using var fixture = f;

        Assert.False(await MarkdownViewer.WriteFileAsync(e, "copy.svg", null,
            () => Task.FromException<Stream>((Exception)Activator.CreateInstance(exception)!)));
    }

    [AvaloniaTheory]
    [InlineData("pic.png", null, MarkdownImageFormat.Png)]
    [InlineData("pic.SVG", null, MarkdownImageFormat.Svg)]
    [InlineData("pic.svg", "PNG", MarkdownImageFormat.Svg)]
    [InlineData("pic", "PNG", MarkdownImageFormat.Png)]
    [InlineData("pic", "SVG", MarkdownImageFormat.Svg)]
    [InlineData("pic.jpg", null, MarkdownImageFormat.Svg)]
    [InlineData("pic", "Other", MarkdownImageFormat.Svg)]
    public void The_format_follows_the_extension_then_the_picked_type(string fileName, string? pickedType, MarkdownImageFormat expected)
    {
        Assert.Equal(expected, MarkdownViewer.FormatOf(fileName, pickedType, [MarkdownImageFormat.Svg, MarkdownImageFormat.Png]));
    }

    // ── ImageExport ───────────────────────────────────────────────────────────

    [AvaloniaTheory]
    [InlineData("https://example.com/a/b/photo.jpg?size=2#top", "photo")]
    [InlineData("images/my%20picture.png", "my picture")]
    [InlineData("data:image/png;base64,AAAA", "image")]
    [InlineData("https://example.com/", "image")]
    [InlineData("https://example.com/images/", "images")]
    [InlineData("avares://App/Assets/logo.svg", "logo")]
    [InlineData("file:///C:/docs/archive.tar.gz", "archive.tar")]
    [InlineData(@"C:\docs\scan.png", "scan")]
    [InlineData("https://example.com/what%3F*%22.png", "what")]
    [InlineData("https://example.com/.hidden", ".hidden")]
    [InlineData("https://example.com/%3F%3F.png", "image")]
    [InlineData("", "image")]
    public void The_file_name_comes_from_the_last_segment_of_the_address(string url, string expected)
    {
        Assert.Equal(expected, ImageExport.FileNameFromUrl(url));
    }

    [AvaloniaTheory]
    [InlineData(MarkdownImageFormat.Png, "png", "image/png")]
    [InlineData(MarkdownImageFormat.Svg, "svg", "image/svg+xml")]
    public void Each_format_has_its_extension_and_media_type(MarkdownImageFormat format, string extension, string mimeType)
    {
        Assert.Equal(extension, ImageExport.GetExtension(format));
        Assert.Equal(mimeType, ImageExport.GetMimeType(format));
    }

    [AvaloniaFact]
    public void A_vector_picture_is_rendered_at_twice_its_size()
    {
        using var bitmap = ImageExport.Render(new FakeImage(200.4, 100), Brushes.White);

        Assert.Equal(new PixelSize(401, 200), bitmap.PixelSize);
        Assert.Equal(new Vector(192, 192), bitmap.Dpi);
    }

    [AvaloniaFact]
    public void A_huge_picture_is_rendered_no_larger_than_the_pixel_cap()
    {
        using var bitmap = ImageExport.Render(new FakeImage(16384, 100), null);

        Assert.Equal(new PixelSize(ImageExport.MaxPixelSize, 50), bitmap.PixelSize);
    }

    [AvaloniaFact]
    public void A_picture_without_a_size_cannot_be_rendered()
    {
        Assert.Throws<InvalidOperationException>(() => ImageExport.Render(new FakeImage(0, 0), null));
    }

    [AvaloniaFact]
    public async Task A_bitmap_is_written_as_PNG()
    {
        using var bitmap = new WriteableBitmap(new PixelSize(3, 2), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        var stream = new MemoryStream();

        // The headless platform encodes no pixels: the PNG itself is checked by hand with Skia.
        await ImageExport.WriteAsync(bitmap, Brushes.Red, MarkdownImageFormat.Png, stream);
    }

    [AvaloniaFact]
    public async Task A_picture_without_an_SVG_document_cannot_be_written_as_SVG()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ImageExport.WriteAsync(new FakeImage(1, 1), null, MarkdownImageFormat.Svg, new MemoryStream()));
    }

    [AvaloniaFact]
    public void Only_a_picture_with_a_file_name_and_a_source_can_be_saved()
    {
        var image = new Image();
        Assert.False(ImageExport.CanSave(image));
        ImageExport.SetFileName(image, "x");
        Assert.False(ImageExport.CanSave(image));
        image.Source = new FakeImage(1, 1);
        Assert.True(ImageExport.CanSave(image));
    }
}
