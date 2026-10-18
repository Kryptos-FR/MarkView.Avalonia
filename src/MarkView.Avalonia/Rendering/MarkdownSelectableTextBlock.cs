// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text;

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;

using MarkView.Avalonia.Automation;
using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A <see cref="TextBlock"/> that exposes hyperlink hit-testing and plain-text extraction
/// for use by <see cref="MarkdownViewer"/>'s pointer handling and selection layer registration.
/// Does not own selection — that is managed by <see cref="DocumentSelectionLayer"/>.
/// It can also draw a border along its edges (<see cref="BorderBrush"/>, <see cref="BorderThickness"/>),
/// which a theme uses to rule off a heading.
/// </summary>
[ExcludeFromCodeCoverage]
public class MarkdownSelectableTextBlock : TextBlock
{
    /// <summary>
    /// U+FFFC, written in place of an embedded control so plain-text offsets match
    /// the single position <see cref="TextBlock.TextLayout"/> gives it.
    /// </summary>
    internal const char ObjectReplacementCharacter = '\uFFFC';

    /// <summary>
    /// Defines the <see cref="BorderBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        Border.BorderBrushProperty.AddOwner<MarkdownSelectableTextBlock>();

    /// <summary>
    /// Defines the <see cref="BorderThickness"/> property.
    /// </summary>
    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        Border.BorderThicknessProperty.AddOwner<MarkdownSelectableTextBlock>();

    static MarkdownSelectableTextBlock()
    {
        AffectsRender<MarkdownSelectableTextBlock>(BorderBrushProperty, BorderThicknessProperty);
    }

    /// <summary>
    /// Gets or sets the brush of the border drawn along the edges given by <see cref="BorderThickness"/>.
    /// No border is drawn by default; the GitHub theme sets one under level 1 and 2 headings.
    /// </summary>
    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the border on each edge. The border is drawn over the edges of the
    /// <see cref="TextBlock.Padding"/> area rather than outside it, so give the block at least as much
    /// padding on those edges, or the border overlaps its text.
    /// </summary>
    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    internal AvaloniaRenderer? Renderer { get; set; }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // Avalonia leaves inlines unstyled after a change to the application's styles
        // (https://github.com/AvaloniaUI/Avalonia/issues/22505).
        StyledInlinesTextBlock.ApplyInlineStyling(Inlines);
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        // Over the background, which is already drawn, and under the text.
        if (BorderBrush is { } brush)
        {
            foreach (var edge in GetBorderEdges())
                context.FillRectangle(brush, edge);
        }
        base.RenderTextLayout(context, origin);
    }

    /// <summary>
    /// The rectangles of the border's edges, in the control's coordinates: the top and bottom edges span
    /// the whole width, the left and right ones the height between them. Edges of zero width are left out.
    /// </summary>
    internal List<Rect> GetBorderEdges()
    {
        var size = Bounds.Size;
        var thickness = BorderThickness;
        var middle = Math.Max(0, size.Height - thickness.Top - thickness.Bottom);
        var edges = new List<Rect>(4);
        if (thickness.Top > 0)
            edges.Add(new Rect(0, 0, size.Width, thickness.Top));
        if (thickness.Bottom > 0)
            edges.Add(new Rect(0, size.Height - thickness.Bottom, size.Width, thickness.Bottom));
        if (thickness.Left > 0)
            edges.Add(new Rect(0, thickness.Top, thickness.Left, middle));
        if (thickness.Right > 0)
            edges.Add(new Rect(size.Width - thickness.Right, thickness.Top, thickness.Right, middle));
        return edges;
    }

    /// <summary>
    /// The focus targets standing in for this block's links, in reading order; set by
    /// <see cref="LinkFocusLayer"/>. <c>null</c> when the block has no link.
    /// </summary>
    internal List<LinkFocusTarget>? LinkTargets { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new MarkdownTextBlockAutomationPeer(this);

    /// <summary>
    /// Returns the <see cref="MarkdownHyperlink"/> at <paramref name="point"/> (in this control's
    /// local coordinates), or <c>null</c> if no hyperlink is present there.
    /// </summary>
    internal MarkdownHyperlink? HitTestHyperlink(Point point)
    {
        if (TextLayout == null || Inlines == null) return null;
        var adjusted = new Point(point.X - Padding.Left, point.Y - Padding.Top);
        var hitResult = TextLayout.HitTestPoint(adjusted);
        if (!hitResult.IsInside) return null;

        // TextPosition is a caret stop (FirstCharacterIndex + TrailingLength), which lands
        // one character past a link when the pointer is over the trailing half of its last
        // glyph. FirstCharacterIndex is the character cluster actually under the pointer.
        return FindHyperlinkAtIndex(Inlines, hitResult.CharacterHit.FirstCharacterIndex);
    }

    private static MarkdownHyperlink? FindHyperlinkAtIndex(InlineCollection inlines, int targetIndex)
    {
        int current = 0;
        foreach (var inline in inlines)
        {
            int length = MeasureInlineLength(inline);
            if (current <= targetIndex && targetIndex < current + length)
            {
                if (inline is MarkdownHyperlink h) return h;
                // Descend into other spans (Bold, Italic, ...) — a hyperlink nested inside
                // emphasis sits one level below the direct inline collection.
                if (inline is Span s) return FindHyperlinkAtIndex(s.Inlines, targetIndex - current);
                return null;
            }
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
    /// Used at registration time to populate <see cref="IndexEntry.PlainText"/>.
    /// Returns the existing <see cref="Run.Text"/> string directly when there is exactly
    /// one <see cref="Run"/> (the common case), avoiding a <see cref="StringBuilder"/> allocation.
    /// </summary>
    internal static string ExtractPlainText(InlineCollection inlines) => ExtractPlainText(inlines, out _);

    /// <summary>
    /// Extracts plain text like <see cref="ExtractPlainText(InlineCollection)"/>. Each embedded control
    /// (<see cref="InlineUIContainer"/>) is written as U+FFFC, the single position
    /// <see cref="TextBlock.TextLayout"/> gives it, and <paramref name="embeddedText"/> maps that
    /// position to the control's copy text (<c>null</c> when there is no embedded control).
    /// </summary>
    internal static string ExtractPlainText(InlineCollection inlines, out Dictionary<int, string>? embeddedText)
    {
        embeddedText = null;

        // Fast path: single Run → return the string reference directly, no allocation
        if (inlines.Count == 1 && inlines[0] is Run singleRun)
            return singleRun.Text ?? string.Empty;

        var sb = new StringBuilder();
        AppendInlines(sb, inlines, ref embeddedText);
        return sb.ToString();
    }

    private static void AppendInlines(StringBuilder sb, InlineCollection inlines, ref Dictionary<int, string>? embeddedText)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run r: sb.Append(r.Text); break;
                case LineBreak: sb.Append(Environment.NewLine); break;
                case Span s: AppendInlines(sb, s.Inlines, ref embeddedText); break;
                case InlineUIContainer container:
                    (embeddedText ??= [])[sb.Length] =
                        container.Child is { } child ? MarkdownSelection.GetCopyText(child) ?? string.Empty : string.Empty;
                    sb.Append(ObjectReplacementCharacter);
                    break;
            }
        }
    }
}

