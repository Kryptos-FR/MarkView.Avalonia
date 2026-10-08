// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Svg.Skia;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

public class MermaidDiagramRenderingTests
{
    private const string Diagram = """
        ```mermaid
        graph TD
          A --> B
        ```
        """;
    private const string InvalidDiagram = """
        ```mermaid
        this is not a diagram
        second line
        ```
        """;

    private static StackPanel Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        new MermaidExtension().Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    // A render that fails immediately (empty or invalid source) can complete before Write returns,
    // so the border may already hold the fallback panel instead of the image.
    private static Border RenderBorder(string markdown) =>
        Assert.IsType<Border>(Assert.Single(Render(markdown).Children));

    private static (Border Border, Image Image) RenderDiagram(string markdown = Diagram)
    {
        var border = RenderBorder(markdown);
        return (border, Assert.IsType<Image>(border.Child));
    }

    [AvaloniaFact]
    public async Task Valid_diagram_is_rendered_to_an_svg_image()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (border, image) = RenderDiagram();

        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);

        var svg = Assert.IsType<SvgImage>(image.Source);
        Assert.NotNull(svg.Source);
        Assert.Equal(Stretch.Uniform, image.Stretch);
        Assert.Equal(HorizontalAlignment.Left, image.HorizontalAlignment);
        Assert.Contains("markdown-mermaid", border.Classes);
    }

    [AvaloniaFact]
    public async Task Invalid_diagram_is_replaced_by_error_and_source_text()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var border = RenderBorder(InvalidDiagram);

        await AsyncTestHelpers.WaitUntilAsync(() => border.Child is StackPanel);

        var panel = (StackPanel)border.Child!;
        var texts = panel.Children.Cast<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(2, texts.Count);
        Assert.StartsWith("Mermaid render error: ", texts[0]);
        Assert.Equal("this is not a diagram\nsecond line", texts[1]); // multi-line source joined with '\n'
        Assert.Equal(["markdown-mermaid-fallback"], border.Classes.Where(c => !c.StartsWith(':')).ToArray());
        Assert.Equal(4, panel.Spacing);
    }

    [AvaloniaFact]
    public async Task Empty_diagram_is_replaced_by_error_and_empty_source_text()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var border = RenderBorder("""
            ```mermaid
            ```
            """);

        await AsyncTestHelpers.WaitUntilAsync(() => border.Child is StackPanel);

        var panel = (StackPanel)border.Child!;
        var texts = panel.Children.Cast<TextBlock>().Select(t => t.Text).ToList();
        Assert.StartsWith("Mermaid render error: ", texts[0]);
        Assert.Equal(string.Empty, texts[1]);
    }

    [AvaloniaFact]
    public async Task Theme_switch_re_renders_the_diagram()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Diagram);
        var image = Assert.IsType<Image>(((Border)root.Children[0]).Child);
        var window = new Window { Content = root };
        try
        {
            window.Show();
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
            var lightSource = image.Source;

            theme.Switch(ThemeVariant.Dark);

            await AsyncTestHelpers.WaitUntilAsync(() => !ReferenceEquals(image.Source, lightSource));
            Assert.IsType<SvgImage>(image.Source);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Theme_switch_during_a_render_discards_the_superseded_result()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Diagram);
        var image = Assert.IsType<Image>(((Border)root.Children[0]).Child);
        var sources = new List<IImage?>();
        image.PropertyChanged += (_, e) =>
        {
            if (e.Property == Image.SourceProperty) sources.Add(image.Source);
        };
        var window = new Window { Content = root };
        try
        {
            // The light render's result is only applied once the UI thread yields, so the switch
            // below happens while it is still in flight.
            window.Show();
            theme.Switch(ThemeVariant.Dark);

            await AsyncTestHelpers.WaitUntilAsync(() => sources.Count > 0);
            await AsyncTestHelpers.PumpAsync();

            Assert.IsType<SvgImage>(Assert.Single(sources));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Never_attached_diagram_does_not_re_render_on_theme_change()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, image) = RenderDiagram();
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        var before = image.Source;

        theme.Switch(ThemeVariant.Dark);
        await AsyncTestHelpers.PumpAsync();

        Assert.Same(before, image.Source);
    }

    [AvaloniaTheory]
    [InlineData(1200)]
    [InlineData(400)]
    public async Task Diagram_width_is_capped_by_viewport_and_800(double windowWidth)
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Diagram);
        var image = Assert.IsType<Image>(((Border)root.Children[0]).Child);
        var scrollViewer = new ScrollViewer { Content = root };
        var window = new Window { Width = windowWidth, Height = 300, Content = scrollViewer };
        try
        {
            window.Show();
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);

            Assert.Equal(Math.Min(scrollViewer.Viewport.Width, 800), image.MaxWidth);
            Assert.True(image.MaxWidth > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Diagram_width_tracks_viewport_resizes()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Diagram);
        var image = Assert.IsType<Image>(((Border)root.Children[0]).Child);
        var scrollViewer = new ScrollViewer { Content = root };
        var window = new Window { Width = 600, Height = 300, Content = scrollViewer };
        try
        {
            window.Show();

            window.Width = 300;
            await AsyncTestHelpers.WaitUntilAsync(() => scrollViewer.Viewport.Width <= 300);

            Assert.Equal(scrollViewer.Viewport.Width, image.MaxWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Diagram_detached_during_a_theme_switch_re_renders_on_reattach()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Diagram);
        var image = Assert.IsType<Image>(((Border)root.Children[0]).Child);
        var scrollViewer = new ScrollViewer { Content = root };
        var window = new Window { Width = 600, Height = 300, Content = scrollViewer };
        try
        {
            window.Show();
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
            var before = image.Source;

            scrollViewer.Content = null;
            theme.Switch(ThemeVariant.Dark);
            await AsyncTestHelpers.PumpAsync();
            Assert.Same(before, image.Source);

            scrollViewer.Content = root;
            await AsyncTestHelpers.WaitUntilAsync(() => !ReferenceEquals(image.Source, before));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Unrelated_application_property_change_does_not_re_render()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, image) = RenderDiagram();
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        var before = image.Source;
        var app = Application.Current!;
        var savedName = app.Name;

        try
        {
            app.Name = "MermaidRenderingTests";
            await AsyncTestHelpers.PumpAsync();
        }
        finally
        {
            app.Name = savedName;
        }

        Assert.Same(before, image.Source);
    }

    [AvaloniaFact]
    public async Task Diagram_outside_a_scroll_viewer_keeps_unbounded_width()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Diagram);
        var image = Assert.IsType<Image>(((Border)root.Children[0]).Child);
        var window = new Window { Width = 600, Height = 300, Content = root };
        try
        {
            window.Show();

            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);

            Assert.Equal(double.PositiveInfinity, image.MaxWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Diagram_in_an_empty_viewport_keeps_unbounded_width()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Diagram);
        var image = Assert.IsType<Image>(((Border)root.Children[0]).Child);
        var scrollViewer = new ScrollViewer { Width = 0, Content = root };
        var window = new Window { Width = 600, Height = 300, Content = scrollViewer };
        try
        {
            window.Show();

            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);

            Assert.Equal(0, scrollViewer.Viewport.Width);
            Assert.Equal(double.PositiveInfinity, image.MaxWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Diagram_attached_to_a_laid_out_scroll_viewer_is_constrained_immediately()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Diagram);
        var image = Assert.IsType<Image>(((Border)root.Children[0]).Child);
        var scrollViewer = new ScrollViewer { Content = new Border() };
        var window = new Window { Width = 600, Height = 300, Content = scrollViewer };
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.True(scrollViewer.Viewport.Width > 0);

            scrollViewer.Content = root;

            Assert.Equal(Math.Min(scrollViewer.Viewport.Width, 800), image.MaxWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Detached_diagram_does_not_track_viewport_resizes()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Diagram);
        var image = Assert.IsType<Image>(((Border)root.Children[0]).Child);
        var scrollViewer = new ScrollViewer { Content = root };
        var window = new Window { Width = 600, Height = 300, Content = scrollViewer };
        try
        {
            window.Show();
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
            var widthWhileAttached = image.MaxWidth;
            Assert.True(widthWhileAttached > 0);

            scrollViewer.Content = new Border();
            window.Width = 300;
            window.UpdateLayout();
            await AsyncTestHelpers.PumpAsync();

            Assert.Equal(widthWhileAttached, image.MaxWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Never_attached_diagram_is_not_kept_alive_by_the_application()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (weakRoot, weakImage) = RenderUnreferenced();
        await AsyncTestHelpers.WaitUntilAsync(() => weakImage.Target is Image { Source: not null });

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weakRoot.IsAlive);
    }

    // Kept out of line so no local in the test method roots the rendered tree.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (WeakReference Root, WeakReference Image) RenderUnreferenced()
    {
        var root = Render(Diagram);
        return (new WeakReference(root), new WeakReference(((Border)root.Children[0]).Child));
    }

    [AvaloniaFact]
    public async Task Diagram_removed_from_tree_while_rendering_shows_its_image_when_reattached()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(Diagram);
        var image = Assert.IsType<Image>(((Border)root.Children[0]).Child);
        var scrollViewer = new ScrollViewer { Content = root };
        var window = new Window { Width = 600, Height = 300, Content = scrollViewer };
        try
        {
            // The render completes on a background thread but its result is only applied once the UI
            // thread yields, so detaching here happens while the render is still in flight.
            window.Show();
            scrollViewer.Content = null;
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);

            scrollViewer.Content = root;

            Assert.IsType<SvgImage>(image.Source);
        }
        finally
        {
            window.Close();
        }
    }
}
