// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Media;

using Markdig.Extensions.CustomContainers;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="CustomContainer"/> (<c>:::name</c> fences, Markdig's
/// <c>UseCustomContainers()</c>) as a <c>Border.markdown-custom-container</c> around its blocks.
/// The border also gets the container's name as a class (<c>:::note</c> gives <c>note</c>), so each
/// kind of container can be styled on its own. Text after the name (<c>:::note Read this first</c>)
/// becomes a <c>markdown-custom-container-title</c> header.
/// </summary>
public sealed class CustomContainerRenderer : AvaloniaObjectRenderer<CustomContainer>
{
    protected override void Write(AvaloniaRenderer renderer, CustomContainer obj)
    {
        var contentPanel = new StackPanel { Spacing = 8 };

        if (!string.IsNullOrWhiteSpace(obj.Arguments))
        {
            var title = new MarkdownSelectableTextBlock
            {
                Text = obj.Arguments.Trim(),
                TextWrapping = TextWrapping.Wrap,
                Renderer = renderer,
            };
            title.Classes.Add("markdown-custom-container-title");
            renderer.ApplyTheme(title, "MarkdownCustomContainerTitleTheme");
            contentPanel.Children.Add(title);
        }

        renderer.Push(contentPanel);
        renderer.WriteChildren(obj);
        renderer.Pop();

        var border = new Border { Child = contentPanel };
        border.Classes.Add("markdown-custom-container");
        renderer.ApplyTheme(border, "MarkdownCustomContainerTheme");
        // Avalonia reserves names starting with ':' for pseudo-classes.
        if (!string.IsNullOrEmpty(obj.Info) && obj.Info[0] != ':')
            border.Classes.Add(obj.Info);

        renderer.WriteBlock(border);
    }
}
