// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

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

        var image = new Image { Stretch = Stretch.None };
        image.Classes.Add("markdown-image");

        if (!string.IsNullOrEmpty(altText))
            ToolTip.SetTip(image, altText);

        image.Tag = url;

        // Start loading on first attach; cancel on detach.
        // Deferring to AttachedToVisualTree avoids spurious cancellations from
        // Avalonia's layout passes that detach/reattach controls during initialisation.
        CancellationTokenSource? cts = null;
        image.AttachedToVisualTree += (_, _) =>
        {
            if (image.Source != null) return; // already loaded, no need to reload
            cts?.Cancel();
            cts = new CancellationTokenSource();
            _ = LoadImageAsync(renderer, image, url, cts.Token);
        };
        image.DetachedFromVisualTree += (_, _) => cts?.Cancel();

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
                    await Dispatcher.UIThread.InvokeAsync(() => image.Source = loaded);
                    return;
                }
            }

            // HTTP fallback for absolute URLs
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return;

            var bytes = await HttpClient.GetByteArrayAsync(uri, cancellationToken);
            var bitmap = new Bitmap(new MemoryStream(bytes));
            await Dispatcher.UIThread.InvokeAsync(() => image.Source = bitmap);
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException) { }
        catch (IOException) { }
    }
}
