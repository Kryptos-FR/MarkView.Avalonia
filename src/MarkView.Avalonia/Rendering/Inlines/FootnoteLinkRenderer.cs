// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;

using Markdig.Extensions.Footnotes;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="FootnoteLink"/> inline as a clickable superscript reference.
/// Clicking navigates to the footnote definition anchor (fn-N). Back-links are skipped.
/// </summary>
public sealed class FootnoteLinkRenderer : AvaloniaObjectRenderer<FootnoteLink>
{
    protected override void Write(AvaloniaRenderer renderer, FootnoteLink obj)
    {
        if (obj.IsBackLink) return;

        var hyperlink = new MarkdownHyperlink
        {
            NavigateUri = new Uri($"#fn-{obj.Footnote.Order}", UriKind.Relative),
        };
        hyperlink.Inlines.Add(new Run { Text = $"[{obj.Footnote.Order}]" });
        hyperlink.Classes.Add("markdown-footnote-ref");
        renderer.WriteInline(hyperlink);
    }
}
