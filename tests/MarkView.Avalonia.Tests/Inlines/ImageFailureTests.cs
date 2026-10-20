// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Net;

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

/// <summary>The placeholder an image that cannot be loaded is replaced with.</summary>
public sealed class ImageFailureTests : IDisposable
{
    private readonly LoopbackHttpServer _server = new(new Dictionary<string, Action<HttpListenerResponse>>());

    public void Dispose() => _server.Dispose();

    private sealed record Fixture(Window Window, AvaloniaRenderer Renderer) : IDisposable
    {
        /// <summary>The inline container the paragraph's first image was written into.</summary>
        public InlineUIContainer Slot =>
            Assert.IsType<InlineUIContainer>(Assert.IsAssignableFrom<TextBlock>(Renderer.RootPanel.Children[0]).Inlines![0]);

        public Task WaitForPlaceholderAsync() => AsyncTestHelpers.WaitUntilAsync(() => Slot.Child is Border);

        public Border Placeholder => Assert.IsType<Border>(Slot.Child);

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown, params IImageLoader[] loaders) => Show(markdown, theme: false, loaders);

    private static Fixture Show(string markdown, bool theme, params IImageLoader[] loaders)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        if (theme)
            renderer.Themes = RenderTestBase.MarkdownThemes();
        for (var i = loaders.Length - 1; i >= 0; i--)
            renderer.ImageLoaders.Insert(0, loaders[i]);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        var window = new Window { Width = 600, Height = 400, Content = renderer.RootPanel };
        if (theme)
            window.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        window.Show();
        return new Fixture(window, renderer);
    }

    private sealed class Loader(Func<string, CancellationToken, Task<IImage?>> load) : IImageLoader
    {
        public bool CanLoad(string url) => url.StartsWith("fake:", StringComparison.Ordinal);
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) => load(url, cancellationToken);
    }

    private sealed class FakeImage : IImage
    {
        public Size Size => new(40, 30);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
    }

    private static string FakeUrl() => $"fake://{Guid.NewGuid()}.png";

    [AvaloniaFact]
    public async Task An_image_no_loader_accepts_shows_the_placeholder()
    {
        using var f = Show("![A diagram](unknown://diagram.png)");

        await f.WaitForPlaceholderAsync();

        Assert.Contains("markdown-image-failed", f.Placeholder.Classes);
    }

    [AvaloniaFact]
    public async Task Screen_readers_see_the_placeholder_as_the_image_that_could_not_be_loaded()
    {
        using var f = Show("![A diagram](unknown://diagram.png)", theme: true);

        await f.WaitForPlaceholderAsync();
        var peer = ControlAutomationPeer.CreatePeerForElement(f.Placeholder);

        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
        Assert.Equal("A diagram", peer.GetName());
        Assert.Equal("Image could not be loaded", peer.GetHelpText());
    }

    [AvaloniaFact]
    public async Task A_placeholder_without_alt_text_is_hidden_as_decorative()
    {
        using var f = Show("![](unknown://divider.png)");

        await f.WaitForPlaceholderAsync();
        var peer = ControlAutomationPeer.CreatePeerForElement(f.Placeholder);

        Assert.False(peer.IsControlElement());
        Assert.False(peer.IsContentElement());
    }

    [AvaloniaFact]
    public async Task An_image_without_url_shows_the_placeholder()
    {
        using var f = Show("![A diagram]()");

        await f.WaitForPlaceholderAsync();
    }

    [AvaloniaFact]
    public async Task An_image_whose_loader_throws_shows_the_placeholder()
    {
        using var f = Show($"![A diagram]({FakeUrl()})", new Loader((_, _) => throw new InvalidOperationException("broken loader")));

        await f.WaitForPlaceholderAsync();
    }

    [AvaloniaFact]
    public async Task An_image_whose_loader_fails_asynchronously_shows_the_placeholder()
    {
        using var f = Show($"![A diagram]({FakeUrl()})", new Loader(async (_, _) =>
        {
            await Task.Yield();
            throw new IOException("bad data");
        }));

        await f.WaitForPlaceholderAsync();
    }

    [AvaloniaFact]
    public async Task An_image_whose_loader_times_out_shows_the_placeholder()
    {
        // HttpClient reports its own timeout as a cancellation the caller did not ask for.
        using var f = Show($"![A diagram]({FakeUrl()})", new Loader((_, _) => Task.FromException<IImage?>(new TaskCanceledException("timeout"))));

        await f.WaitForPlaceholderAsync();
    }

    [AvaloniaFact]
    public async Task An_image_every_loader_returns_nothing_for_shows_the_placeholder()
    {
        using var f = Show($"![A diagram]({FakeUrl()})",
            new Loader((_, _) => Task.FromResult<IImage?>(null)), new Loader((_, _) => Task.FromResult<IImage?>(null)));

        await f.WaitForPlaceholderAsync();
    }

    [AvaloniaFact]
    public async Task A_missing_remote_image_shows_the_placeholder()
    {
        using var f = Show($"![A diagram]({_server.Url("missing.png")})");

        await f.WaitForPlaceholderAsync();
    }

    [AvaloniaFact]
    public async Task A_loaded_image_shows_no_placeholder()
    {
        var image = new FakeImage();
        using var f = Show($"![A diagram]({FakeUrl()})", new Loader((_, _) => Task.FromResult<IImage?>(image)));

        var shown = Assert.IsType<Image>(f.Slot.Child);
        await AsyncTestHelpers.WaitUntilAsync(() => shown.Source is not null);
        await AsyncTestHelpers.PumpAsync();

        Assert.Same(shown, f.Slot.Child);
        Assert.Same(image, shown.Source);
    }

    [AvaloniaFact]
    public async Task A_load_cancelled_by_removing_the_image_shows_no_placeholder()
    {
        var cancelled = false;
        using var f = Show($"![A diagram]({FakeUrl()})", new Loader(async (_, ct) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            finally
            {
                cancelled = ct.IsCancellationRequested;
            }
            return null;
        }));
        var image = Assert.IsType<Image>(f.Slot.Child);

        f.Window.Content = null;
        await AsyncTestHelpers.WaitUntilAsync(() => cancelled);
        await AsyncTestHelpers.PumpAsync();

        Assert.Same(image, f.Slot.Child);
    }

    [AvaloniaFact]
    public async Task The_placeholder_shows_a_glyph_then_the_alt_text_and_copies_the_alt_text()
    {
        using var f = Show("![A *diagram*](unknown://diagram.png)");
        await f.WaitForPlaceholderAsync();

        var placeholder = f.Placeholder;
        Assert.Equal("A ", MarkdownSelection.GetCopyText(placeholder));
        Assert.True(placeholder.ClipToBounds);
        Assert.Equal(double.PositiveInfinity, placeholder.MaxWidth);
        Assert.Equal(double.PositiveInfinity, placeholder.MaxHeight);

        var content = Assert.IsType<Grid>(placeholder.Child);
        Assert.Equal("Auto,1*", content.ColumnDefinitions.ToString());
        Assert.Equal(4, content.ColumnSpacing);
        Assert.Equal(2, content.Children.Count);
        var glyph = Assert.IsType<BrokenImageGlyph>(content.Children[0]);
        Assert.Equal(VerticalAlignment.Center, glyph.VerticalAlignment);
        Assert.Equal(0, Grid.GetColumn(glyph));
        var text = Assert.IsType<TextBlock>(content.Children[1]);
        Assert.Equal("A ", text.Text);
        Assert.Equal(1, Grid.GetColumn(text));
        Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);
        Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
        Assert.Equal(VerticalAlignment.Center, text.VerticalAlignment);
    }

    [AvaloniaFact]
    public async Task The_placeholder_of_an_image_without_alt_text_shows_only_the_glyph()
    {
        using var f = Show("![](unknown://diagram.png)");
        await f.WaitForPlaceholderAsync();

        var content = Assert.IsType<Grid>(f.Placeholder.Child);
        Assert.IsType<BrokenImageGlyph>(Assert.Single(content.Children));
        Assert.Equal(string.Empty, MarkdownSelection.GetCopyText(f.Placeholder));
    }

    [AvaloniaFact]
    public async Task A_size_hint_only_caps_the_placeholder()
    {
        using var f = Show("![A diagram with a long description](unknown://diagram.png \"=60x300\")");
        await f.WaitForPlaceholderAsync();

        var placeholder = f.Placeholder;
        Assert.Equal(60, placeholder.MaxWidth);
        Assert.Equal(300, placeholder.MaxHeight);
        await AsyncTestHelpers.WaitUntilAsync(() => placeholder.Bounds.Width > 0);
        Assert.InRange(placeholder.Bounds.Width, 21, 60);
        // The alt text is cut to fit.
        var text = (TextBlock)((Grid)placeholder.Child!).Children[1];
        Assert.True(text.TextLayout.TextLines[0].HasCollapsed);
        // As tall as one line of text, not as the hint.
        Assert.True(placeholder.Bounds.Height < 40, $"height {placeholder.Bounds.Height}");
    }

    [AvaloniaFact]
    public async Task Selecting_the_paragraph_copies_the_alt_text_of_a_failed_image()
    {
        var viewer = new MarkdownViewer { Markdown = "Before ![broken](unknown://diagram.png) after" };
        var window = new Window { Width = 600, Height = 400, Content = viewer };
        window.Show();
        var paragraph = Assert.IsAssignableFrom<TextBlock>(Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]).Children[0]);
        var slot = paragraph.Inlines!.OfType<InlineUIContainer>().Single();
        await AsyncTestHelpers.WaitUntilAsync(() => slot.Child is Border);

        viewer.SelectAll();

        Assert.Equal("Before broken after", viewer.GetSelectedText());
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light", "#52525B", "#F4F4F5", "#82828B")]
    [InlineData("Dark", "#A1A1AA", "#27272A", "#7A7A83")]
    public async Task The_theme_colours_the_placeholder(string variant, string foreground, string background, string border)
    {
        using var theme = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        var viewer = new MarkdownViewer { Markdown = "![broken](unknown://diagram.png)" };
        viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        var window = new Window { Width = 600, Height = 400, Content = viewer };
        window.Show();
        var paragraph = Assert.IsAssignableFrom<TextBlock>(Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]).Children[0]);
        var slot = Assert.IsType<InlineUIContainer>(paragraph.Inlines![0]);
        await AsyncTestHelpers.WaitUntilAsync(() => slot.Child is Border);
        var placeholder = (Border)slot.Child!;
        var text = Assert.IsType<TextBlock>(((Grid)placeholder.Child!).Children[1]);

        Assert.Equal(Color.Parse(foreground), Assert.IsAssignableFrom<ISolidColorBrush>(text.Foreground).Color);
        Assert.Equal(Color.Parse(background), Assert.IsAssignableFrom<ISolidColorBrush>(placeholder.Background).Color);
        Assert.Equal(Color.Parse(border), Assert.IsAssignableFrom<ISolidColorBrush>(placeholder.BorderBrush).Color);
        Assert.Equal(new Thickness(1), placeholder.BorderThickness);
        window.Close();
    }
}
