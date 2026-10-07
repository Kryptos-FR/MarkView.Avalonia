// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;
using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class YoutubeTests : RenderTestBase
{
    private const string YoutubeUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
    private const string YoutubeShortUrl = "https://youtu.be/dQw4w9WgXcQ";

    private static MarkdownPipeline MediaPipeline() =>
        new MarkdownPipelineBuilder().UseMediaLinks().Build();

    [AvaloniaFact]
    public void Youtube_image_renders_as_Button()
    {
        var result = Render($"![video]({YoutubeUrl})", MediaPipeline());
        var btn = FindFirst<Button>(result);
        Assert.NotNull(btn);
        Assert.Contains("markdown-youtube", btn!.Classes);
    }

    [AvaloniaFact]
    public void Youtube_short_url_renders_as_Button()
    {
        var result = Render($"![video]({YoutubeShortUrl})", MediaPipeline());
        var btn = FindFirst<Button>(result);
        Assert.NotNull(btn);
        Assert.Contains("markdown-youtube", btn!.Classes);
    }

    [AvaloniaFact]
    public void Youtube_button_contains_grid_with_image_and_play_overlay()
    {
        var result = Render($"![video]({YoutubeUrl})", MediaPipeline());
        var btn = FindFirst<Button>(result);
        Assert.NotNull(btn);
        var grid = Assert.IsType<Grid>(btn!.Content);
        Assert.True(grid.Children.Count >= 2, "Grid should have thumbnail Image and play overlay");
        Assert.Contains(grid.Children, c => c is Image);
        Assert.Contains(grid.Children, c => c is TextBlock tb && tb.Classes.Contains("markdown-youtube-play"));
    }

    [AvaloniaFact]
    public void Non_youtube_image_renders_without_button()
    {
        var result = Render("![img](https://example.com/image.png)", MediaPipeline());
        var btn = FindFirst<Button>(result);
        Assert.Null(btn);
    }

    private sealed class RecordingLoader : IImageLoader
    {
        public List<string> Requests { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public bool CanLoad(string url) => true;
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Requests.Add(url);
            Tokens.Add(cancellationToken);
            return Task.FromResult<IImage?>(null);
        }
    }

    private static AvaloniaRenderer RenderWith(string markdown, IImageLoader loader)
    {
        var pipeline = MediaPipeline();
        var renderer = new AvaloniaRenderer();
        renderer.ImageLoaders.Insert(0, loader);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer;
    }

    [AvaloniaFact]
    public void Youtube_embed_requests_the_hq_thumbnail_and_click_raises_LinkClicked()
    {
        var loader = new RecordingLoader();
        var renderer = RenderWith($"![]({YoutubeUrl})", loader);
        string? clicked = null;
        renderer.LinkClicked += (_, e) => clicked = e.Url;
        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(renderer.RootPanel.Children));
        Assert.Single(paragraph.Inlines!); // only the embed - no fallthrough to a plain image
        var button = FindFirst<Button>(renderer.RootPanel)!;
        var window = new Window { Content = renderer.RootPanel };
        try
        {
            window.Show();

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(["https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg"], loader.Requests);
            Assert.Equal(YoutubeUrl, clicked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Youtube_embed_structure_has_thumbnail_and_centered_play_overlay_classes()
    {
        var renderer = RenderWith($"![]({YoutubeShortUrl})", new RecordingLoader());
        var button = FindFirst<Button>(renderer.RootPanel)!;
        var grid = Assert.IsType<Grid>(button.Content);

        Assert.Contains("markdown-youtube", button.Classes);
        Assert.Contains("markdown-youtube-overlay", grid.Classes);
        Assert.Contains("markdown-youtube-thumbnail", Assert.IsType<Image>(grid.Children[0]).Classes);
        var play = Assert.IsType<TextBlock>(grid.Children[1]);
        Assert.Equal("▶", play.Text);
        Assert.Contains("markdown-youtube-play", play.Classes);
        Assert.Equal(HorizontalAlignment.Center, play.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, play.VerticalAlignment);
    }

    [AvaloniaFact]
    public void Youtube_embed_with_size_title_sets_thumbnail_size()
    {
        var renderer = RenderWith($"![]({YoutubeShortUrl} \"=320x180\")", new RecordingLoader());
        var thumbnail = Assert.IsType<Image>(Assert.IsType<Grid>(FindFirst<Button>(renderer.RootPanel)!.Content).Children[0]);

        Assert.Equal(320, thumbnail.Width);
        Assert.Equal(180, thumbnail.Height);
        Assert.Equal(Stretch.Uniform, thumbnail.Stretch);
    }

    [AvaloniaFact]
    public void Youtube_embed_with_a_non_dimension_title_keeps_theme_sizing()
    {
        var renderer = RenderWith($"![]({YoutubeShortUrl} \"A title\")", new RecordingLoader());
        var thumbnail = Assert.IsType<Image>(Assert.IsType<Grid>(FindFirst<Button>(renderer.RootPanel)!.Content).Children[0]);

        Assert.True(double.IsNaN(thumbnail.Width));
        Assert.True(double.IsNaN(thumbnail.Height));
    }

    [AvaloniaFact]
    public void Youtube_url_that_is_not_absolute_renders_nothing()
    {
        var loader = new RecordingLoader();
        var renderer = RenderWith("![x](watch?v=dQw4w9WgXcQ)", loader);

        Assert.Null(FindFirst<Button>(renderer.RootPanel));
        Assert.Null(FindFirst<Image>(renderer.RootPanel));
    }

    [AvaloniaFact]
    public async Task Youtube_thumbnail_is_requested_once_across_reattach()
    {
        var loader = new RecordingLoader();
        var renderer = RenderWith($"![]({YoutubeShortUrl})", loader);
        var window = new Window { Content = renderer.RootPanel };
        try
        {
            window.Show();
            window.Content = null;
            window.Content = renderer.RootPanel;
            await AsyncTestHelpers.PumpAsync();

            Assert.Single(loader.Requests);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_the_youtube_embed_cancels_the_thumbnail_load()
    {
        var loader = new RecordingLoader();
        var renderer = RenderWith($"![]({YoutubeShortUrl})", loader);
        var window = new Window { Content = renderer.RootPanel };
        try
        {
            window.Show();

            window.Content = null;

            Assert.True(Assert.Single(loader.Tokens).IsCancellationRequested);
        }
        finally
        {
            window.Close();
        }
    }

    private static T? FindFirst<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                var found = FindFirst<T>(child);
                if (found != null) return found;
            }
        }
        if (root is ContentControl cc && cc.Content is Control content)
            return FindFirst<T>(content);
        if (root is Decorator dec && dec.Child is Control decChild)
            return FindFirst<T>(decChild);
        if (root is TextBlock tb && tb.Inlines != null)
        {
            foreach (var inline in tb.Inlines)
            {
                if (inline is InlineUIContainer iuc && iuc.Child is Control inlineChild)
                {
                    var found = FindFirst<T>(inlineChild);
                    if (found != null) return found;
                }
            }
        }
        return null;
    }
}
