// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

using Avalonia.Controls.Documents;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Renders each match of a regular expression in the document's text (<c>@bob</c>, <c>:smile:</c>,
/// <c>JIRA-42</c>…) as the inline an <see cref="InlinePatternFactory"/> returns, without writing a
/// Markdig parser.
/// </summary>
/// <remarks>
/// <para>
/// The pattern runs on the document's plain text one run at a time: the text between two pieces of
/// markdown syntax (<c>**</c>, a link, a line break…), never across them. Code, link text, image
/// alt text and raw HTML are left alone. When several patterns match, the match that starts first
/// wins, and the pattern registered last wins a tie, so a viewer's own
/// <see cref="MarkdownViewer.Extensions"/> override <see cref="MarkdownViewerDefaults.Extensions"/>. Matches can't overlap: text matched once is
/// not searched again.
/// </para>
/// <para>
/// When the factory returns <see langword="null"/> or throws, the matched text is kept as it is. An
/// <see cref="InlineUIContainer"/> whose control has no
/// <see cref="MarkdownSelection.CopyTextProperty"/> copies the matched text.
/// </para>
/// </remarks>
public sealed class InlinePatternExtension : IMarkViewExtension
{
    /// <summary>
    /// Creates an inline pattern.
    /// </summary>
    /// <param name="pattern">The text to find. Empty matches are ignored.</param>
    /// <param name="factory">Creates the inline for each match.</param>
    public InlinePatternExtension(Regex pattern, InlinePatternFactory factory)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(factory);
        Pattern = pattern;
        Factory = factory;
    }

    /// <summary>The text to find.</summary>
    public Regex Pattern { get; }

    /// <summary>Creates the inline for each match.</summary>
    public InlinePatternFactory Factory { get; }

    public void Register(AvaloniaRenderer renderer) =>
        renderer.InlinePatterns.Add(new InlinePattern(Pattern, (_, match) => Factory(match)));
}
