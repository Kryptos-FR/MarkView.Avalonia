// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="AutolinkInline"/> as a <see cref="MarkdownHyperlink"/> span,
/// matching regular <c>[text](url)</c> links.
/// </summary>
public sealed class AutolinkInlineRenderer : AvaloniaObjectRenderer<AutolinkInline>
{
    protected override void Write(AvaloniaRenderer renderer, AutolinkInline obj)
    {
        var rawUrl = obj.Url;
        var resolvedUrl = obj.IsEmail ? "mailto:" + rawUrl : renderer.ResolveUrl(rawUrl);

        var hyperlink = new MarkdownHyperlink
        {
            NavigateUri = Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri) ? uri : null,
        };
        hyperlink.Classes.Add("markdown-link");
        hyperlink.Inlines.Add(new Run(rawUrl));

        renderer.WriteInline(hyperlink);
    }
}
