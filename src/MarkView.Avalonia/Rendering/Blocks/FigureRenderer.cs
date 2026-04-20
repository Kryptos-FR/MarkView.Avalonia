// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Markdig.Extensions.Figures;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders Markdig <see cref="Figure"/> blocks. <see cref="FigureCaption"/> children
/// are rendered as an italic centred caption below the figure content.
/// </summary>
public sealed class FigureRenderer : AvaloniaObjectRenderer<Figure>
{
    protected override void Write(AvaloniaRenderer renderer, Figure obj)
    {
        var contentPanel = new StackPanel { Spacing = 4 };

        foreach (var child in obj)
        {
            if (child is FigureCaption caption)
            {
                var captionBlock = new MarkdownSelectableTextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Renderer = renderer,
                };
                captionBlock.Classes.Add("markdown-figure-caption");
                renderer.Push(captionBlock.Inlines!);
                renderer.WriteLeafInline(caption);
                renderer.Pop();
                contentPanel.Children.Add(captionBlock);
            }
            else
            {
                renderer.Push(contentPanel);
                renderer.Write(child);
                renderer.Pop();
            }
        }

        var border = new Border
        {
            Child = contentPanel,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        border.Classes.Add("markdown-figure");

        renderer.WriteBlock(border);
    }
}
