// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
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
