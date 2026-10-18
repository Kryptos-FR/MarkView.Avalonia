// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// Finding text in a <see cref="MarkdownViewer"/>: what is matched, moving between matches,
/// scrolling to them, folded content, re-rendering and selection.
/// </summary>
public class MarkdownViewerFindTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public ScrollViewer ScrollViewer => Viewer.GetVisualDescendants().OfType<ScrollViewer>().First();

        public DocumentSelectionLayer FindLayer => Layer(Viewer);

        /// <summary>The bounds of the current match relative to the document's scroll viewer.</summary>
        public Rect CurrentMatchInViewport()
        {
            var match = FindLayer.FindMatches[Viewer.CurrentFindMatchIndex];
            var transform = match.Entry.TextBlock!.TransformToVisual(ScrollViewer)!.Value;
            return match.GetBounds()!.Value.TransformToAABB(transform);
        }

        public void Dispose() => Window.Close();
    }

    // Mutation testing compiles the library without its AXAML, so the viewer gets a minimal template with
    // the PART_ScrollViewer instead of the theme's.
    private static Fixture Show(string markdown, double width = 400)
    {
        var viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create() }.UseAlertBlocks();
        viewer.Markdown = markdown;
        var window = new Window { Width = width, Height = 300, Content = viewer };
        window.Show();
        return new Fixture(window, viewer);
    }

    private static MarkdownViewer Viewer(string markdown) => new() { Markdown = markdown };

    private static DocumentSelectionLayer Layer(MarkdownViewer viewer) =>
        Assert.IsType<DocumentSelectionLayer>(Assert.IsType<Grid>(viewer.Content).Children[1]);

    private static (string Text, int Start)[] Matches(MarkdownViewer viewer) =>
        Layer(viewer).FindMatches.Select(m => (m.Entry.PlainText, m.Start)).ToArray();

    private static string Paragraphs(string prefix, int count) =>
        string.Join("\n\n", Enumerable.Range(0, count).Select(i => $"{prefix} {i}"));

    // ── What is matched ───────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Find_ignores_case_by_default()
    {
        var viewer = Viewer("Apple apple APPLE");

        Assert.Equal(3, viewer.Find("aPPle"));
        Assert.Equal(3, viewer.FindMatchCount);
        Assert.Equal([("Apple apple APPLE", 0), ("Apple apple APPLE", 6), ("Apple apple APPLE", 12)], Matches(viewer));
    }

    [AvaloniaFact]
    public void Find_with_MatchCase_only_matches_the_same_case()
    {
        var viewer = Viewer("Apple apple APPLE");

        Assert.Equal(1, viewer.Find("apple", MarkdownFindOptions.MatchCase));
        Assert.Equal([("Apple apple APPLE", 6)], Matches(viewer));
    }

    [AvaloniaFact]
    public void Find_with_WholeWord_skips_matches_inside_words()
    {
        const string text = "cat concat cats cat's _cat cat1 2cat cat";
        var viewer = Viewer(text);

        Assert.Equal(3, viewer.Find("CAT", MarkdownFindOptions.WholeWord));
        Assert.Equal([(text, 0), (text, 16), (text, 37)], Matches(viewer));
    }

    [AvaloniaFact]
    public void Find_with_both_options_matches_whole_words_of_the_same_case()
    {
        var viewer = Viewer("Cat cat cats");

        Assert.Equal(1, viewer.Find("cat", MarkdownFindOptions.MatchCase | MarkdownFindOptions.WholeWord));
        Assert.Equal([("Cat cat cats", 4)], Matches(viewer));
    }

    [AvaloniaFact]
    public void Find_with_WholeWord_finds_a_match_overlapping_a_rejected_one()
    {
        var viewer = Viewer("xa-a-a");

        Assert.Equal(1, viewer.Find("a-a", MarkdownFindOptions.WholeWord));
        Assert.Equal([("xa-a-a", 3)], Matches(viewer));
    }

    [AvaloniaTheory]
    [InlineData("\U0001D400cat", 0)]     // a letter outside the BMP before the word
    [InlineData("cat\U0001D400", 0)]     // and after it
    [InlineData("\U0001F600cat\U0001F600", 1)] // emoji are not letters
    public void Find_with_WholeWord_reads_characters_outside_the_BMP_whole(string text, int expected)
    {
        var viewer = Viewer(text);

        Assert.Equal(expected, viewer.Find("cat", MarkdownFindOptions.WholeWord));
    }

    [AvaloniaFact]
    public void Matches_do_not_overlap()
    {
        var viewer = Viewer("aaaaa");

        Assert.Equal(2, viewer.Find("aa"));
        Assert.Equal([("aaaaa", 0), ("aaaaa", 2)], Matches(viewer));
    }

    [AvaloniaFact]
    public void Find_reports_matches_in_reading_order_across_blocks()
    {
        var viewer = Viewer("""
            # One title

            one two one

            ```
            code one
            ```

            | Cell | one |
            |------|-----|
            | one  | x   |

            - item one
            """);

        Assert.Equal(7, viewer.Find("one"));
        Assert.Equal(
            [("One title", 0), ("one two one", 0), ("one two one", 8), ("code one", 5), ("one", 0), ("one", 0), ("item one", 5)],
            Matches(viewer));
    }

    [AvaloniaFact]
    public void A_match_never_spans_two_blocks()
    {
        var viewer = Viewer("Alpha\n\nBeta\n\n| a | b |\n|---|---|\n| c | d |");

        Assert.Equal(0, viewer.Find("a\nB"));
        Assert.Equal(0, viewer.Find("a\tb"));
        Assert.Equal(0, viewer.Find("a b"));
    }

    [AvaloniaFact]
    public void Find_without_a_match_finds_nothing()
    {
        var viewer = Viewer("Alpha\n\nBeta");

        Assert.Equal(0, viewer.Find("Gamma"));
        Assert.Equal(-1, viewer.CurrentFindMatchIndex);
        Assert.False(viewer.FindNext());
        Assert.False(viewer.FindPrevious());
        Assert.Equal(-1, viewer.CurrentFindMatchIndex);
    }

    [AvaloniaFact]
    public void List_markers_are_not_searched()
    {
        var viewer = Viewer("- bullet\n\n1. first");

        Assert.Equal(0, viewer.Find("•"));
        Assert.Equal(0, viewer.Find("1."));
        Assert.Equal(1, viewer.Find("first"));
    }

    [AvaloniaFact]
    public void The_copy_text_of_controls_is_not_searched()
    {
        var viewer = new MarkdownViewer();
        viewer.Extensions.Add(new CopyableBreakExtension());
        viewer.Markdown = "An ![inline image](x.png) here\n\n---\n\nAfter";

        Assert.Equal(0, viewer.Find("inline image"));
        Assert.Equal(0, viewer.Find(CopyableBreakExtension.CopyText));
        // The placeholder each control is registered as is not searched in a block control's entry,
        // only where it stands for an inline control in a text block.
        Assert.Equal(1, viewer.Find("￼"));
        Assert.Equal([("An ￼ here", 3)], Matches(viewer));
    }

    [AvaloniaFact]
    public void Text_in_a_collapsed_alert_is_counted()
    {
        var viewer = new MarkdownViewer().UseAlertBlocks();
        viewer.Markdown = "Before word\n\n> [!NOTE]-\n> Hidden word\n\nAfter";

        Assert.Equal(2, viewer.Find("word"));
    }

    [AvaloniaFact]
    public void Find_with_empty_text_clears_the_search()
    {
        var viewer = Viewer("Alpha alpha");
        viewer.Find("alpha");
        viewer.FindNext();

        Assert.Equal(0, viewer.Find(string.Empty));

        Assert.Equal(0, viewer.FindMatchCount);
        Assert.Equal(-1, viewer.CurrentFindMatchIndex);
        Assert.Empty(Layer(viewer).FindMatches);
        viewer.Markdown = "Alpha again";
        Assert.Equal(0, viewer.FindMatchCount);
    }

    [AvaloniaFact]
    public void Find_replaces_the_previous_matches()
    {
        var viewer = Viewer("x y");
        viewer.Find("x");

        Assert.Equal(1, viewer.Find("y"));
        Assert.Equal([("x y", 2)], Matches(viewer));
    }

    [AvaloniaFact]
    public void The_layer_leaves_no_current_match_after_a_new_search()
    {
        var layer = Layer(Viewer("x x"));
        layer.Find("x", MarkdownFindOptions.None);
        layer.CurrentFindMatch = 1;

        layer.Find("x", MarkdownFindOptions.None);

        Assert.Equal(-1, layer.CurrentFindMatch);
    }

    [AvaloniaFact]
    public void Find_rejects_null()
    {
        var viewer = Viewer("Alpha");

        Assert.Throws<ArgumentNullException>(() => viewer.Find(null!));
    }

    [AvaloniaFact]
    public void Find_on_an_empty_viewer_finds_nothing()
    {
        var viewer = new MarkdownViewer();

        Assert.Equal(0, viewer.Find("Alpha"));
        Assert.False(viewer.FindNext());
    }

    // ── Moving between matches ────────────────────────────────────────────────

    [AvaloniaFact]
    public void FindNext_starts_at_the_first_match_and_wraps_around()
    {
        var viewer = Viewer("x x\n\nx");
        viewer.Find("x");
        Assert.Equal(-1, viewer.CurrentFindMatchIndex);
        Assert.Equal(-1, Layer(viewer).CurrentFindMatch);

        var visited = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            Assert.True(viewer.FindNext());
            visited.Add(viewer.CurrentFindMatchIndex);
            Assert.Equal(viewer.CurrentFindMatchIndex, Layer(viewer).CurrentFindMatch);
        }

        Assert.Equal([0, 1, 2, 0], visited);
    }

    [AvaloniaFact]
    public void FindPrevious_starts_at_the_last_match_and_wraps_around()
    {
        var viewer = Viewer("x x\n\nx");
        viewer.Find("x");

        var visited = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            Assert.True(viewer.FindPrevious());
            visited.Add(viewer.CurrentFindMatchIndex);
        }

        Assert.Equal([2, 1, 0, 2], visited);
    }

    [AvaloniaFact]
    public void FindNext_and_FindPrevious_step_from_the_current_match()
    {
        var viewer = Viewer("x x x x");
        viewer.Find("x");
        viewer.FindNext();
        viewer.FindNext();

        viewer.FindPrevious();
        Assert.Equal(0, viewer.CurrentFindMatchIndex);
        viewer.FindNext();
        viewer.FindNext();
        Assert.Equal(2, viewer.CurrentFindMatchIndex);
    }

    [AvaloniaFact]
    public void Find_again_leaves_no_current_match()
    {
        var viewer = Viewer("x x x");
        viewer.Find("x");
        viewer.FindNext();

        viewer.Find("x");

        Assert.Equal(-1, viewer.CurrentFindMatchIndex);
        Assert.Equal(-1, Layer(viewer).CurrentFindMatch);
    }

    [AvaloniaFact]
    public void Match_count_and_current_index_raise_property_changes()
    {
        var viewer = Viewer("x x");
        var counts = new List<int>();
        var indexes = new List<int>();
        viewer.PropertyChanged += (_, e) =>
        {
            if (e.Property == MarkdownViewer.FindMatchCountProperty) counts.Add((int)e.NewValue!);
            else if (e.Property == MarkdownViewer.CurrentFindMatchIndexProperty) indexes.Add((int)e.NewValue!);
        };

        viewer.Find("x");
        viewer.FindNext();
        viewer.FindNext();
        viewer.ClearFind();

        Assert.Equal([2, 0], counts);
        Assert.Equal([0, 1, -1], indexes);
    }

    [AvaloniaFact]
    public void ClearFind_removes_every_match()
    {
        var viewer = Viewer("x x");
        viewer.Find("x");
        viewer.FindNext();

        viewer.ClearFind();

        Assert.Equal(0, viewer.FindMatchCount);
        Assert.Equal(-1, viewer.CurrentFindMatchIndex);
        Assert.Empty(Layer(viewer).FindMatches);
        Assert.Equal(-1, Layer(viewer).CurrentFindMatch);
        Assert.False(viewer.FindNext());
    }

    // ── Scrolling ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void FindNext_scrolls_a_match_below_the_viewport_into_view_with_a_margin()
    {
        using var f = Show($"{Paragraphs("Paragraph", 40)}\n\nTarget\n\n{Paragraphs("Trailing", 10)}");
        f.Viewer.Find("Target");

        f.Viewer.FindNext();
        f.Window.UpdateLayout();

        Assert.True(f.ScrollViewer.Offset.Y > 0);
        Assert.Equal(f.ScrollViewer.Viewport.Height - 16, f.CurrentMatchInViewport().Bottom, tolerance: 1);
    }

    [AvaloniaFact]
    public void FindNext_scrolls_a_match_above_the_viewport_into_view_with_a_margin()
    {
        using var f = Show($"{Paragraphs("Leading", 10)}\n\nTarget\n\n{Paragraphs("Paragraph", 40)}\n\nTarget");
        f.Viewer.Find("Target");
        f.Viewer.FindPrevious();
        f.Window.UpdateLayout();
        var scrolledDown = f.ScrollViewer.Offset.Y;

        f.Viewer.FindNext();
        f.Window.UpdateLayout();

        Assert.Equal(0, f.Viewer.CurrentFindMatchIndex);
        Assert.InRange(f.ScrollViewer.Offset.Y, 1, scrolledDown - 1);
        Assert.Equal(16, f.CurrentMatchInViewport().Top, tolerance: 1);
    }

    [AvaloniaFact]
    public void FindNext_does_not_scroll_to_a_visible_match()
    {
        using var f = Show($"{Paragraphs("Paragraph", 40)}");
        f.ScrollViewer.Offset = new Vector(0, 200);
        f.Window.UpdateLayout();
        f.Viewer.Find("Paragraph 12");

        f.Viewer.FindNext();
        f.Window.UpdateLayout();

        Assert.Equal(200, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public void FindNext_scrolls_a_code_block_with_a_maximum_height_and_then_the_document()
    {
        var code = string.Join("\n", Enumerable.Range(0, 30).Select(i => $"line {i}"));
        using var f = Show($"{Paragraphs("Paragraph", 40)}\n\n```\n{code}\nTarget\n```\n\n{Paragraphs("After", 40)}");
        f.Viewer.CodeBlockMaxHeight = 100;
        f.Window.UpdateLayout();
        f.Viewer.Find("Target");

        f.Viewer.FindNext();
        f.Window.UpdateLayout();

        var codeScroll = f.Viewer.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Classes.Contains("markdown-code-scroll"));
        var match = f.FindLayer.FindMatches[0];
        var inCode = match.GetBounds()!.Value.TransformToAABB(match.Entry.TextBlock!.TransformToVisual(codeScroll)!.Value);
        Assert.True(codeScroll.Offset.Y > 0);
        Assert.InRange(inCode.Top, 0, codeScroll.Viewport.Height);
        Assert.InRange(inCode.Bottom, 0, codeScroll.Viewport.Height);
        var inDocument = f.CurrentMatchInViewport();
        Assert.InRange(inDocument.Top, 0, f.ScrollViewer.Viewport.Height);
        Assert.InRange(inDocument.Bottom, 0, f.ScrollViewer.Viewport.Height);
    }

    [AvaloniaFact]
    public void FindNext_scrolls_the_document_and_then_a_scroll_viewer_around_the_viewer()
    {
        var viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create(), Height = 200 };
        viewer.Markdown = $"{Paragraphs("Paragraph", 40)}\n\nTarget\n\n{Paragraphs("Trailing", 10)}";
        var page = new ScrollViewer { Content = new StackPanel { Children = { new Border { Height = 400 }, viewer } } };
        var window = new Window { Width = 400, Height = 300, Content = page };
        window.Show();
        try
        {
            viewer.Find("Target");

            viewer.FindNext();
            window.UpdateLayout();

            var match = Layer(viewer).FindMatches[0];
            var inWindow = match.GetBounds()!.Value.TransformToAABB(match.Entry.TextBlock!.TransformToVisual(page)!.Value);
            Assert.True(page.Offset.Y > 0);
            Assert.InRange(inWindow.Top, 0, page.Viewport.Height);
            Assert.InRange(inWindow.Bottom, 0, page.Viewport.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Moving_to_a_match_without_a_template_does_not_throw()
    {
        var viewer = Viewer("Alpha");
        viewer.Find("Alpha");

        Assert.True(viewer.FindNext());
    }

    // ── Folded content ────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Moving_to_a_match_in_a_collapsed_alert_expands_it_and_scrolls_to_it()
    {
        using var f = Show($"""
            {Paragraphs("Paragraph", 30)}

            > [!NOTE]-
            > Intro
            >
            > Hidden target

            {Paragraphs("Trailing", 30)}
            """);
        var alert = CollapsibleAlert.Get(f.Viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-alert")))!;
        Assert.Equal(1, f.Viewer.Find("target"));
        Assert.False(alert.IsExpanded);

        f.Viewer.FindNext();
        f.Window.UpdateLayout();

        Assert.True(alert.IsExpanded);
        Assert.Equal(f.ScrollViewer.Viewport.Height - 16, f.CurrentMatchInViewport().Bottom, tolerance: 1);
    }

    [AvaloniaFact]
    public void Moving_to_a_match_in_collapsed_details_expands_them()
    {
        var viewer = Viewer("Before\n\n<details>\n<summary>More</summary>\n\nHidden target\n\n</details>\n\nAfter target");
        var details = CollapsibleAlert.Get(viewer.GetLogicalDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-details")))!;
        viewer.Find("target");

        viewer.FindPrevious();
        Assert.False(details.IsExpanded);
        viewer.FindPrevious();

        Assert.True(details.IsExpanded);
    }

    // ── Re-rendering ──────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void A_new_document_is_searched_again_keeping_the_current_index()
    {
        var viewer = Viewer("x x");
        viewer.Find("x");
        viewer.FindNext();
        viewer.FindNext();

        viewer.Markdown = "x x x";

        Assert.Equal(3, viewer.FindMatchCount);
        Assert.Equal(1, viewer.CurrentFindMatchIndex);
        Assert.Equal(3, Layer(viewer).FindMatches.Count);
        Assert.Equal(1, Layer(viewer).CurrentFindMatch);
    }

    [AvaloniaFact]
    public void A_new_document_with_fewer_matches_makes_the_last_one_current()
    {
        var viewer = Viewer("x x x");
        viewer.Find("x");
        viewer.FindPrevious();

        viewer.Markdown = "x x";

        Assert.Equal(1, viewer.CurrentFindMatchIndex);

        viewer.Markdown = "none";

        Assert.Equal(0, viewer.FindMatchCount);
        Assert.Equal(-1, viewer.CurrentFindMatchIndex);
    }

    [AvaloniaFact]
    public void A_new_document_keeps_no_current_match_when_there_was_none()
    {
        var viewer = Viewer("x x");
        viewer.Find("x");

        viewer.Markdown = "x x x";

        Assert.Equal(3, viewer.FindMatchCount);
        Assert.Equal(-1, viewer.CurrentFindMatchIndex);
    }

    [AvaloniaFact]
    public void The_search_and_its_options_outlive_an_empty_document()
    {
        var viewer = Viewer("x X");
        viewer.Find("x", MarkdownFindOptions.MatchCase);

        viewer.Markdown = null;
        Assert.Equal(0, viewer.FindMatchCount);
        Assert.Null(viewer.Content);

        viewer.Markdown = "x X x";
        Assert.Equal(2, viewer.FindMatchCount);
    }

    [AvaloniaFact]
    public void A_cleared_search_is_not_run_on_a_new_document()
    {
        var viewer = Viewer("x");
        viewer.Find("x");
        viewer.ClearFind();

        viewer.Markdown = "x x";

        Assert.Equal(0, viewer.FindMatchCount);
        Assert.Empty(Layer(viewer).FindMatches);
    }

    [AvaloniaFact]
    public void A_new_document_does_not_scroll_to_the_current_match()
    {
        using var f = Show($"{Paragraphs("Paragraph", 40)}\n\nTarget");
        f.Viewer.Find("Target");
        f.Viewer.FindNext();
        f.Window.UpdateLayout();

        f.Viewer.Markdown += "\n\nMore";
        f.Window.UpdateLayout();

        Assert.Equal(0, f.Viewer.CurrentFindMatchIndex);
        Assert.Equal(0, f.ScrollViewer.Offset.Y);
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Finding_leaves_the_selection_as_it_is()
    {
        var viewer = Viewer("Alpha\n\nBeta");
        viewer.SelectAll();

        viewer.Find("Beta");
        viewer.FindNext();
        Assert.Equal("Alpha\nBeta", viewer.GetSelectedText());

        viewer.ClearFind();
        Assert.Equal("Alpha\nBeta", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Clearing_the_selection_leaves_the_matches()
    {
        var viewer = Viewer("Alpha\n\nBeta");
        viewer.Find("a");
        viewer.FindNext();
        viewer.SelectAll();

        viewer.ClearSelection();

        Assert.Equal(3, viewer.FindMatchCount);
        Assert.Equal(0, viewer.CurrentFindMatchIndex);
        Assert.Equal(3, Layer(viewer).FindMatches.Count);
    }
}
