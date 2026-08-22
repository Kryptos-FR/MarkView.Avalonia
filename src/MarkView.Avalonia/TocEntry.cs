// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia;

/// <summary>
/// A single heading entry in <see cref="MarkdownViewer.TableOfContents"/>.
/// </summary>
public sealed record TocEntry
{
    /// <summary>The heading level (1-6).</summary>
    public required int Level { get; init; }

    /// <summary>The heading's plain text.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The anchor id also registered for in-document navigation. Pass to
    /// <see cref="MarkdownViewer.ScrollToAnchor"/> to scroll to this heading.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Nested headings at a deeper level, up to the next heading at the same or
    /// shallower level. Empty (never null) if this entry has no nested headings.
    /// </summary>
    public required IReadOnlyList<TocEntry> Children { get; init; }

    /// <summary>
    /// Builds a nested heading tree from a flat, document-ordered sequence of headings.
    /// Headings deeper than <paramref name="maxDepth"/> are dropped entirely, along with
    /// their descendants (never promoted to a shallower level). Each remaining heading
    /// nests under the nearest preceding heading with a strictly shallower level; if none
    /// exists — including when levels skip, e.g. an H3 with no preceding H1 or H2 — it
    /// becomes a root entry.
    /// </summary>
    public static IReadOnlyList<TocEntry> BuildTree(
        IReadOnlyList<(int Level, string Text, string Slug)> flatEntries, int maxDepth)
    {
        var roots = new List<TocEntry>();
        var stack = new List<(int Level, List<TocEntry> Children)> { (0, roots) };

        foreach (var (level, text, slug) in flatEntries)
        {
            if (level > maxDepth)
                continue;

            while (stack.Count > 1 && stack[^1].Level >= level)
                stack.RemoveAt(stack.Count - 1);

            var children = new List<TocEntry>();
            var entry = new TocEntry { Level = level, Text = text, Slug = slug, Children = children };
            stack[^1].Children.Add(entry);
            stack.Add((level, children));
        }

        return roots;
    }
}
