// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Media.Imaging;

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="LinkInline"/> as a <see cref="MarkdownHyperlink"/> span or image.
/// </summary>
public class LinkInlineRenderer : AvaloniaObjectRenderer<LinkInline>
{
    private static readonly HttpClient HttpClient = new();

    protected override void Write(AvaloniaRenderer renderer, LinkInline obj)
    {
        if (obj.IsImage)
        {
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

        var altText = string.Concat(obj.SelectMany(c =>
            c is LiteralInline literal ? literal.Content.ToString() : string.Empty));

        var image = new Image();
        image.Classes.Add("markdown-image");

        if (!string.IsNullOrEmpty(altText))
            ToolTip.SetTip(image, altText);

        image.Tag = url;

        // CancellationToken tied to visual-tree lifetime
        var cts = new CancellationTokenSource();
        image.DetachedFromVisualTree += (_, _) => cts.Cancel();

        _ = LoadImageAsync(renderer, image, url, cts.Token);

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

            // HTTP fallback for absolute URLs
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return;

            var bytes = await HttpClient.GetByteArrayAsync(uri, cancellationToken);
            using var stream = new MemoryStream(bytes);
            image.Source = new Bitmap(stream);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException) { }
        catch (IOException) { }
    }
}
