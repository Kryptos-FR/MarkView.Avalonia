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
public sealed class HeadingRenderer : AvaloniaObjectRenderer<HeadingBlock>
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
        ExtractText(obj.Inline, sb);
        var slug = renderer.SlugGenerator.GenerateSlug(sb.ToString());
        textBlock.Tag = slug;
        renderer.RegisterAnchor(slug, textBlock);

        renderer.WriteBlock(textBlock);
    }

    /// <summary>
    /// Recursively extracts plain text from an inline tree, matching GitHub's behaviour:
    /// literal text and code-span content are included; container inlines are descended into.
    /// </summary>
    private static void ExtractText(Markdig.Syntax.Inlines.Inline? inline, System.Text.StringBuilder sb)
    {
        while (inline is not null)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    sb.Append(lit.Content.AsSpan());
                    break;
                case Markdig.Syntax.Inlines.CodeInline code:
                    sb.Append(code.Content);
                    break;
                case ContainerInline container:
                    ExtractText(container.FirstChild, sb);
                    break;
            }
            inline = inline.NextSibling;
        }
    }
}
