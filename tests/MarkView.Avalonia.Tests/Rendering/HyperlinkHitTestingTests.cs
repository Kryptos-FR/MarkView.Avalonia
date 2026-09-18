// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// Regression tests for hyperlink hit-testing bugs reported against 12.2.1:
/// GitHub issues #83 (nested-span lookup) and #84 (caret-vs-glyph hit test, including
/// the IsInside gate that keeps a Grid-star-stretched list item's hand cursor from
/// covering the whole row instead of just the link text).
/// </summary>
public class HyperlinkHitTestingTests
{
    private static MarkdownSelectableTextBlock RenderAndLayout(MarkdownViewer viewer)
    {
        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();
        return viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().Single();
    }

    [AvaloniaFact]
    public void HitTestHyperlink_finds_link_nested_inside_bold()
    {
        // #83: a hyperlink nested inside Bold/Italic sits one level below the direct
        // inline collection, so a lookup that only checks the top level never finds it.
        var viewer = new MarkdownViewer { Markdown = "**[docs](https://example.com)**" };
        var textBlock = RenderAndLayout(viewer);

        var linkRect = textBlock.TextLayout!.HitTestTextRange(0, "docs".Length).First();
        var point = new Point(linkRect.X + linkRect.Width / 2, linkRect.Y + linkRect.Height / 2);

        var hyperlink = textBlock.HitTestHyperlink(point);
        Assert.Equal(new Uri("https://example.com"), hyperlink?.NavigateUri);
    }

    [AvaloniaFact]
    public void HitTestHyperlink_on_trailing_edge_resolves_to_that_link_not_the_next()
    {
        // #84: TextPosition (a caret stop = FirstCharacterIndex + TrailingLength) lands
        // one character past a link when the pointer is over the trailing half of its
        // last glyph, wrongly matching the adjacent link that starts right after it.
        var viewer = new MarkdownViewer { Markdown = "[a](https://example.com/a)[b](https://example.com/b)" };
        var textBlock = RenderAndLayout(viewer);

        var linkRect = textBlock.TextLayout!.HitTestTextRange(0, 1).First(); // bounds of "a"
        var point = new Point(linkRect.Right - 0.25, linkRect.Y + linkRect.Height / 2);

        var hyperlink = textBlock.HitTestHyperlink(point);
        Assert.Equal(new Uri("https://example.com/a"), hyperlink?.NavigateUri);
    }

    [AvaloniaFact]
    public void HitTestHyperlink_outside_line_bounds_returns_null()
    {
        // IsInside gating: a point past the end of a short line (but still within the
        // TextBlock's own layout box) must not resolve to a hyperlink.
        var viewer = new MarkdownViewer { Markdown = "[a](https://example.com/a)" };
        var textBlock = RenderAndLayout(viewer);

        var hyperlink = textBlock.HitTestHyperlink(new Point(399, 7));
        Assert.Null(hyperlink);
    }

    [AvaloniaFact]
    public void HitTestHyperlink_in_wide_list_item_column_ignores_area_past_link_text()
    {
        // The list item's content column is Grid-star-sized (ListRenderer: "Auto,*"), so the
        // TextBlock is arranged much wider than "Math" itself. The hand cursor must still
        // revert to default past the link's actual glyphs, not the whole stretched column.
        var viewer = new MarkdownViewer { Markdown = "- [Math](#math)\n- [Other](#other)" };
        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();
        var textBlock = viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().First();

        var linkRect = textBlock.TextLayout!.HitTestTextRange(0, "Math".Length).First();
        Assert.True(textBlock.Bounds.Width > linkRect.Width + 100, "test assumes the column is stretched well past the link text");

        var pastLinkPoint = new Point(linkRect.Right + 20, linkRect.Y + linkRect.Height / 2);
        var hyperlink = textBlock.HitTestHyperlink(pastLinkPoint);
        Assert.Null(hyperlink);
    }

    [AvaloniaFact]
    public void Cursor_reverts_to_default_after_moving_off_link_within_same_row()
    {
        // Real PointerMoved events routed through MarkdownViewer.OnContentPointerMoved →
        // UpdateHyperlinkCursor, not a direct HitTestHyperlink call — this is the actual
        // production path a mouse sweep exercises. Cursor is set directly on the hit
        // MarkdownSelectableTextBlock (the real hit-test-visible control Avalonia tracks as
        // hovered) — not on MarkdownViewer, and not on DocumentSelectionLayer, which is
        // IsHitTestVisible=false and therefore never the tracked element.
        var viewer = new MarkdownViewer { Markdown = "- [Math](#math)\n- [Other](#other)" };
        var window = new Window { Width = 400, Height = 200, Content = viewer };
        window.Show();
        var textBlock = viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().First();
        var contentGrid = (Grid)viewer.Content!;

        var linkRect = textBlock.TextLayout!.HitTestTextRange(0, "Math".Length).First();
        var onLinkBlock = new Point(linkRect.X + linkRect.Width / 2, linkRect.Y + linkRect.Height / 2);
        var pastLinkBlock = new Point(linkRect.Right + 20, linkRect.Y + linkRect.Height / 2);
        var onLinkWindow = textBlock.TranslatePoint(onLinkBlock, window) ?? default;
        var pastLinkWindow = textBlock.TranslatePoint(pastLinkBlock, window) ?? default;

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        ulong ts = 1;
        var props = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);

        contentGrid.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, contentGrid, pointer, window, onLinkWindow, ts++, props, KeyModifiers.None));
        Assert.Equal("Hand", textBlock.Cursor?.ToString());

        contentGrid.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, contentGrid, pointer, window, pastLinkWindow, ts++, props, KeyModifiers.None));
        Assert.Equal("Arrow", textBlock.Cursor?.ToString());
    }

    [AvaloniaFact]
    public void Cursor_reverts_when_link_is_alone_on_a_wide_paragraph()
    {
        // Link is the ONLY content of its own paragraph (nothing trailing it on that line),
        // second paragraph is plain text, window much wider than the link text itself.
        var viewer = new MarkdownViewer { Markdown = "[Math](https://example.com/math)\n\nOther text" };
        var window = new Window { Width = 800, Height = 200, Content = viewer };
        window.Show();
        var textBlocks = viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().ToList();
        var linkBlock = textBlocks[0];
        var otherBlock = textBlocks[1];
        var contentGrid = (Grid)viewer.Content!;

        var linkRect = linkBlock.TextLayout!.HitTestTextRange(0, "Math".Length).First();
        var onLinkBlockPt = new Point(linkRect.X + linkRect.Width / 2, linkRect.Y + linkRect.Height / 2);
        var farRightBlockPt = new Point(700, linkRect.Y + linkRect.Height / 2);
        var onOtherBlockPt = new Point(5, 5);

        var onLinkWindow = linkBlock.TranslatePoint(onLinkBlockPt, window) ?? default;
        var farRightWindow = linkBlock.TranslatePoint(farRightBlockPt, window) ?? default;
        var onOtherWindow = otherBlock.TranslatePoint(onOtherBlockPt, window) ?? default;

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        ulong ts = 1;
        var props = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);

        contentGrid.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, contentGrid, pointer, window, onLinkWindow, ts++, props, KeyModifiers.None));
        Assert.Equal("Hand", linkBlock.Cursor?.ToString());

        contentGrid.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, contentGrid, pointer, window, farRightWindow, ts++, props, KeyModifiers.None));
        Assert.Equal("Arrow", linkBlock.Cursor?.ToString());

        contentGrid.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, contentGrid, pointer, window, onOtherWindow, ts++, props, KeyModifiers.None));
        Assert.Equal("Arrow", otherBlock.Cursor?.ToString());
    }
}
