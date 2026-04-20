// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using System.Text.RegularExpressions;

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="LinkInline"/> as a <see cref="MarkdownHyperlink"/> span or image.
/// YouTube video links (produced by UseMediaLinks) are rendered as clickable thumbnails.
/// </summary>
public sealed partial class LinkInlineRenderer : AvaloniaObjectRenderer<LinkInline>
{

    // Matches the "=WxH" title produced by MarkdownViewer's preprocessor.
    [GeneratedRegex(@"^=(\d+)x(\d+)$")]
    private static partial Regex DimensionTitleRegex();

    [GeneratedRegex(@"(?:youtu\.be/|[?&]v=)([A-Za-z0-9_\-]{11})")]
    private static partial Regex YoutubeIdRegex();

    protected override void Write(AvaloniaRenderer renderer, LinkInline obj)
    {
        if (obj.IsImage)
        {
            var videoId = ExtractYoutubeId(obj.Url);
            if (videoId is not null)
            {
                WriteYoutubeEmbed(renderer, obj, videoId);
                return;
            }
            WriteImage(renderer, obj);
            return;
        }

        var url = renderer.ResolveUrl(obj.Url ?? string.Empty);

        var hyperlink = new MarkdownHyperlink
        {
            NavigateUri = Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri) ? uri : null,
            Title = string.IsNullOrEmpty(obj.Title) ? null : obj.Title,
        };
        hyperlink.Classes.Add("markdown-link");

        renderer.Push(hyperlink.Inlines);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteInline(hyperlink);
    }

    private static void WriteImage(AvaloniaRenderer renderer, LinkInline obj)
    {
        var url = renderer.ResolveUrl(obj.Url ?? string.Empty);

        var sb = new System.Text.StringBuilder();
        foreach (var c in obj)
            if (c is LiteralInline literal) sb.Append(literal.Content.AsSpan());
        var altText = sb.ToString();

        var image = new Image { Stretch = Stretch.None };
        image.Classes.Add("markdown-image");

        if (!string.IsNullOrEmpty(altText))
            ToolTip.SetTip(image, altText);

        // Apply explicit dimensions from =WxH title (set by MarkdownViewer's preprocessor).
        if (!string.IsNullOrEmpty(obj.Title))
        {
            var dim = DimensionTitleRegex().Match(obj.Title);
            if (dim.Success)
            {
                image.Width = int.Parse(dim.Groups[1].Value, CultureInfo.InvariantCulture);
                image.Height = int.Parse(dim.Groups[2].Value, CultureInfo.InvariantCulture);
                image.Stretch = Stretch.Uniform;
            }
        }

        image.Tag = url;

        // Start loading on first attach; cancel on detach.
        // Deferring to AttachedToVisualTree avoids spurious cancellations from
        // Avalonia's layout passes that detach/reattach controls during initialisation.
        CancellationTokenSource? cts = null;
        image.AttachedToVisualTree += (_, _) =>
        {
            // Guard against both "already loaded" and "load already in flight".
            // AttachedToVisualTree can fire multiple times during layout passes; without
            // the second guard the first in-flight task would be cancelled and a new one
            // started, causing the image to never load.
            if (image.Source != null || cts != null) return;
            cts = new CancellationTokenSource();
            _ = LoadImageAsync(renderer, image, url, cts.Token);
        };
        image.DetachedFromLogicalTree += (_, _) => cts?.Cancel();

        renderer.WriteInline(image);
    }

    private static async Task LoadImageAsync(
        AvaloniaRenderer renderer,
        Image image,
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            // Try registered loaders first (priority order, index 0 = highest)
            foreach (var loader in renderer.ImageLoaders)
            {
                if (!loader.CanLoad(url))
                    continue;

                var loaded = await loader.LoadAsync(url, cancellationToken);
                if (loaded != null)
                {
                    image.Source = loaded;
                    return;
                }
            }

            // No loader claimed the URL — nothing to do.
            // (BitmapImageLoader is always registered last as the catch-all.)
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { }
        catch (IOException) { }
    }

    private static string? ExtractYoutubeId(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var match = YoutubeIdRegex().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static void WriteYoutubeEmbed(AvaloniaRenderer renderer, LinkInline obj, string videoId)
    {
        if (!Uri.TryCreate(obj.Url, UriKind.Absolute, out var videoUri)) return;

        var thumbnailUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";

        var thumbnail = new Image();
        thumbnail.Classes.Add("markdown-youtube-thumbnail");

        // Honour =WxH title syntax (same as regular images); falls back to theme dimensions.
        if (!string.IsNullOrEmpty(obj.Title))
        {
            var dim = DimensionTitleRegex().Match(obj.Title);
            if (dim.Success)
            {
                thumbnail.Width = int.Parse(dim.Groups[1].Value, CultureInfo.InvariantCulture);
                thumbnail.Height = int.Parse(dim.Groups[2].Value, CultureInfo.InvariantCulture);
                thumbnail.Stretch = Stretch.Uniform;
            }
        }

        var playOverlay = new TextBlock
        {
            Text = "▶",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        playOverlay.Classes.Add("markdown-youtube-play");

        var overlayGrid = new Grid();
        overlayGrid.Classes.Add("markdown-youtube-overlay");

        var button = new Button { Content = overlayGrid };
        button.Classes.Add("markdown-youtube");

        button.Click += async (_, _) =>
        {
            var launcher = TopLevel.GetTopLevel(button)?.Launcher;
            if (launcher is not null)
                await launcher.LaunchUriAsync(videoUri);
        };

        CancellationTokenSource? cts = null;
        thumbnail.AttachedToVisualTree += (_, _) =>
        {
            // Guard against both "already loaded" and "load already in flight".
            // AttachedToVisualTree fires multiple times during Avalonia's layout passes
            // (Button > Grid > Image nesting amplifies this). Without the second guard
            // each re-attach would cancel the previous in-flight fetch and restart it.
            if (thumbnail.Source != null || cts != null) return;
            cts = new CancellationTokenSource();
            _ = LoadImageAsync(renderer, thumbnail, thumbnailUrl, cts.Token);
        };
        // DetachedFromLogicalTree fires on genuine content replacement (e.g. Markdown property
        // change), unlike DetachedFromVisualTree which also fires during layout passes.
        thumbnail.DetachedFromLogicalTree += (_, _) => cts?.Cancel();

        overlayGrid.Children.Add(thumbnail);
        overlayGrid.Children.Add(playOverlay);
        renderer.WriteInline(button);
    }

}
