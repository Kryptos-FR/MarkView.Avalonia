// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

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
        var sb = new System.Text.StringBuilder();
        if (obj.Inline is not null)
            foreach (var c in obj.Inline)
                if (c is LiteralInline lit) sb.Append(lit.Content.AsSpan());
        var headingText = sb.ToString();
        var slug = renderer.SlugGenerator.GenerateSlug(headingText);
        textBlock.Tag = slug;
        renderer.RegisterAnchor(slug, textBlock);

        renderer.WriteBlock(textBlock);
    }
}
