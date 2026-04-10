using Avalonia.Media;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="HeadingBlock"/> as a <see cref="MarkdownSelectableTextBlock"/>
/// and registers it as an anchor target for in-document navigation.
/// </summary>
public class HeadingRenderer : AvaloniaObjectRenderer<HeadingBlock>
{
    protected override void Write(AvaloniaRenderer renderer, HeadingBlock obj)
    {
        var textBlock = new MarkdownSelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Renderer = renderer,
        };
        textBlock.Classes.Add("markdown-heading");
        textBlock.Classes.Add($"markdown-h{obj.Level}");

        renderer.Push(textBlock.Inlines!);
        renderer.WriteLeafInline(obj);
        renderer.Pop();

        // Generate anchor slug and register for fragment navigation
        var headingText = string.Concat(obj.Inline?.SelectMany(
            c => c is LiteralInline lit ? lit.Content.ToString() : string.Empty)
            ?? []);
        var slug = renderer.SlugGenerator.GenerateSlug(headingText);
        textBlock.Tag = slug;
        renderer.RegisterAnchor(slug, textBlock);

        renderer.WriteBlock(textBlock);
    }
}
