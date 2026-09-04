// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="AutolinkInline"/> as an Avalonia <see cref="HyperlinkButton"/>.
/// </summary>
public sealed class AutolinkInlineRenderer : AvaloniaObjectRenderer<AutolinkInline>
{
    protected override void Write(AvaloniaRenderer renderer, AutolinkInline obj)
    {
        var rawUrl = obj.Url;
        var displayUrl = rawUrl;
        string resolvedUrl;

        if (obj.IsEmail)
        {
            resolvedUrl = "mailto:" + rawUrl;
        }
        else
        {
            resolvedUrl = renderer.ResolveUrl(rawUrl);
        }

        var contentTextBlock = new TextBlock();
        contentTextBlock.Inlines!.Add(new Run(displayUrl));

        var button = new HyperlinkButton
        {
            Content = contentTextBlock,
        };

        button.Classes.Add("markdown-link");

        if (Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri))
            button.NavigateUri = uri;

        // HyperlinkButton reads NavigateUri (and launches it via the platform launcher)
        // right after its Click event finishes, so clearing it here — once a LinkClicked
        // subscriber has handled the click itself — suppresses that default launch.
        button.Click += (_, _) =>
        {
            if (renderer.RaiseLinkClicked(resolvedUrl))
                button.NavigateUri = null;
        };

        renderer.WriteInline(button);
    }
}
