// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Xml.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

/// <summary>A rendered diagram can be saved as SVG or PNG (<see cref="MarkdownViewer.IsImageSaveEnabled"/>).</summary>
public class MermaidSaveTests
{
    private const string Diagram = "```mermaid\ngraph TD\n  A --> B\n```";

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, Image Image) : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    private static async Task<Fixture> ShowAsync()
    {
        var viewer = new MarkdownViewer { IsImageZoomEnabled = true }.UseMermaid();
        viewer.Markdown = Diagram;
        var window = new Window { Width = 800, Height = 600, Content = viewer };
        window.Show();
        var image = viewer.GetVisualDescendants().OfType<Image>().Single();
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        window.UpdateLayout();
        return new Fixture(window, viewer, image);
    }

    private static async Task<SavingImageEventArgs> SaveAsync(Fixture f)
    {
        SavingImageEventArgs? saved = null;
        f.Viewer.SavingImage += (_, e) =>
        {
            saved = e;
            e.Cancel = true;
        };
        await f.Viewer.SaveImageAsync(f.Image);
        return Assert.IsType<SavingImageEventArgs>(saved);
    }

    [AvaloniaFact]
    public async Task A_diagram_is_saved_as_SVG_or_PNG_under_the_name_diagram()
    {
        using var f = await ShowAsync();

        var e = await SaveAsync(f);

        Assert.Equal("diagram", e.SuggestedFileName);
        Assert.Equal([MarkdownImageFormat.Svg, MarkdownImageFormat.Png], e.Formats);
    }

    [AvaloniaTheory]
    [InlineData(false, "#FFFFFF")]
    [InlineData(true, "#18181B")]
    public async Task A_saved_SVG_is_painted_on_the_diagram_background(bool dark, string background)
    {
        using var theme = new ThemeScope(dark ? ThemeVariant.Dark : ThemeVariant.Light);
        using var f = await ShowAsync();
        var e = await SaveAsync(f);

        var stream = new MemoryStream();
        await e.WriteAsync(MarkdownImageFormat.Svg, stream);

        stream.Position = 0;
        var root = XDocument.Load(stream).Root!;
        var first = root.Elements().First();
        Assert.Equal("rect", first.Name.LocalName);
        Assert.Equal($"fill:{background};stroke:none", first.Attribute("style")!.Value);
        // The colours are resolved, so that programs without CSS variables draw them.
        Assert.DoesNotContain(root.Descendants(), e => e.Attributes().Any(a => a.Value.Contains("var(", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public async Task A_diagram_in_the_lightbox_has_a_save_button()
    {
        using var f = await ShowAsync();

        f.Image.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        Assert.True(f.Viewer.Lightbox!.SaveButton.IsVisible);
    }

    [AvaloniaFact]
    public async Task A_diagram_is_written_as_PNG()
    {
        using var f = await ShowAsync();
        var e = await SaveAsync(f);

        // The headless platform encodes no pixels: the PNG itself is checked by hand with Skia.
        await e.WriteAsync(MarkdownImageFormat.Png, new MemoryStream());
    }
}
