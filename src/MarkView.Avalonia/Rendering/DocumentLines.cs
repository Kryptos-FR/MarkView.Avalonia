// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Counts the lines of a laid-out document, for <see cref="MarkdownViewer.MaxLines"/>. A line is a
/// row of the page: each text line of a text block, and each control that stands for content
/// (one with <see cref="MarkdownSelection.CopyTextProperty"/>, such as an image, a formula or a
/// diagram) is a span from its top to its bottom, and spans that overlap vertically make one line:
/// a list marker and its item's first line, the cells of a table row, a code line and its number.
/// </summary>
internal static class DocumentLines
{
    // Layout rounding can make two adjacent lines overlap by a fraction of a pixel.
    private const double Tolerance = 0.5;

    /// <summary>
    /// Finds where the document under <paramref name="root"/> must be cut to show only its first
    /// <paramref name="maxLines"/> lines: below the last line shown, and below the bottom padding and
    /// border of the blocks around it that end before the next line (e.g. a table cell, or a code
    /// block whose last line it is), so that the cut does not go through them.
    /// </summary>
    /// <returns>
    /// Where to cut, in <paramref name="relativeTo"/>'s coordinates, or <c>null</c> when the document
    /// has no more than <paramref name="maxLines"/> lines.
    /// </returns>
    public static double? FindCut(Panel root, Visual relativeTo, int maxLines)
    {
        var count = 0;
        Line? previous = null;
        foreach (var line in GetLines(root, relativeTo))
        {
            if (++count > maxLines)
                return ExtendCut(previous!, line.Top, root, relativeTo);
            previous = line;
        }
        return null;
    }

    private static double ExtendCut(Line lastShown, double nextTop, Visual root, Visual relativeTo)
    {
        var cut = lastShown.Bottom;
        foreach (var source in lastShown.Sources)
        {
            // The blocks around a line are nested, so the first one that goes on past the next line
            // contains all the others.
            for (var visual = source; visual is not null && visual != root; visual = visual.GetVisualParent())
            {
                if (visual.TranslatePoint(new Point(0, visual.Bounds.Height), relativeTo) is not { } bottom
                    || bottom.Y > nextTop + Tolerance)
                    break;
                cut = Math.Max(cut, Math.Min(bottom.Y, nextTop));
            }
        }
        return cut;
    }

    /// <summary>
    /// The lines of the document under <paramref name="root"/>, from top to bottom, in
    /// <paramref name="relativeTo"/>'s coordinates. Lazy: the blocks below the lines enumerated are
    /// not looked at.
    /// </summary>
    internal static IEnumerable<Line> GetLines(Panel root, Visual relativeTo)
    {
        Line? line = null;
        // The top-level blocks are stacked, so a line is complete once a later block starts below it.
        foreach (var block in root.Children)
        {
            var spans = new List<Span>();
            Collect(block, relativeTo, (double.NegativeInfinity, double.PositiveInfinity), spans);
            spans.Sort((a, b) => a.Top.CompareTo(b.Top));
            foreach (var span in spans)
            {
                // Sorted by top, a span can only overlap the last line.
                if (line is not null && span.Top < line.Bottom - Tolerance)
                {
                    line.Bottom = Math.Max(line.Bottom, span.Bottom);
                    // Stryker disable once Statement : the spans of one line come from blocks that end together (the cells of a table row, a list item's marker and text), so its first span's blocks give the same cut
                    line.Sources.Add(span.Source);
                    continue;
                }
                if (line is not null)
                    yield return line;
                line = new Line(span.Top, span.Bottom) { Sources = { span.Source } };
            }
        }
        if (line is not null)
            yield return line;
    }

    /// <summary>A line of the document: from the top of its spans to their bottom, and where they come from.</summary>
    internal sealed class Line(double top, double bottom)
    {
        public double Top { get; } = top;

        public double Bottom { get; set; } = bottom;

        /// <summary>The text blocks and controls the line's spans come from.</summary>
        public List<Visual> Sources { get; } = [];
    }

    private readonly record struct Span(double Top, double Bottom, Visual Source);

    /// <summary>
    /// Adds the spans of the content under <paramref name="visual"/> that shows between the top and
    /// bottom of <paramref name="clip"/>, what the clipping controls around it leave visible.
    /// </summary>
    private static void Collect(Visual visual, Visual relativeTo, (double Top, double Bottom) clip, List<Span> spans)
    {
        if (!visual.IsVisible)
            return;

        switch (visual)
        {
            case TextBlock textBlock:
                AddTextLines(textBlock, relativeTo, clip, spans);
                return;
            case Control control when MarkdownSelection.GetCopyText(control) is not null:
                if (control.TranslatePoint(default, relativeTo) is { } origin)
                    AddSpan(origin.Y, origin.Y + control.Bounds.Height, control, clip, spans);
                return;
        }

        // E.g. the scroll viewer of a code block with a maximum height only shows some of its lines.
        if (visual.ClipToBounds && visual.TranslatePoint(default, relativeTo) is { } clipOrigin)
            clip = (Math.Max(clip.Top, clipOrigin.Y), Math.Min(clip.Bottom, clipOrigin.Y + visual.Bounds.Height));

        foreach (var child in visual.GetVisualChildren())
            Collect(child, relativeTo, clip, spans);
    }

    private static void AddTextLines(TextBlock textBlock, Visual relativeTo, (double Top, double Bottom) clip, List<Span> spans)
    {
        // An empty text block still lays out one line, which shows nothing.
        if (string.IsNullOrEmpty(textBlock.Text) && textBlock.Inlines is not { Count: > 0 })
            return;
        if (textBlock.TranslatePoint(new Point(0, textBlock.Padding.Top), relativeTo) is not { } origin)
            return;
        // The layout places each line right below the one before it.
        var top = origin.Y;
        foreach (var line in textBlock.TextLayout.TextLines)
        {
            AddSpan(top, top + line.Height, textBlock, clip, spans);
            top += line.Height;
        }
    }

    private static void AddSpan(double top, double bottom, Visual source, (double Top, double Bottom) clip, List<Span> spans)
    {
        top = Math.Max(top, clip.Top);
        bottom = Math.Min(bottom, clip.Bottom);
        if (bottom - top > Tolerance)
            spans.Add(new Span(top, bottom, source));
    }
}
