using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A <see cref="SelectableTextBlock"/> that intercepts pointer clicks on
/// <see cref="MarkdownHyperlink"/> spans and dispatches them as link-clicked events.
/// Text selection falls through to the base class for all non-link areas.
/// </summary>
/// <remarks>
/// V1 limitation: selection is scoped to a single block. Cross-block selection
/// requires a custom document container — see Markdown.Avalonia's CTextBlock for
/// a geometry-based reference implementation.
/// </remarks>
public class MarkdownSelectableTextBlock : SelectableTextBlock
{
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    internal AvaloniaRenderer? Renderer { get; set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var hyperlink = HitTestHyperlink(e.GetPosition(this));
        if (hyperlink?.NavigateUri != null)
        {
            Renderer?.OnLinkClicked(hyperlink.NavigateUri.ToString());
            e.Handled = true;
            return;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var hyperlink = HitTestHyperlink(e.GetPosition(this));
        Cursor = hyperlink != null ? HandCursor : Cursor.Default;
        base.OnPointerMoved(e);
    }

    private MarkdownHyperlink? HitTestHyperlink(Point point)
    {
        if (TextLayout == null || Inlines == null) return null;

        var hitResult = TextLayout.HitTestPoint(point);
        int charIndex = hitResult.TextPosition;

        return FindHyperlinkAtIndex(Inlines, charIndex);
    }

    private static MarkdownHyperlink? FindHyperlinkAtIndex(
        InlineCollection inlines, int targetIndex)
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

    private static int MeasureInlineLength(Inline inline) => inline switch
    {
        Run r => r.Text?.Length ?? 0,
        Span s => s.Inlines.Sum(MeasureInlineLength),
        LineBreak => 1,
        _ => 1,
    };
}
