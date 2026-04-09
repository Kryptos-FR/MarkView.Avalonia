using Avalonia.Controls;
using Avalonia.Controls.Documents;
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
        _ = LoadImageAsync(image, url);

        renderer.WriteInline(image);
    }

    private static async Task LoadImageAsync(Image image, string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return;

            var bytes = await HttpClient.GetByteArrayAsync(uri);
            using var stream = new MemoryStream(bytes);
            image.Source = new Bitmap(stream);
        }
        catch (HttpRequestException) { }
        catch (IOException) { }
    }
}
