using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="LinkInline"/> as an Avalonia <see cref="HyperlinkButton"/> or image.
/// </summary>
public class LinkInlineRenderer : AvaloniaObjectRenderer<LinkInline>
{
    protected override void Write(AvaloniaRenderer renderer, LinkInline obj)
    {
        if (obj.IsImage)
        {
            WriteImage(renderer, obj);
            return;
        }

        var url = renderer.ResolveUrl(obj.Url ?? string.Empty);

        // Build the button content as a TextBlock with inlines
        var contentTextBlock = new TextBlock();
        renderer.Push(contentTextBlock.Inlines!);
        renderer.WriteChildren(obj);
        renderer.Pop();

        var button = new HyperlinkButton
        {
            Content = contentTextBlock,
        };

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            button.NavigateUri = uri;

        button.Classes.Add("markdown-link");

        button.Click += (_, _) => renderer.OnLinkClicked(url);

        renderer.WriteInline(button);
    }

    private static void WriteImage(AvaloniaRenderer renderer, LinkInline obj)
    {
        var url = renderer.ResolveUrl(obj.Url ?? string.Empty);

        // Build alt text from children
        var altText = string.Concat(obj.SelectMany(c =>
            c is LiteralInline literal ? literal.Content.ToString() : string.Empty));

        var image = new Image();
        image.Classes.Add("markdown-image");

        if (!string.IsNullOrEmpty(altText))
            ToolTip.SetTip(image, altText);

        image.Tag = url;

        renderer.WriteInline(image);
    }
}
