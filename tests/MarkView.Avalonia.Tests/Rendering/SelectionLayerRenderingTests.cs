// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.VisualTree;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class SelectionLayerRenderingTests
{
    // "Alpha" = 0..5, '\n' at 5, "Beta" = 6..10, '\n' at 10, "Gamma" = 11..16
    private sealed record Fixture(Window Window, DocumentSelectionLayer Layer, MarkdownSelectableTextBlock[] Blocks) : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown = "Alpha\n\nBeta\n\nGamma", params IMarkViewExtension[] extensions)
    {
        var viewer = new MarkdownViewer();
        foreach (var extension in extensions)
            viewer.Extensions.Add(extension);
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        try
        {
            var grid = Assert.IsType<Grid>(viewer.Content);
            var layer = Assert.IsType<DocumentSelectionLayer>(grid.Children[1]);
            var blocks = viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().ToArray();
            return new Fixture(window, layer, blocks);
        }
        catch
        {
            window.Close();
            throw;
        }
    }

    private static Rect[] RecordHighlights(DocumentSelectionLayer layer)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            layer.Render(context);
        return group.Children.OfType<GeometryDrawing>().Select(d => d.Geometry!.Bounds).ToArray();
    }

    private static Point CaretPoint(Visual relativeTo, TextBlock block, int index)
    {
        var caret = block.TextLayout.HitTestTextPosition(index);
        var origin = block.TranslatePoint(new Point(0, 0), relativeTo)!.Value;
        return new Point(origin.X + block.Padding.Left + caret.X + 0.5, origin.Y + block.Padding.Top + caret.Y + caret.Height / 2);
    }

    private static Rect ExpectedHighlight(DocumentSelectionLayer layer, TextBlock block, int start, int length)
    {
        var origin = block.TranslatePoint(new Point(0, 0), layer)!.Value;
        var textOrigin = origin + new Vector(block.Padding.Left, block.Padding.Top);
        return block.TextLayout.HitTestTextRange(start, length).Single().Translate(textOrigin);
    }

    [AvaloniaFact]
    public void Render_highlights_only_the_selected_ranges()
    {
        using var f = Show();
        f.Layer.SetSelectionForTest(2, 8); // "pha" in Alpha, "Be" in Beta

        var rects = RecordHighlights(f.Layer);

        Assert.Equal(2, rects.Length);
        Assert.Equal(ExpectedHighlight(f.Layer, f.Blocks[0], 2, 3), rects[0]);
        Assert.Equal(ExpectedHighlight(f.Layer, f.Blocks[1], 0, 2), rects[1]);
        var gammaTop = f.Blocks[2].TranslatePoint(new Point(0, 0), f.Layer)!.Value.Y;
        Assert.All(rects, r => Assert.True(r.Bottom <= gammaTop));
    }

    [AvaloniaFact]
    public void Render_reversed_selection_matches_forward_selection()
    {
        using var f = Show();
        f.Layer.SetSelectionForTest(2, 8);
        var forward = RecordHighlights(f.Layer);

        f.Layer.SetSelectionForTest(8, 2);

        Assert.Equal(forward, RecordHighlights(f.Layer));
    }

    [AvaloniaFact]
    public void Render_highlights_a_range_inside_a_single_block()
    {
        using var f = Show();
        f.Layer.SetSelectionForTest(1, 3);

        var rects = RecordHighlights(f.Layer);

        Assert.Equal(ExpectedHighlight(f.Layer, f.Blocks[0], 1, 2), Assert.Single(rects));
    }

    [AvaloniaFact]
    public void Render_skips_blocks_that_end_before_the_selection()
    {
        using var f = Show();
        f.Layer.SetSelectionForTest(8, 13); // "ta" in Beta, "Ga" in Gamma

        var rects = RecordHighlights(f.Layer);

        Assert.Equal(2, rects.Length);
        Assert.Equal(ExpectedHighlight(f.Layer, f.Blocks[1], 2, 2), rects[0]);
        Assert.Equal(ExpectedHighlight(f.Layer, f.Blocks[2], 0, 2), rects[1]);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Render_offsets_highlights_by_the_block_padding(bool warmBoundsCache)
    {
        using var f = Show();
        f.Blocks[0].Padding = new Thickness(10, 6, 0, 0);
        f.Window.UpdateLayout();
        if (warmBoundsCache)
            _ = f.Layer.HitTestOffset(CaretPoint(f.Layer, f.Blocks[0], 1));
        f.Layer.SetSelectionForTest(1, 3);

        var rects = RecordHighlights(f.Layer);

        Assert.Equal(ExpectedHighlight(f.Layer, f.Blocks[0], 1, 2), Assert.Single(rects));
    }

    [AvaloniaFact]
    public void Render_skips_blocks_that_are_not_in_the_layer_visual_tree()
    {
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(new TextBlock { Text = "Detached" }, "Detached", "\n"));
        layer.SetSelectionForTest(0, 4);

        Assert.Empty(RecordHighlights(layer));
    }

    [AvaloniaFact]
    public void SelectAll_without_registered_entries_selects_nothing()
    {
        var layer = new DocumentSelectionLayer();

        layer.SelectAll();

        Assert.Equal(string.Empty, layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void HitTestOffset_resolves_the_line_under_the_point_in_a_padded_multi_line_block()
    {
        using var f = Show("Alpha\n\nBeta  \nOmega\n\nGamma");
        var multiLine = f.Blocks[1];
        multiLine.Padding = new Thickness(10, 30, 0, 0);
        f.Window.UpdateLayout();
        Assert.True(multiLine.TextLayout.TextLines.Count > 1);

        var offset = f.Layer.HitTestOffset(CaretPoint(f.Layer, multiLine, 1));

        Assert.Equal(6 + 1, offset);
    }

    [AvaloniaFact]
    public void Hit_testing_a_gap_between_cached_blocks_finds_nothing()
    {
        using var f = Show();
        var alphaBottom = f.Blocks[0].TranslatePoint(new Point(0, f.Blocks[0].Bounds.Height), f.Layer)!.Value.Y;
        var betaTop = f.Blocks[1].TranslatePoint(new Point(0, 0), f.Layer)!.Value.Y;
        Assert.True(betaTop > alphaBottom);
        var gapPoint = new Point(2, (alphaBottom + betaTop) / 2);
        _ = f.Layer.HitTestOffset(gapPoint); // caches every block's bounds

        Assert.Null(f.Layer.HitTestOffset(gapPoint));
        Assert.Null(f.Layer.HitTestEntry(gapPoint));
    }

    [AvaloniaTheory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 4, 4)]
    public void Render_draws_nothing_without_a_non_empty_selection(bool select, int start, int end)
    {
        using var f = Show();
        if (select) f.Layer.SetSelectionForTest(start, end);

        Assert.Empty(RecordHighlights(f.Layer));
    }

    [AvaloniaFact]
    public void HitTestOffset_maps_a_point_to_the_absolute_document_offset_including_padding()
    {
        using var f = Show();
        f.Blocks[1].Padding = new Thickness(10, 6, 0, 0);
        f.Window.UpdateLayout();

        var offset = f.Layer.HitTestOffset(CaretPoint(f.Layer, f.Blocks[1], 2));

        Assert.Equal(6 + 2, offset);
    }

    [AvaloniaFact]
    public void HitTestOffset_outside_all_blocks_returns_null()
    {
        using var f = Show();

        Assert.Null(f.Layer.HitTestOffset(new Point(5, 290)));
    }

    [AvaloniaFact]
    public void HitTestOffset_is_stable_once_bounds_are_cached()
    {
        using var f = Show();
        var gammaPoint = CaretPoint(f.Layer, f.Blocks[2], 1);

        var first = f.Layer.HitTestOffset(gammaPoint);
        var second = f.Layer.HitTestOffset(gammaPoint);

        Assert.Equal(12, first);
        Assert.Equal(first, second);
    }

    [AvaloniaFact]
    public void Layout_change_invalidates_cached_bounds()
    {
        using var f = Show();
        _ = f.Layer.HitTestOffset(CaretPoint(f.Layer, f.Blocks[1], 1)); // caches bounds

        f.Blocks[0].Margin = new Thickness(0, 60, 0, 0);
        f.Window.UpdateLayout();

        Assert.Equal(6 + 1, f.Layer.HitTestOffset(CaretPoint(f.Layer, f.Blocks[1], 1)));
    }

    [AvaloniaFact]
    public void Pointer_press_outside_text_clears_selection_and_ignores_following_drags()
    {
        using var f = Show();
        f.Layer.SelectAll();

        f.Layer.OnPointerPressed(new Point(5, 290));
        f.Layer.OnPointerMoved(CaretPoint(f.Layer, f.Blocks[1], 2));

        Assert.Equal(string.Empty, f.Layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Pointer_press_then_drag_selects_between_the_two_points()
    {
        using var f = Show();

        f.Layer.OnPointerPressed(CaretPoint(f.Layer, f.Blocks[0], 3));
        f.Layer.OnPointerMoved(CaretPoint(f.Layer, f.Blocks[1], 2));

        Assert.Equal("ha\nBe", f.Layer.GetSelectedText());
    }

    // "a " = 0..2, image at 2, ' ' at 3, "bcd" = 4..7
    private const string InlineImage = "a ![pic](x.png) bcd";

    [AvaloniaFact]
    public void Dragging_across_text_after_an_inline_image_selects_exactly_that_text()
    {
        using var f = Show(InlineImage);

        f.Layer.OnPointerPressed(CaretPoint(f.Layer, f.Blocks[0], 4));
        f.Layer.OnPointerMoved(CaretPoint(f.Layer, f.Blocks[0], 7));

        Assert.Equal("bcd", f.Layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Dragging_across_an_inline_image_copies_its_alt_text()
    {
        using var f = Show(InlineImage);

        f.Layer.OnPointerPressed(CaretPoint(f.Layer, f.Blocks[0], 1));
        f.Layer.OnPointerMoved(CaretPoint(f.Layer, f.Blocks[0], 5));

        Assert.Equal(" pic b", f.Layer.GetSelectedText());
    }

    // "Alpha" = 0..5, '\n' at 5, break = 6..7, '\n' at 7, "Gamma" = 8..13
    private const string CopyableBreak = "Alpha\n\n---\n\nGamma";

    private static Border BreakBorder(Fixture f) =>
        f.Window.GetVisualDescendants().OfType<Border>().Single(b => MarkdownSelection.GetCopyText(b) is not null);

    private static Point PointInBlock(Visual relativeTo, Control block, double heightFraction)
    {
        var origin = block.TranslatePoint(new Point(0, 0), relativeTo)!.Value;
        return new Point(origin.X + 5, origin.Y + block.Bounds.Height * heightFraction);
    }

    [AvaloniaFact]
    public void Dragging_past_the_middle_of_a_copyable_block_selects_it()
    {
        using var f = Show(CopyableBreak, new CopyableBreakExtension());

        f.Layer.OnPointerPressed(CaretPoint(f.Layer, f.Blocks[0], 2));
        f.Layer.OnPointerMoved(PointInBlock(f.Layer, BreakBorder(f), 0.75));

        Assert.Equal("pha\n<break>", f.Layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Dragging_into_the_top_half_of_a_copyable_block_stops_before_it()
    {
        using var f = Show(CopyableBreak, new CopyableBreakExtension());

        f.Layer.OnPointerPressed(CaretPoint(f.Layer, f.Blocks[0], 2));
        f.Layer.OnPointerMoved(PointInBlock(f.Layer, BreakBorder(f), 0.25));

        Assert.Equal("pha\n", f.Layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Render_highlights_the_whole_copyable_block()
    {
        using var f = Show(CopyableBreak, new CopyableBreakExtension());
        f.Layer.SetSelectionForTest(6, 7);

        var rect = Assert.Single(RecordHighlights(f.Layer));

        var border = BreakBorder(f);
        Assert.Equal(new Rect(border.TranslatePoint(new Point(0, 0), f.Layer)!.Value, border.Bounds.Size), rect);
    }

    // "•" = 0..1, ' ' at 1, "one" = 2..5, '\n' at 5, "•" = 6..7, ' ' at 7, "two" = 8..11
    private const string BulletList = """
        - one
        - two
        """;

    private static TextBlock[] Markers(Fixture f) =>
        f.Window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("markdown-list-marker")).ToArray();

    [AvaloniaFact]
    public void Dragging_across_list_item_text_selects_that_text_without_the_marker()
    {
        using var f = Show(BulletList);

        f.Layer.OnPointerPressed(CaretPoint(f.Layer, f.Blocks[0], 0));
        f.Layer.OnPointerMoved(CaretPoint(f.Layer, f.Blocks[0], 3));

        Assert.Equal("one", f.Layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Dragging_into_the_next_list_item_includes_its_marker()
    {
        using var f = Show(BulletList);

        f.Layer.OnPointerPressed(CaretPoint(f.Layer, f.Blocks[0], 1));
        f.Layer.OnPointerMoved(CaretPoint(f.Layer, f.Blocks[1], 2));

        Assert.Equal("ne\n• tw", f.Layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Render_highlights_list_item_text_at_its_own_position_not_shifted_by_the_marker()
    {
        using var f = Show(BulletList);
        f.Layer.SetSelectionForTest(3, 5); // "ne" in "• one"

        var rect = Assert.Single(RecordHighlights(f.Layer));

        Assert.Equal(ExpectedHighlight(f.Layer, f.Blocks[0], 1, 2), rect);
    }

    [AvaloniaFact]
    public void Render_highlights_the_list_marker_and_the_item_text_separately()
    {
        using var f = Show(BulletList);
        f.Layer.SetSelectionForTest(0, 5); // "• one"

        var rects = RecordHighlights(f.Layer);

        Assert.Equal(
            [ExpectedHighlight(f.Layer, Markers(f)[0], 0, 1), ExpectedHighlight(f.Layer, f.Blocks[0], 0, 3)],
            rects);
    }

    [AvaloniaFact]
    public void Dragging_from_a_list_marker_selects_the_marker()
    {
        using var f = Show(BulletList);

        f.Layer.OnPointerPressed(CaretPoint(f.Layer, Markers(f)[1], 0));
        f.Layer.OnPointerMoved(CaretPoint(f.Layer, f.Blocks[1], 3));

        Assert.Equal("• two", f.Layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Task_list_check_glyph_is_selectable_and_highlighted()
    {
        using var f = Show("- [x] done");
        var glyph = Assert.Single(Markers(f));

        f.Layer.OnPointerPressed(CaretPoint(f.Layer, glyph, 0));
        f.Layer.OnPointerMoved(CaretPoint(f.Layer, f.Blocks[0], 4));

        Assert.Equal("☑ done", f.Layer.GetSelectedText());
        Assert.Equal(
            [ExpectedHighlight(f.Layer, glyph, 0, 1), ExpectedHighlight(f.Layer, f.Blocks[0], 0, 4)],
            RecordHighlights(f.Layer));
    }

    [AvaloniaFact]
    public async Task CopyToClipboardAsync_copies_selection_and_skips_empty_selection()
    {
        using var f = Show();
        var clipboard = f.Window.Clipboard!;
        await clipboard.SetTextAsync("keep");

        await f.Layer.CopyToClipboardAsync(f.Window);
        Assert.Equal("keep", await clipboard.TryGetTextAsync());

        f.Layer.SelectAll();
        await f.Layer.CopyToClipboardAsync(f.Window);
        Assert.Equal("Alpha\nBeta\nGamma", await clipboard.TryGetTextAsync());
    }
}
