// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// How <see cref="DocumentSelectionLayer"/> draws find matches: where, with which brush, the text drawn
/// again over them, and what it leaves out.
/// </summary>
public class FindHighlightRenderingTests
{
    private static readonly Color LightMatch = Color.Parse("#FFF176");
    private static readonly Color LightCurrentMatch = Color.Parse("#FFC584");
    private static readonly Color DarkMatch = Color.Parse("#453A00");
    private static readonly Color DarkCurrentMatch = Color.Parse("#652A00");

    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public DocumentSelectionLayer Layer => Assert.IsType<DocumentSelectionLayer>(Assert.IsType<Grid>(Viewer.Content).Children[1]);

        public MarkdownSelectableTextBlock Paragraph(string text) =>
            Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>()
                .Single(tb => MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!).StartsWith(text, StringComparison.Ordinal));

        public TextBlock Code =>
            CodeBlockToolbar.FindCode(Viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-code-block")))!;

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown, bool theme = false, double width = 400)
    {
        var viewer = new MarkdownViewer().UseAlertBlocks();
        if (theme)
            viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        viewer.Markdown = markdown;
        var window = new Window { Width = width, Height = 600, Content = viewer };
        window.Show();
        return new Fixture(window, viewer);
    }

    /// <summary>A highlight drawn by the layer: its rectangle and colour, and the clip it is drawn in.</summary>
    private sealed record Highlight(Rect Rect, Color Color, Rect? Clip, DrawingGroup? Text);

    /// <summary>
    /// Records what the layer draws and returns its highlights in drawing order. Each highlight is a
    /// rectangle followed by a group clipped to it, holding the text drawn again.
    /// </summary>
    private static Highlight[] Record(DocumentSelectionLayer layer)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            layer.Render(context);
        var highlights = new List<Highlight>();
        Collect(group, null, highlights);
        return [.. highlights];
    }

    private static void Collect(DrawingGroup group, Rect? clip, List<Highlight> highlights)
    {
        var children = group.Children;
        for (int i = 0; i < children.Count; i++)
        {
            switch (children[i])
            {
                case GeometryDrawing { Brush: ISolidColorBrush brush } drawing:
                    var text = i + 1 < children.Count && children[i + 1] is DrawingGroup next
                        && next.ClipGeometry?.Bounds == drawing.Geometry!.Bounds ? next : null;
                    highlights.Add(new Highlight(drawing.Geometry!.Bounds, brush.Color, clip, text));
                    if (text is not null) i++;
                    break;
                case DrawingGroup nested:
                    Collect(nested, nested.ClipGeometry?.Bounds ?? clip, highlights);
                    break;
            }
        }
    }

    private static Rect Expected(DocumentSelectionLayer layer, TextBlock block, int start, int length)
    {
        var origin = block.TranslatePoint(new Point(block.Padding.Left, block.Padding.Top), layer)!.Value;
        return block.TextLayout.HitTestTextRange(start, length).Single().Translate(origin);
    }

    /// <summary>The glyph runs drawn in <paramref name="group"/>, with the offset each was drawn at.</summary>
    private static List<(GlyphRun Run, Vector Offset)> GlyphRuns(DrawingGroup group, Vector offset = default)
    {
        var runs = new List<(GlyphRun, Vector)>();
        foreach (var child in group.Children)
        {
            switch (child)
            {
                case GlyphRunDrawing { GlyphRun: { } run }:
                    runs.Add((run, offset));
                    break;
                case DrawingGroup nested:
                    var translation = nested.Transform?.Value is { } m ? new Vector(m.M31, m.M32) : default;
                    runs.AddRange(GlyphRuns(nested, offset + translation));
                    break;
            }
        }
        return runs;
    }

    [AvaloniaFact]
    public void Every_match_is_highlighted_where_its_text_is_laid_out()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show("one two one\n\nthree one");
        f.Viewer.Find("one");

        var highlights = Record(f.Layer);

        var first = f.Paragraph("one two");
        var second = f.Paragraph("three");
        Assert.Equal(
            [Expected(f.Layer, first, 0, 3), Expected(f.Layer, first, 8, 3), Expected(f.Layer, second, 6, 3)],
            highlights.Select(h => h.Rect));
        Assert.All(highlights, h => Assert.Equal(LightMatch, h.Color));
        Assert.All(highlights, h => Assert.Null(h.Clip));
    }

    [AvaloniaFact]
    public void The_current_match_has_its_own_brush()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show("one one one");
        f.Viewer.Find("one");

        f.Viewer.FindNext();
        f.Viewer.FindNext();

        Assert.Equal([LightMatch, LightCurrentMatch, LightMatch], Record(f.Layer).Select(h => h.Color));
    }

    [AvaloniaFact]
    public void The_dark_theme_has_its_own_built_in_brushes()
    {
        using var theme = new ThemeScope(ThemeVariant.Dark);
        using var f = Show("one one");
        f.Viewer.Find("one");

        f.Viewer.FindNext();

        Assert.Equal([DarkCurrentMatch, DarkMatch], Record(f.Layer).Select(h => h.Color));
    }

    [AvaloniaFact]
    public void Brushes_come_from_the_resources_when_defined()
    {
        using var f = Show("one one");
        f.Viewer.Resources["MarkdownFindMatchBackground"] = Brushes.Red;
        f.Viewer.Resources["MarkdownFindCurrentMatchBackground"] = Brushes.Blue;
        f.Viewer.Find("one");

        f.Viewer.FindNext();

        Assert.Equal([Colors.Blue, Colors.Red], Record(f.Layer).Select(h => h.Color));
    }

    [AvaloniaFact]
    public void The_theme_brushes_follow_the_theme_variant()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show("one", theme: true);
        f.Viewer.Find("one");
        f.Viewer.FindNext();
        Assert.Equal(LightMatch, ((ISolidColorBrush)f.Layer.FindMatchBrush!).Color);
        Assert.Equal(LightCurrentMatch, ((ISolidColorBrush)f.Layer.CurrentFindMatchBrush!).Color);

        theme.Switch(ThemeVariant.Dark);

        Assert.Equal(DarkMatch, ((ISolidColorBrush)f.Layer.FindMatchBrush!).Color);
        Assert.Equal(DarkCurrentMatch, ((ISolidColorBrush)f.Layer.CurrentFindMatchBrush!).Color);
        Assert.Equal([DarkCurrentMatch], Record(f.Layer).Select(h => h.Color));
    }

    [AvaloniaFact]
    public void The_text_of_a_match_is_drawn_again_over_its_highlight()
    {
        using var f = Show("```\nfirst line\nsecond match\nthird line\n```");
        f.Viewer.Find("match");

        var highlight = Assert.Single(Record(f.Layer));

        // Only the line the match is on is drawn again, where the code block draws it.
        Assert.NotNull(highlight.Text);
        var (run, offset) = Assert.Single(GlyphRuns(highlight.Text));
        var lines = f.Code.TextLayout.TextLines;
        var codeOrigin = f.Code.TranslatePoint(new Point(f.Code.Padding.Left, f.Code.Padding.Top), f.Layer)!.Value;
        Assert.Equal(new Vector(codeOrigin.X, codeOrigin.Y + lines[0].Height), offset);
        Assert.Equal("second match".Length, run.Characters.Length);
    }

    [AvaloniaFact]
    public void Highlights_are_offset_by_the_block_padding()
    {
        using var f = Show("one two");
        var paragraph = f.Paragraph("one");
        paragraph.Padding = new Thickness(10, 6, 0, 0);
        f.Window.UpdateLayout();
        f.Viewer.Find("two");

        var highlight = Assert.Single(Record(f.Layer));

        Assert.Equal(Expected(f.Layer, paragraph, 4, 3), highlight.Rect);
        var (_, offset) = Assert.Single(GlyphRuns(highlight.Text!));
        Assert.Equal((Vector)paragraph.TranslatePoint(new Point(10, 6), f.Layer)!.Value, offset);
    }

    [AvaloniaFact]
    public void The_bounds_of_a_match_on_two_lines_cover_both()
    {
        const string text = "Some words before the wrapped match here";
        using var f = Show(text, width: 200);
        var paragraph = f.Paragraph("Some");
        paragraph.Padding = new Thickness(10, 6, 0, 0);
        f.Window.UpdateLayout();
        var firstLine = paragraph.TextLayout.TextLines[0].Length;
        f.Viewer.Find(text.Substring(firstLine - 3, 5));

        var bounds = f.Layer.FindMatches.Single().GetBounds();

        var rects = paragraph.TextLayout.HitTestTextRange(firstLine - 3, 5).ToArray();
        Assert.Equal(2, rects.Length);
        Assert.Equal(rects[0].Union(rects[1]).Translate(new Vector(10, 6)), bounds);
    }

    [AvaloniaFact]
    public void A_match_on_two_lines_is_highlighted_on_both()
    {
        const string text = "Some words before the wrapped match here";
        using var f = Show(text, width: 200);
        var paragraph = f.Paragraph("Some");
        var firstLine = paragraph.TextLayout.TextLines[0].Length;
        Assert.InRange(firstLine, 6, text.Length - 3);
        // The end of the first line and the start of the second
        f.Viewer.Find(text.Substring(firstLine - 3, 5));

        var highlights = Record(f.Layer);

        Assert.Equal(2, highlights.Length);
        Assert.True(highlights[1].Rect.Top >= highlights[0].Rect.Bottom - 0.001);
        Assert.All(highlights, h => Assert.Single(GlyphRuns(h.Text!)));
    }

    [AvaloniaFact]
    public void Highlights_follow_the_text_when_it_wraps_differently()
    {
        using var f = Show("Some words before the wrapped match here");
        var paragraph = f.Paragraph("Some");
        f.Viewer.Find("here");
        var wide = Assert.Single(Record(f.Layer)).Rect;

        f.Viewer.Width = 120;
        f.Window.UpdateLayout();

        var narrow = Assert.Single(Record(f.Layer)).Rect;
        Assert.Equal(Expected(f.Layer, paragraph, 36, 4), narrow);
        Assert.True(narrow.Top > wide.Top);
    }

    [AvaloniaFact]
    public void A_match_in_a_collapsed_alert_is_drawn_once_expanded()
    {
        using var f = Show("> [!NOTE]-\n> Hidden word\n\nShown word");
        var alert = CollapsibleAlert.Get(f.Viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-alert")))!;
        f.Viewer.Find("word");

        Assert.Equal([Expected(f.Layer, f.Paragraph("Shown"), 6, 4)], Record(f.Layer).Select(h => h.Rect));

        alert.IsExpanded = true;
        f.Window.UpdateLayout();

        Assert.Equal(
            [Expected(f.Layer, f.Paragraph("Hidden"), 7, 4), Expected(f.Layer, f.Paragraph("Shown"), 6, 4)],
            Record(f.Layer).Select(h => h.Rect));

        // Collapsed again, the paragraph keeps the layout it had, which is not drawn.
        alert.IsExpanded = false;
        f.Window.UpdateLayout();

        Assert.Equal([Expected(f.Layer, f.Paragraph("Shown"), 6, 4)], Record(f.Layer).Select(h => h.Rect));
    }

    [AvaloniaFact]
    public void A_match_in_a_code_block_with_a_maximum_height_is_clipped_to_its_viewport()
    {
        var code = string.Join("\n", Enumerable.Range(0, 20).Select(i => $"line {i}"));
        using var f = Show($"```\n{code}\n```");
        f.Viewer.CodeBlockMaxHeight = 60;
        f.Window.UpdateLayout();
        f.Viewer.Find("line 1");

        var highlights = Record(f.Layer);

        var scroll = f.Viewer.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Classes.Contains("markdown-code-scroll"));
        var viewport = new Rect(scroll.TranslatePoint(new Point(0, 0), f.Layer)!.Value, scroll.Bounds.Size);
        Assert.Equal(11, highlights.Length); // "line 1" and "line 10" to "line 19"
        Assert.All(highlights, h => Assert.Equal(viewport, h.Clip));
    }

    [AvaloniaFact]
    public void Matches_are_drawn_under_the_selection()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show("Alpha");
        f.Viewer.Find("pha");
        f.Viewer.SelectAll();

        var drawn = Record(f.Layer);

        Assert.Equal(2, drawn.Length);
        Assert.Equal(LightMatch, drawn[0].Color);
        Assert.Equal(Expected(f.Layer, f.Paragraph("Alpha"), 0, 5), drawn[1].Rect);
    }

    [AvaloniaFact]
    public void Nothing_is_drawn_once_the_search_is_cleared()
    {
        using var f = Show("one one");
        f.Viewer.Find("one");
        f.Viewer.FindNext();

        f.Viewer.ClearFind();

        Assert.Empty(Record(f.Layer));
    }

    [AvaloniaFact]
    public void Matches_of_a_detached_document_are_not_drawn()
    {
        var viewer = new MarkdownViewer { Markdown = "one" };
        var layer = Assert.IsType<DocumentSelectionLayer>(Assert.IsType<Grid>(viewer.Content).Children[1]);

        viewer.Find("one");

        Assert.Empty(Record(layer));
    }
}
