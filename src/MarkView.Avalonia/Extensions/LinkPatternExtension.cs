// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

using Avalonia.Controls.Documents;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Renders each match of a regular expression in the document's text (<c>#123</c>, <c>@bob</c>,
/// <c>JIRA-42</c>…) as a link to the address a <see cref="LinkPatternFactory"/> gives, like any other
/// link of the document: it raises <see cref="MarkdownViewer.LinkClicked"/>, takes the keyboard focus
/// and is read as a link by screen readers.
/// </summary>
/// <remarks>
/// The link shows the matched text and has the <c>markdown-link</c> and <c>markdown-pattern-link</c>
/// classes. A relative address resolves against the viewer's <see cref="MarkdownViewer.BaseUri"/>.
/// The pattern runs as an <see cref="InlinePatternExtension"/>'s does.
/// </remarks>
public sealed class LinkPatternExtension : IMarkViewExtension
{
    /// <summary>
    /// Creates a link pattern.
    /// </summary>
    /// <param name="pattern">The text to find. Empty matches are ignored.</param>
    /// <param name="url">Gives the address of each match.</param>
    public LinkPatternExtension(Regex pattern, LinkPatternFactory url)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(url);
        Pattern = pattern;
        Url = url;
    }

    /// <summary>The text to find.</summary>
    public Regex Pattern { get; }

    /// <summary>Gives the address of each match.</summary>
    public LinkPatternFactory Url { get; }

    public void Register(AvaloniaRenderer renderer) =>
        renderer.InlinePatterns.Add(new InlinePattern(Pattern, (r, match) => Url(match) is { } url ? CreateLink(r, url, match.Value) : null));

    private static MarkdownHyperlink CreateLink(AvaloniaRenderer renderer, string url, string text)
    {
        var resolved = renderer.ResolveUrl(url);
        var link = new MarkdownHyperlink
        {
            NavigateUri = Uri.TryCreate(resolved, UriKind.RelativeOrAbsolute, out var uri) ? uri : null,
        };
        link.Classes.Add("markdown-link");
        link.Classes.Add("markdown-pattern-link");
        link.Inlines.Add(new Run(text));
        return link;
    }
}
