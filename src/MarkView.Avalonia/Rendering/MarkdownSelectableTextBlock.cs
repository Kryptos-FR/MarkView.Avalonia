// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;

using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A <see cref="TextBlock"/> that exposes hyperlink hit-testing and plain-text extraction
/// for use by <see cref="MarkdownViewer"/>'s pointer handling and selection layer registration.
/// Does not own selection — that is managed by <see cref="DocumentSelectionLayer"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class MarkdownSelectableTextBlock : TextBlock
{
    internal AvaloniaRenderer? Renderer { get; set; }

    /// <summary>
    /// Returns the <see cref="MarkdownHyperlink"/> at <paramref name="point"/> (in this control's
    /// local coordinates), or <c>null</c> if no hyperlink is present there.
    /// </summary>
    internal MarkdownHyperlink? HitTestHyperlink(Point point)
    {
        if (TextLayout == null || Inlines == null) return null;
        var adjusted = new Point(point.X - Padding.Left, point.Y - Padding.Top);
        var hitResult = TextLayout.HitTestPoint(adjusted);
        return FindHyperlinkAtIndex(Inlines, hitResult.TextPosition);
    }

    private static MarkdownHyperlink? FindHyperlinkAtIndex(InlineCollection inlines, int targetIndex)
    {
        int current = 0;
        foreach (var inline in inlines)
        {
            int length = MeasureInlineLength(inline);
            if (inline is MarkdownHyperlink h && current <= targetIndex && targetIndex < current + length)
                return h;
            current += length;
        }
        return null;
    }

    internal static int MeasureInlineLength(Inline inline)
    {
        switch (inline)
        {
            case Run r: return r.Text?.Length ?? 0;
            case Span s:
                int total = 0;
                foreach (var child in s.Inlines) total += MeasureInlineLength(child);
                return total;
            case LineBreak: return Environment.NewLine.Length;
            default: return 1;
        }
    }

    /// <summary>
    /// Extracts plain text from an <see cref="InlineCollection"/>, recursing into spans.
    /// Used at registration time to populate <see cref="DocumentBlock.PlainText"/>.
    /// Returns the existing <see cref="Run.Text"/> string directly when there is exactly
    /// one <see cref="Run"/> (the common case), avoiding a <see cref="StringBuilder"/> allocation.
    /// </summary>
    internal static string ExtractPlainText(InlineCollection inlines)
    {
        // Fast path: single Run → return the string reference directly, no allocation
        if (inlines.Count == 1 && inlines[0] is Run singleRun)
            return singleRun.Text ?? string.Empty;

        var sb = new StringBuilder();
        AppendInlines(sb, inlines);
        return sb.ToString();
    }

    private static void AppendInlines(StringBuilder sb, InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run r: sb.Append(r.Text); break;
                case LineBreak: sb.Append(Environment.NewLine); break;
                case Span s: AppendInlines(sb, s.Inlines); break;
            }
        }
    }
}

