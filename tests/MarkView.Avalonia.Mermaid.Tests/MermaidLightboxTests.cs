// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

/// <summary>A rendered diagram opens in the viewer's lightbox (<see cref="MarkdownViewer.IsImageZoomEnabled"/>).</summary>
public class MermaidLightboxTests
{
    private const string Source = "graph TD\n  A --> B";
    private const string Diagram = $"```mermaid\n{Source}\n```";

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, Image Image) : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    private static async Task<Fixture> ShowAsync(Action<MarkdownViewer>? configure = null)
    {
        var viewer = new MarkdownViewer { IsImageZoomEnabled = true }.UseMermaid();
        configure?.Invoke(viewer);
        viewer.Markdown = Diagram;
        var window = new Window { Width = 800, Height = 600, Content = viewer };
        window.Show();
        var image = viewer.GetVisualDescendants().OfType<Image>().Single();
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        window.UpdateLayout();
        return new Fixture(window, viewer, image);
    }

    private static void RaiseKey(InputElement element, Key key) =>
        element.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    [AvaloniaFact]
    public async Task A_rendered_diagram_is_zoomable()
    {
        using var f = await ShowAsync();

        Assert.Same(ZoomCursor.Instance, f.Image.Cursor);
        Assert.True(f.Image.Focusable);
        Assert.Contains("markdown-image-zoomable", f.Image.Classes);
        Assert.True(ImageZoom.GetEnlargeToFit(f.Image));
    }

    [AvaloniaFact]
    public async Task Clicking_a_diagram_opens_it_in_the_lightbox_enlarged_to_fit()
    {
        using var f = await ShowAsync();
        // Inside the window: the diagram is as wide as the viewer, without a scroll viewer to cap it.
        var point = f.Image.TranslatePoint(new Point(f.Image.Bounds.Width / 2, 100), f.Window)!.Value;

        f.Window.MouseDown(point, MouseButton.Left);
        f.Window.MouseUp(point, MouseButton.Left);
        f.Window.UpdateLayout();

        var lightbox = Assert.IsType<ImageLightbox>(f.Viewer.Lightbox);
        Assert.Same(f.Image.Source, lightbox.Image.Source);
        Assert.Equal(Source, AutomationProperties.GetName(lightbox));
        var natural = f.Image.Source!.Size;
        var fit = Math.Min(Math.Min(800 / natural.Width, 600 / natural.Height), ImageLightbox.MaxFitEnlargement);
        Assert.True(fit > 1);
        Assert.Equal(fit, lightbox.Zoom, 9);
        Assert.Equal(natural.Width * fit, lightbox.Image.Bounds.Width, 6);
    }

    [AvaloniaFact]
    public async Task The_lightbox_shows_the_diagram_in_the_colours_of_a_new_theme()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = await ShowAsync();
        RaiseKey(f.Image, Key.Enter);
        var lightbox = f.Viewer.Lightbox!;
        var light = lightbox.Image.Source;

        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(lightbox.Backing.Background).Color);

        theme.Switch(ThemeVariant.Dark);

        await AsyncTestHelpers.WaitUntilAsync(() => !ReferenceEquals(f.Image.Source, light));
        Assert.Same(f.Image.Source, lightbox.Image.Source);
        Assert.Same(lightbox, f.Viewer.Lightbox);
        Assert.Equal(Color.Parse("#18181B"), Assert.IsAssignableFrom<ISolidColorBrush>(lightbox.Backing.Background).Color);

        // Back to a theme already rendered, from the diagram's cache.
        theme.Switch(ThemeVariant.Light);
        await AsyncTestHelpers.WaitUntilAsync(() => ReferenceEquals(f.Image.Source, light));
        Assert.Same(light, lightbox.Image.Source);
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(lightbox.Backing.Background).Color);
    }

    [AvaloniaFact]
    public void A_diagram_not_rendered_yet_does_not_open()
    {
        var viewer = new MarkdownViewer { IsImageZoomEnabled = true }.UseMermaid();
        viewer.Markdown = Diagram;
        var image = viewer.GetLogicalDescendants().OfType<Image>().Single();

        RaiseKey(image, Key.Enter);

        Assert.Null(viewer.Lightbox);
    }

    [AvaloniaFact]
    public async Task Turning_zoom_off_leaves_diagrams_inert()
    {
        using var f = await ShowAsync(v => v.IsImageZoomEnabled = false);

        RaiseKey(f.Image, Key.Enter);

        Assert.Null(f.Viewer.Lightbox);
        Assert.False(f.Image.Focusable);
        Assert.Null(f.Image.Cursor);
    }
}
