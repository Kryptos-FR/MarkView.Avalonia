// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extensions for rendering the matches of a pattern in a document's text as an
/// application's own inline (see <see cref="InlinePatternExtension"/>) or as a link (see
/// <see cref="LinkPatternExtension"/>).
/// </summary>
public static class MarkdownViewerInlinePatternExtensions
{
    /// <summary>
    /// Adds an <see cref="InlinePatternExtension"/> to the extension list.
    /// </summary>
    /// <example>
    /// <code>
    /// MarkdownViewerDefaults.Extensions.AddInlinePattern(new Regex(@"(?&lt;!\w)@(\w+)"),
    ///     match => new Run(match.Value) { FontWeight = FontWeight.SemiBold });
    /// </code>
    /// </example>
    public static void AddInlinePattern(this IList<IMarkViewExtension> extensions, Regex pattern, InlinePatternFactory factory)
    {
        extensions.Add(new InlinePatternExtension(pattern, factory));
    }

    /// <summary>
    /// Adds a <see cref="LinkPatternExtension"/> to the extension list.
    /// </summary>
    /// <example>
    /// <code>
    /// MarkdownViewerDefaults.Extensions.AddLinkPattern(new Regex(@"(?&lt;![\w&amp;])#(\d+)\b"),
    ///     match => $"https://github.com/owner/repo/issues/{match.Groups[1].Value}");
    /// </code>
    /// </example>
    public static void AddLinkPattern(this IList<IMarkViewExtension> extensions, Regex pattern, LinkPatternFactory url)
    {
        extensions.Add(new LinkPatternExtension(pattern, url));
    }

    /// <summary>
    /// Renders each match of <paramref name="pattern"/> in the viewer's text as the inline
    /// <paramref name="factory"/> returns, replacing an inline or link pattern the viewer's
    /// <see cref="MarkdownViewer.Extensions"/> already had for the same pattern, and re-renders the document.
    /// </summary>
    /// <example>
    /// <code>
    /// viewer.UseInlinePattern(new Regex(@"(?&lt;!\w)@(\w+)"), match => new Run(match.Value) { FontWeight = FontWeight.SemiBold });
    /// </code>
    /// </example>
    public static MarkdownViewer UseInlinePattern(this MarkdownViewer viewer, Regex pattern, InlinePatternFactory factory) =>
        Use(viewer, new InlinePatternExtension(pattern, factory));

    /// <summary>
    /// Renders each match of <paramref name="pattern"/> in the viewer's text as a link to the address
    /// <paramref name="url"/> gives, replacing an inline or link pattern the viewer's
    /// <see cref="MarkdownViewer.Extensions"/> already had for the same pattern, and re-renders the document.
    /// </summary>
    /// <example>
    /// <code>
    /// viewer.UseLinkPattern(new Regex(@"(?&lt;![\w&amp;])#(\d+)\b"),
    ///     match => $"https://github.com/owner/repo/issues/{match.Groups[1].Value}");
    /// </code>
    /// </example>
    public static MarkdownViewer UseLinkPattern(this MarkdownViewer viewer, Regex pattern, LinkPatternFactory url) =>
        Use(viewer, new LinkPatternExtension(pattern, url));

    private static MarkdownViewer Use(MarkdownViewer viewer, IMarkViewExtension extension)
    {
        var pattern = PatternOf(extension)!;
        var extensions = viewer.Extensions;
        for (var i = extensions.Count - 1; i >= 0; i--)
        {
            if (PatternOf(extensions[i]) is { } existing && SamePattern(existing, pattern))
                extensions.RemoveAt(i);
        }
        extensions.Add(extension);
        viewer.RenderMarkdown();
        return viewer;
    }

    private static Regex? PatternOf(IMarkViewExtension extension) => extension switch
    {
        InlinePatternExtension inline => inline.Pattern,
        LinkPatternExtension link => link.Pattern,
        _ => null,
    };

    private static bool SamePattern(Regex a, Regex b) =>
        a.Options == b.Options && string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
}
