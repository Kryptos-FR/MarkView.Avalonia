// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Parsing;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="LinkInline"/> as a <see cref="MarkdownHyperlink"/> span or image.
/// YouTube video links (produced by UseMediaLinks) are rendered as clickable thumbnails.
/// An image that cannot be loaded is replaced by a <c>markdown-image-failed</c> placeholder.
/// A link whose URL <see cref="AvaloniaRenderer.UrlTransform"/> drops is written as its text alone, and an image
/// as its placeholder, without being loaded.
/// While <see cref="AvaloniaRenderer.IsProvisional"/> holds for a link or image (its address may still be
/// growing), a link keeps its text, class and title but gets no <see cref="MarkdownHyperlink.NavigateUri"/>,
/// and an image (a YouTube one included) is written as its alt text in a plain <see cref="Run"/>, nothing being loaded.
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
        var provisional = renderer.IsProvisional(obj);
        if (obj.IsImage)
        {
            if (provisional)
            {
                WriteProvisionalImage(renderer, obj);
                return;
            }
            var videoId = ExtractYoutubeId(obj.Url);
            if (videoId is not null)
            {
                WriteYoutubeEmbed(renderer, obj, videoId);
                return;
            }
            WriteImage(renderer, obj);
            return;
        }

        // A provisional address is still growing: the transform judges it once complete.
        var url = provisional ? null : renderer.ResolveUrl(obj.Url ?? string.Empty);
        if (!provisional && url is null)
        {
            renderer.WriteChildren(obj);
            return;
        }

        var hyperlink = new MarkdownHyperlink
        {
            NavigateUri = url is not null && Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri) ? uri : null,
            Title = string.IsNullOrEmpty(obj.Title) ? null : obj.Title,
        };
        hyperlink.Classes.Add("markdown-link");

        renderer.Push(hyperlink.Inlines);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteInline(hyperlink);
    }

    /// <summary>
    /// Writes an image of the block that contains the end of the text as its alt text in a plain
    /// <see cref="Run"/>, or nothing without one: its address may still be growing, so nothing is loaded.
    /// </summary>
    private static void WriteProvisionalImage(AvaloniaRenderer renderer, LinkInline obj)
    {
        var altText = ExtractAltText(obj);
        if (!string.IsNullOrEmpty(altText))
            renderer.WriteInline(new Run(altText));
    }

    private static void WriteImage(AvaloniaRenderer renderer, LinkInline obj)
    {
        var url = renderer.ResolveUrl(obj.Url ?? string.Empty, MarkdownUrlKind.Image);
        var altText = ExtractAltText(obj);

        var image = new Image();
        ApplyResizeMode(image, renderer.ImageResizeMode);
        image.Classes.Add("markdown-image");

        if (!string.IsNullOrEmpty(altText))
        {
            ToolTip.SetTip(image, altText);
            AutomationProperties.SetName(image, altText);
        }
        else
        {
            // An image without alt text is treated as decorative, as browsers do with alt="".
            AutomationRoles.Hide(image);
        }
        MarkdownSelection.SetCopyText(image, altText);

        // Apply explicit dimensions from =WxH title (set by MarkdownViewer's preprocessor), or from the
        // width and height attributes of an HTML <img>, either of which can be missing.
        var (width, height) = ExplicitSize(obj);
        if (width is not null || height is not null)
        {
            // MaxWidth/MaxHeight (not Width/Height) so the layout box itself shrinks to
            // the image's native resolution when it's smaller than the requested size —
            // Width/Height would hard-pin the arranged size regardless of StretchDirection.
            if (width is not null)
                image.MaxWidth = width.Value;
            if (height is not null)
                image.MaxHeight = height.Value;
            image.Stretch = Stretch.Uniform;
            // "=WxH" is a ceiling, never a forced upscale, regardless of ImageResizeMode.
            image.StretchDirection = StretchDirection.DownOnly;
        }

        if (url is null)
        {
            renderer.WriteInline(image);
            ShowFailurePlaceholder(image, altText);
            return;
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
            _ = LoadImageAsync(renderer, image, url, cts.Token, () => ShowFailurePlaceholder(image, altText));
        };
        image.DetachedFromLogicalTree += (_, _) => cts?.Cancel();

        // An image inside a link opens the link instead.
        if (renderer.IsImageZoomEnabled && !IsInsideLink(obj))
            ImageZoom.MakeZoomable(renderer, image);

        renderer.WriteInline(image);
    }

    /// <summary>
    /// The size an image is capped to: the <see cref="HtmlImageSize"/> of an HTML <c>&lt;img&gt;</c>, or its
    /// "=WxH" title.
    /// </summary>
    private static (int? Width, int? Height) ExplicitSize(LinkInline obj)
    {
        if (obj.GetData(typeof(HtmlImageSize)) is HtmlImageSize size)
            return (size.Width, size.Height);
        if (!string.IsNullOrEmpty(obj.Title) && DimensionTitleRegex().Match(obj.Title) is { Success: true } dim)
            return (int.Parse(dim.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(dim.Groups[2].Value, CultureInfo.InvariantCulture));
        return (null, null);
    }

    private static bool IsInsideLink(LinkInline image)
    {
        for (var parent = image.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is LinkInline)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Replaces <paramref name="image"/>, which could not be loaded, with a <c>markdown-image-failed</c>
    /// placeholder: a broken-image glyph followed by the alt text, if any. It copies the same text as the
    /// image. A "=WxH" size hint does not size the placeholder, which keeps its own compact size: the hint
    /// only caps it, the alt text being cut with an ellipsis beyond the width.
    /// </summary>
    private static void ShowFailurePlaceholder(Image image, string altText)
    {
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 4 };
        content.Children.Add(new BrokenImageGlyph { VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrEmpty(altText))
        {
            var text = new TextBlock
            {
                Text = altText,
                // Not the paragraph's wrapping, which the text would otherwise inherit.
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(text, 1);
            content.Children.Add(text);
        }

        var placeholder = new Border
        {
            Child = content,
            MaxWidth = image.MaxWidth,
            MaxHeight = image.MaxHeight,
            ClipToBounds = true,
            // The address, as on the image, for copying the selection as Markdown.
            Tag = image.Tag,
        };
        placeholder.Classes.Add("markdown-image-failed");
        MarkdownSelection.SetCopyText(placeholder, altText);
        // Named by its alt text, the theme's style saying that it could not be loaded; decorative without one.
        if (string.IsNullOrEmpty(altText))
        {
            AutomationRoles.Hide(placeholder);
        }
        else
        {
            AutomationRoles.Set(placeholder, AutomationControlType.Image);
            AutomationProperties.SetName(placeholder, altText);
        }

        // Written by AvaloniaRenderer.WriteInline, the image is the child of an InlineUIContainer.
        if (image.Parent is InlineUIContainer container)
            container.Child = placeholder;
    }

    private static string ExtractAltText(LinkInline obj)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in obj)
            if (c is LiteralInline literal) sb.Append(literal.Content.AsSpan());
        return sb.ToString();
    }

    private static void ApplyResizeMode(Image image, ImageResizeMode mode)
    {
        switch (mode)
        {
            case ImageResizeMode.Natural:
                image.Stretch = Stretch.None;
                break;
            case ImageResizeMode.Fill:
                image.Stretch = Stretch.Uniform;
                image.StretchDirection = StretchDirection.Both;
                break;
            case ImageResizeMode.ScaleDownToFit:
            default:
                image.Stretch = Stretch.Uniform;
                image.StretchDirection = StretchDirection.DownOnly;
                break;
        }
    }

    /// <summary>
    /// Loads <paramref name="url"/> into <paramref name="image"/> through the renderer's image loaders
    /// (index 0 first), reading through its resource resolver, and the shared <see cref="ImageCache"/>. Calls <paramref name="failed"/> when no
    /// loader returns an image or a loader throws, but not when the load is cancelled.
    /// </summary>
    private static async Task LoadImageAsync(
        AvaloniaRenderer renderer,
        Image image,
        string url,
        CancellationToken cancellationToken,
        Action? failed = null)
    {
        IImage? loaded = null;
        try
        {
            loaded = await ImageCache.Shared.GetOrLoadAsync(url, renderer.ImageLoaders, cancellationToken, renderer.EffectiveResourceResolver);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        // Whatever a loader throws (an HTTP error, undecodable data, its own bug) is a failed load: this
        // task is never awaited, so nothing else would observe it.
        catch (Exception)
        {
        }

        if (loaded != null)
            image.Source = loaded;
        else
            failed?.Invoke();
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
        if (renderer.TransformUrl(videoUri.ToString(), MarkdownUrlKind.Link) is not { } videoUrl
            || !Uri.TryCreate(videoUrl, UriKind.Absolute, out videoUri))
        {
            // Without its link, the thumbnail would be a picture nobody asked for: written as a dropped image.
            var image = new Image();
            image.Classes.Add("markdown-image");
            renderer.WriteInline(image);
            ShowFailurePlaceholder(image, ExtractAltText(obj));
            return;
        }

        var thumbnailUrl = renderer.TransformUrl($"https://img.youtube.com/vi/{videoId}/hqdefault.jpg", MarkdownUrlKind.Image);

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
        button.Click += (_, _) => renderer.OnLinkClicked(videoUri.ToString());
        var altText = ExtractAltText(obj);
        MarkdownSelection.SetCopyText(button, altText);
        AutomationProperties.SetControlTypeOverride(button, AutomationControlType.Hyperlink);
        AutomationProperties.SetName(button, string.IsNullOrEmpty(altText) ? videoUri.ToString() : altText);

        CancellationTokenSource? cts = null;
        thumbnail.AttachedToVisualTree += (_, _) =>
        {
            // Guard against both "already loaded" and "load already in flight".
            // AttachedToVisualTree fires multiple times during Avalonia's layout passes
            // (Button > Grid > Image nesting amplifies this). Without the second guard
            // each re-attach would cancel the previous in-flight fetch and restart it.
            // A thumbnail the URL transform dropped is never loaded.
            if (thumbnail.Source != null || cts != null || thumbnailUrl is null) return;
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
