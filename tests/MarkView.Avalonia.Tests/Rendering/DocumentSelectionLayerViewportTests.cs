// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// <see cref="DocumentSelectionLayer"/> highlights only the top-level blocks in view and hit-tests only those
/// under the pointer, with the same results as when it went through every entry.
/// </summary>
public class DocumentSelectionLayerViewportTests
{
    private sealed record Shown(Window Window, MarkdownViewer Viewer, DocumentSelectionLayer Layer, ScrollViewer Scroll) : IDisposable
    {
        public void Dispose() => Window.Close();

        public Control Paragraph(int number) => Layer.Entries.Single(e => e.PlainText == $"Paragraph {number}").Element;

        public Rect BoundsInLayer(Control control) =>
            new(control.TranslatePoint(new Point(0, 0), Layer)!.Value, control.Bounds.Size);

        public void ScrollTo(double y)
        {
            Scroll.Offset = new Vector(0, y);
            Window.UpdateLayout();
        }
    }

    private static Shown Show(string? markdown = null, bool foldable = false)
    {
        var viewer = new MarkdownViewer
        {
            Template = ScrollingViewerTemplate.Create(),
            AreHeadingsFoldable = foldable,
            Markdown = markdown ?? string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"Paragraph {i}")),
        };
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        var grid = Assert.IsType<Grid>(viewer.Content);
        var layer = Assert.IsType<DocumentSelectionLayer>(grid.Children[1]);
        var scroll = viewer.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "PART_ScrollViewer");
        return new Shown(window, viewer, layer, scroll);
    }

    // The bounds of the rectangles drawn, ignoring the text drawn again over find matches.
    private static List<Rect> Highlights(DocumentSelectionLayer layer)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            layer.Render(context);
        return [.. group.Children.OfType<GeometryDrawing>().Select(d => d.Geometry!.Bounds)];
    }

    private static Rect Viewport(Shown s) => new(0, s.Scroll.Offset.Y, s.Scroll.Viewport.Width, s.Scroll.Viewport.Height);

    [AvaloniaFact]
    public void The_layer_knows_the_document_it_covers()
    {
        using var s = Show();

        Assert.Same(Assert.IsType<Grid>(s.Viewer.Content).Children[0], s.Layer.Document);
    }

    [AvaloniaFact]
    public void Select_all_highlights_only_the_paragraphs_in_view()
    {
        using var s = Show();
        s.Layer.SelectAll();

        var highlights = Highlights(s.Layer);

        var viewport = Viewport(s);
        Assert.InRange(highlights.Count, 1, 20);
        Assert.All(highlights, h => Assert.True(h.Bottom >= viewport.Top - 8 && h.Top <= viewport.Bottom + 8, $"{h} out of {viewport}"));
        Assert.Contains(highlights, h => h.Intersects(s.BoundsInLayer(s.Paragraph(1))));
    }

    [AvaloniaFact]
    public void Scrolling_brings_the_highlight_of_the_paragraphs_now_in_view()
    {
        using var s = Show();
        s.Layer.SelectAll();

        s.ScrollTo(s.Scroll.Extent.Height);
        var highlights = Highlights(s.Layer);

        Assert.Contains(highlights, h => h.Intersects(s.BoundsInLayer(s.Paragraph(60))));
        Assert.DoesNotContain(highlights, h => h.Intersects(s.BoundsInLayer(s.Paragraph(1))));
        var viewport = Viewport(s);
        Assert.All(highlights, h => Assert.True(h.Bottom >= viewport.Top - 8 && h.Top <= viewport.Bottom + 8, $"{h} out of {viewport}"));
    }

    [AvaloniaFact]
    public void Find_draws_only_the_matches_in_view()
    {
        using var s = Show();
        Assert.Equal(60, s.Viewer.Find("Paragraph"));

        s.ScrollTo(s.Scroll.Extent.Height / 2);
        var highlights = Highlights(s.Layer);

        var viewport = Viewport(s);
        Assert.InRange(highlights.Count, 1, 20);
        Assert.All(highlights, h => Assert.True(h.Bottom >= viewport.Top - 8 && h.Top <= viewport.Bottom + 8, $"{h} out of {viewport}"));
        var middle = Enumerable.Range(1, 60).Select(s.Paragraph).First(p => s.BoundsInLayer(p).Bottom >= viewport.Center.Y);
        Assert.Contains(highlights, h => h.Intersects(s.BoundsInLayer(middle)));
    }

    [AvaloniaFact]
    public void A_paragraph_far_down_is_hit_where_it_is_after_scrolling()
    {
        using var s = Show();
        s.ScrollTo(s.Scroll.Extent.Height);
        var paragraph = s.Paragraph(55);
        var bounds = s.BoundsInLayer(paragraph);
        var entry = s.Layer.Entries.Single(e => e.Element == paragraph);

        Assert.Equal(entry.AbsStart, s.Layer.HitTestOffset(new Point(bounds.Left + 1, bounds.Center.Y)));
        Assert.Same(entry, s.Layer.HitTestEntry(bounds.Center));
        // Between two paragraphs, nothing is hit.
        Assert.Null(s.Layer.HitTestEntry(new Point(bounds.Center.X, bounds.Bottom + 4)));
    }

    [AvaloniaFact]
    public void Text_out_of_view_is_still_hit_while_dragging_past_the_edge()
    {
        using var s = Show();
        var paragraph = s.Paragraph(40);
        var bounds = s.BoundsInLayer(paragraph);
        Assert.True(bounds.Top > Viewport(s).Bottom);

        var entry = s.Layer.Entries.Single(e => e.Element == paragraph);
        Assert.Equal(entry.AbsStart, s.Layer.HitTestOffset(new Point(bounds.Left + 1, bounds.Center.Y)));
    }

    [AvaloniaFact]
    public void Blocks_folded_away_are_skipped_and_those_after_them_are_still_hit()
    {
        using var s = Show("# One\n\n" + string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"Paragraph {i}"))
            + "\n\n# Two\n\nParagraph 11", foldable: true);
        var paragraph = s.Paragraph(11);
        var before = s.BoundsInLayer(paragraph);

        var heading = s.Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>()
            .First(tb => MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!).TrimStart('\uFFFC') == "One");
        HeadingFold.Get(heading)!.Toggle();
        s.Window.UpdateLayout();
        var after = s.BoundsInLayer(paragraph);

        Assert.True(after.Top < before.Top);
        var entry = s.Layer.Entries.Single(e => e.Element == paragraph);
        Assert.Equal(entry.AbsStart, s.Layer.HitTestOffset(new Point(after.Left + 1, after.Center.Y)));
        Assert.False(s.Paragraph(1).IsEffectivelyVisible);
        s.Layer.SelectAll();
        Assert.Contains(Highlights(s.Layer), h => h.Intersects(after));
    }

    [AvaloniaTheory]
    [InlineData(40, 0)]
    [InlineData(0, 40)]
    public void Blocks_are_found_where_they_are_when_the_document_is_offset_from_the_layer(double documentTop, double layerTop)
    {
        using var s = Show();
        s.Layer.Document!.Margin = new Thickness(0, documentTop, 0, 0);
        s.Layer.Margin = new Thickness(0, layerTop, 0, 0);
        s.Window.UpdateLayout();
        var paragraph = s.Paragraph(2);
        var bounds = s.BoundsInLayer(paragraph);
        var entry = s.Layer.Entries.Single(e => e.Element == paragraph);

        Assert.Equal(entry.AbsStart, s.Layer.HitTestOffset(new Point(bounds.Left + 1, bounds.Center.Y)));
        Assert.Same(entry, s.Layer.HitTestEntry(bounds.Center));
    }

    [AvaloniaFact]
    public void Find_draws_nothing_when_no_entry_is_in_view()
    {
        using var s = Show(string.Join("\n\n", Enumerable.Range(1, 3).Select(i => $"Paragraph {i}")));
        s.Layer.Document!.Children.Add(new Border { Height = 2000 });
        s.Window.UpdateLayout();
        Assert.Equal(3, s.Viewer.Find("Paragraph"));

        s.ScrollTo(s.Scroll.Extent.Height);

        Assert.Empty(Highlights(s.Layer));
    }

    [AvaloniaFact]
    public void Cached_bounds_are_stale_after_a_layout_pass()
    {
        using var s = Show();
        var entry = s.Layer.Entries[0];
        Assert.NotNull(s.Layer.HitTestEntry(s.BoundsInLayer(s.Paragraph(1)).Center));
        Assert.NotNull(entry.CachedBounds);

        s.Paragraph(1).Margin = new Thickness(0, 10, 0, 0);
        s.Window.UpdateLayout();

        Assert.Null(entry.CachedBounds);
        Assert.Null(entry.CachedClip);
    }
}
