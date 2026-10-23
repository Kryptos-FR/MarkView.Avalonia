// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Markdig;
using Markdig.Syntax;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// With <see cref="MarkdownViewer.IsVirtualizationEnabled"/>, only the top-level blocks near the visible area are
/// in the visual tree, while everything that reads the document still sees all of it.
/// </summary>
public class MarkdownViewerVirtualizationTests
{
    private static string Paragraphs(int count, Func<int, string>? text = null) =>
        string.Join("\n\n", Enumerable.Range(0, count).Select(text ?? (i => $"Paragraph {i}")));

    private sealed class Fixture : IDisposable
    {
        public MarkdownViewer Viewer { get; }
        public Window Window { get; }

        // Whether Settle runs the dispatcher's queued jobs, among which the background build of the blocks.
        private readonly bool _idle;

        public Fixture(string markdown, Action<MarkdownViewer>? configure = null, bool virtualize = true, bool idle = true)
        {
            _idle = idle;
            Viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create(), IsVirtualizationEnabled = virtualize };
            configure?.Invoke(Viewer);
            Viewer.Markdown = markdown;
            Window = new Window { Width = 400, Height = 300, Content = Viewer };
            Window.Show();
            Settle();
        }

        public ScrollViewer ScrollViewer => Viewer.GetVisualDescendants().OfType<ScrollViewer>().First();

        public StackPanel Root => Assert.IsAssignableFrom<StackPanel>(Assert.IsType<Grid>(Viewer.Content).Children[0]);

        public VirtualizingBlockPanel Panel => Assert.IsType<VirtualizingBlockPanel>(Root);

        public bool IsAttached(Control block) => block.GetVisualParent() == Root;

        public IEnumerable<int> AttachedIndexes => Root.Children.Select((c, i) => (c, i)).Where(x => IsAttached(x.c)).Select(x => x.i);

        public double Top(Visual visual) => visual.TranslatePoint(default, ScrollViewer)!.Value.Y;

        public bool IsInView(Visual visual) => visual.IsAttachedToVisualTree() && Top(visual) >= 0 && Top(visual) < ScrollViewer.Viewport.Height;

        public TextBlock Paragraph(int index) => Assert.IsAssignableFrom<TextBlock>(Root.Children[index]);

        public void ScrollTo(double y)
        {
            ScrollViewer.Offset = new Vector(0, y);
            Settle();
        }

        public void ScrollToEnd()
        {
            // The extent changes as the blocks at the end replace their estimates.
            for (var i = 0; i < 5; i++)
                ScrollTo(ScrollViewer.Extent.Height);
        }

        public void Settle()
        {
            if (_idle)
                Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Window.UpdateLayout();
        }

        public void Dispose() => Window.Close();
    }

    [AvaloniaFact]
    public void Off_by_default_the_document_is_a_stack_panel_with_every_block_attached()
    {
        using var f = new Fixture(Paragraphs(100), virtualize: false);

        Assert.False(new MarkdownViewer().IsVirtualizationEnabled);
        Assert.IsType<StackPanel>(f.Root);
        Assert.All(f.Root.Children, child => Assert.True(f.IsAttached(child)));
    }

    [AvaloniaFact]
    public void Only_the_blocks_near_the_visible_area_are_attached_but_every_block_is_a_child()
    {
        using var f = new Fixture(Paragraphs(300));

        Assert.Equal(300, f.Panel.Children.Count);
        var attached = f.AttachedIndexes.ToList();
        Assert.Equal(Enumerable.Range(0, attached.Count), attached);
        // The visible area and one viewport height below it: a few dozen one-line paragraphs, not all of them.
        Assert.InRange(attached.Count, 15, 60);
        Assert.Equal(attached.Count, f.Panel.AttachedChildren.Count);
        Assert.Equal(300 - attached.Count, f.Panel.DetachedChildren.Count());
        Assert.Null(f.Root.Children[^1].GetLogicalParent());
    }

    [AvaloniaFact]
    public void Scrolling_attaches_the_blocks_that_come_near_and_detaches_those_left_far_behind()
    {
        using var f = new Fixture(Paragraphs(300));

        f.ScrollToEnd();

        Assert.True(f.IsAttached(f.Root.Children[^1]));
        Assert.True(f.IsInView(f.Root.Children[^1]));
        Assert.False(f.IsAttached(f.Root.Children[0]));
        var attached = f.AttachedIndexes.ToList();
        Assert.Equal(Enumerable.Range(attached[0], attached.Count), attached);
    }

    [AvaloniaFact]
    public void Blocks_scrolled_a_little_out_of_the_lead_stay_attached()
    {
        using var f = new Fixture(Paragraphs(300));
        var first = f.Root.Children[0];

        // Two viewport heights down: past the one-viewport lead that attaches blocks, within the three that keep them.
        f.ScrollTo(2 * f.ScrollViewer.Viewport.Height);

        Assert.True(f.IsAttached(first));
    }

    [AvaloniaFact]
    public void The_extent_estimates_the_blocks_never_laid_out_from_those_that_were()
    {
        var markdown = Paragraphs(300);
        double expected;
        using (var plain = new Fixture(markdown, virtualize: false))
            expected = plain.ScrollViewer.Extent.Height;

        using var f = new Fixture(markdown);

        Assert.InRange(f.ScrollViewer.Extent.Height, expected * 0.9, expected * 1.1);
        f.ScrollToEnd();
        // Every block laid out once: the extent is exact.
        f.ScrollTo(0);
        for (var y = 0.0; y < f.ScrollViewer.Extent.Height; y += f.ScrollViewer.Viewport.Height)
            f.ScrollTo(y);
        Assert.Equal(expected, f.ScrollViewer.Extent.Height, 0.5);
    }

    [AvaloniaFact]
    public void Selecting_all_copies_the_blocks_left_out_as_well()
    {
        using var f = new Fixture(Paragraphs(300));

        f.Viewer.SelectAll();

        Assert.Equal(Paragraphs(300, i => $"Paragraph {i}").Replace("\n\n", "\n"), f.Viewer.GetSelectedText().Replace("\n\n", "\n").TrimEnd('\n'));
    }

    [AvaloniaFact]
    public void Find_counts_the_matches_in_blocks_left_out_and_brings_the_one_it_moves_to_into_view()
    {
        using var f = new Fixture(Paragraphs(300, i => i == 250 ? "The needle is here" : $"Paragraph {i}"));
        Assert.False(f.IsAttached(f.Root.Children[250]));

        Assert.Equal(1, f.Viewer.Find("needle"));
        Assert.True(f.Viewer.FindNext());
        f.Settle();

        Assert.True(f.IsAttached(f.Root.Children[250]));
        Assert.True(f.IsInView(f.Root.Children[250]));
    }

    [AvaloniaFact]
    public void A_jump_to_an_anchor_in_a_block_left_out_shows_it_at_the_top()
    {
        using var f = new Fixture(Paragraphs(300, i => i % 30 == 0 ? $"## Section {i / 30}" : $"Paragraph {i}"),
            viewer => viewer.IsSmoothScrollingEnabled = false);
        var heading = f.Root.Children[240];
        Assert.False(f.IsAttached(heading));

        f.Viewer.ScrollToAnchor("section-8");
        f.Settle();

        Assert.True(f.IsAttached(heading));
        Assert.InRange(f.Top(heading), 0, 40);
    }

    [AvaloniaFact]
    public void Tab_reaches_a_link_in_a_block_left_out_and_its_block_stays_attached_while_it_has_the_focus()
    {
        using var f = new Fixture(Paragraphs(300, i => i is 0 or 200 ? $"[Link {i}](https://example.com/{i})" : $"Paragraph {i}"));
        var targets = f.Viewer.GetVisualDescendants().OfType<LinkFocusTarget>().ToList();
        Assert.Equal(2, targets.Count);
        Assert.False(f.IsAttached(f.Root.Children[200]));

        targets[0].Focus(NavigationMethod.Tab);
        var (handled, next) = ((ICustomKeyboardNavigation)f.Viewer).GetNext(targets[0], NavigationDirection.Next);

        Assert.True(handled);
        Assert.Same(targets[1], next);
        Assert.True(f.IsAttached(f.Root.Children[200]));
        Assert.NotEqual(default, targets[1].GeometryBounds);

        targets[1].Focus(NavigationMethod.Tab);
        f.ScrollTo(0);

        Assert.True(f.IsAttached(f.Root.Children[200]));
    }

    [AvaloniaFact]
    public void A_focusable_control_in_a_block_left_out_is_still_a_tab_stop()
    {
        using var f = new Fixture(Paragraphs(300) + "\n\n```\ncode\n```", viewer => viewer.ShowCodeBlockCopyButton = true);
        var codeBlock = f.Root.Children[^1];
        Assert.False(f.IsAttached(codeBlock));

        var stops = f.Viewer.GetVisualDescendants().OfType<LinkFocusLayer>().Single().GetActiveStops();

        var button = Assert.IsType<Button>(Assert.Single(stops));
        Assert.Same(codeBlock, f.Panel.BlockOf(button));

        // Reached with Tab, it is attached, and stays so while it has the focus.
        Assert.Same(button, ((ICustomKeyboardNavigation)f.Viewer).GetNext(f.Viewer, NavigationDirection.Next).next);
        button.Focus(NavigationMethod.Tab);
        f.ScrollTo(0);
        Assert.True(f.IsAttached(codeBlock));
    }

    [AvaloniaFact]
    public void Child_indexes_count_every_block_so_nth_child_selectors_see_the_document_order()
    {
        using var f = new Fixture(Paragraphs(300));
        f.Window.Styles.Add(new Style(x => x.Is<TextBlock>().NthLastChild(0, 1)) { Setters = { new Setter(TextBlock.FontSizeProperty, 31.0) } });
        f.ScrollToEnd();

        var provider = (IChildIndexProvider)f.Panel;
        Assert.Equal(299, provider.GetChildIndex(f.Root.Children[^1]));
        Assert.Equal(-1, provider.GetChildIndex(new LogicalStub()));
        Assert.True(provider.TryGetTotalCount(out var count));
        Assert.Equal(300, count);
        Assert.Equal(31, f.Paragraph(299).FontSize);
        Assert.NotEqual(31, f.Paragraph(f.AttachedIndexes.First()).FontSize);
    }

    private sealed class LogicalStub : StyledElement;

    [AvaloniaFact]
    public void Pointer_hit_testing_finds_the_text_of_a_block_far_down_the_document()
    {
        using var f = new Fixture(Paragraphs(300));
        f.ScrollToEnd();
        var layer = f.Viewer.GetVisualDescendants().OfType<DocumentSelectionLayer>().Single();
        var paragraph = f.Paragraph(295);
        var entry = layer.Entries.Single(e => e.Element == paragraph);

        var offset = layer.HitTestOffset(paragraph.TranslatePoint(new Point(1, 5), layer)!.Value);

        Assert.Equal(entry.AbsStart, offset);
    }

    [AvaloniaFact]
    public void A_preset_added_later_restyles_the_blocks_left_out_once_they_come_into_view()
    {
        using var f = new Fixture(Paragraphs(300) + "\n\n# Title", viewer => viewer.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml"),
        }));
        var heading = Assert.IsAssignableFrom<TextBlock>(f.Root.Children[^1]);
        Assert.False(f.IsAttached(heading));

        f.Viewer.Styles.Add(new Styles { Resources = { ["MarkdownHeading1Theme"] = new ControlTheme(typeof(TextBlock)) { Setters = { new Setter(TextBlock.FontSizeProperty, 50.0) } } } });
        f.ScrollToEnd();

        Assert.True(f.IsAttached(heading));
        Assert.Equal(50, heading.FontSize);
    }

    [AvaloniaFact]
    public void Turning_it_on_or_off_renders_the_document_again()
    {
        using var f = new Fixture(Paragraphs(50), virtualize: false);

        f.Viewer.IsVirtualizationEnabled = true;
        Assert.IsType<VirtualizingBlockPanel>(f.Root);

        f.Viewer.IsVirtualizationEnabled = false;
        Assert.IsType<StackPanel>(f.Root);
    }

    [AvaloniaFact]
    public void Folded_sections_take_no_room()
    {
        using var f = new Fixture("# Top\n\n" + Paragraphs(300), viewer => viewer.AreHeadingsFoldable = true);
        var extent = f.ScrollViewer.Extent.Height;

        foreach (var child in f.Root.Children.Skip(1))
            child.IsVisible = false;
        f.Settle();

        Assert.True(f.ScrollViewer.Extent.Height < extent / 10);
        Assert.Equal([0], f.AttachedIndexes);
    }

    [AvaloniaFact]
    public void Incremental_edits_and_streaming_splice_the_blocks_attached_or_not()
    {
        using var f = new Fixture(Paragraphs(300), viewer => viewer.IsIncrementalRenderingEnabled = true);
        f.ScrollToEnd();

        // Removes a block left out and one attached.
        f.Viewer.Markdown = Paragraphs(300, i => $"Paragraph {i}").Replace("Paragraph 3\n\n", "").Replace("\n\nParagraph 298", "");
        f.Settle();

        Assert.Equal(298, f.Root.Children.Count);
        Assert.Equal(f.AttachedIndexes, f.Panel.AttachedChildren.Select(c => f.Root.Children.IndexOf(c)));
        Assert.True(f.IsInView(f.Root.Children[^1]));

        var session = f.Viewer.BeginStreaming();
        session.Append("\n\nParagraph 300\n\nParagraph 301");
        session.Complete();
        f.Settle();

        Assert.Equal(300, f.Root.Children.Count);
        f.Viewer.SelectAll();
        Assert.EndsWith("Paragraph 301", f.Viewer.GetSelectedText().TrimEnd('\n'));
    }

    [AvaloniaFact]
    public void Only_the_blocks_near_the_visible_area_are_built_at_first_and_the_others_while_idle()
    {
        var markdown = Paragraphs(300, i => i % 30 == 0 ? $"## Section {i / 30}" : $"Paragraph {i}");
        double expected;
        using (var plain = new Fixture(markdown, virtualize: false))
            expected = plain.ScrollViewer.Extent.Height;

        using var f = new Fixture(markdown, idle: false);

        var built = f.Root.Children.Count;
        Assert.InRange(built, 15, 60);
        Assert.Equal(300 - built, f.Viewer.PendingBlockCount);
        Assert.Equal(Enumerable.Range(0, built), f.AttachedIndexes);
        // The blocks not built yet are estimated like those built but never laid out.
        Assert.InRange(f.ScrollViewer.Extent.Height, expected * 0.9, expected * 1.1);
        // The table of contents waits for the whole document.
        Assert.Empty(f.Viewer.TableOfContents);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(300, f.Root.Children.Count);
        Assert.Equal(0, f.Viewer.PendingBlockCount);
        Assert.Equal(10, f.Viewer.TableOfContents.Count);
        Assert.Equal(Paragraphs(300, i => i % 30 == 0 ? $"Section {i / 30}" : $"Paragraph {i}").Replace("\n\n", "\n"),
            SelectedAll(f));
    }

    private static string SelectedAll(Fixture f)
    {
        f.Viewer.SelectAll();
        return f.Viewer.GetSelectedText().Replace("\n\n", "\n").TrimEnd('\n');
    }

    [AvaloniaFact]
    public void Scrolling_far_builds_every_block_up_to_the_visible_area_before_it_is_drawn()
    {
        using var f = new Fixture(Paragraphs(300), idle: false);

        f.ScrollTo(f.ScrollViewer.Extent.Height / 2);

        var attached = f.AttachedIndexes.ToList();
        Assert.InRange(attached[0], 100, 200);
        Assert.Contains(attached, i => f.IsInView(f.Root.Children[i]));
        Assert.True(f.Viewer.PendingBlockCount > 0);

        f.ScrollToEnd();

        Assert.Equal(0, f.Viewer.PendingBlockCount);
        Assert.True(f.IsInView(f.Paragraph(299)));
    }

    [AvaloniaFact]
    public void Find_select_all_anchors_and_tab_build_the_blocks_they_need()
    {
        var markdown = Paragraphs(300, i => i switch { 250 => "## Far", 280 => "[Far link](https://example.com)", _ => $"Paragraph {i}" });

        using (var f = new Fixture(markdown, idle: false))
        {
            Assert.Equal(1, f.Viewer.Find("Paragraph 299"));
            Assert.Equal(0, f.Viewer.PendingBlockCount);
        }
        using (var f = new Fixture(markdown, idle: false))
        {
            Assert.EndsWith("Paragraph 299", SelectedAll(f));
        }
        using (var f = new Fixture(markdown, viewer => viewer.IsSmoothScrollingEnabled = false, idle: false))
        {
            f.Viewer.ScrollToAnchor("far");
            f.Settle();

            Assert.InRange(f.Top(f.Root.Children[250]), 0, 40);
        }
        using (var f = new Fixture(markdown, idle: false))
        {
            var (handled, next) = ((ICustomKeyboardNavigation)f.Viewer).GetNext(f.Viewer, NavigationDirection.Next);

            Assert.True(handled);
            var target = Assert.IsType<LinkFocusTarget>(next);
            Assert.Same(f.Root.Children[280], target.TextBlock);
        }
    }

    [AvaloniaFact]
    public void A_new_document_drops_the_blocks_the_last_one_had_not_built()
    {
        using var f = new Fixture(Paragraphs(300), idle: false);

        f.Viewer.Markdown = Paragraphs(200, i => $"Other {i}");
        f.Settle();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(200, f.Root.Children.Count);
        Assert.Equal(Paragraphs(200, i => $"Other {i}").Replace("\n\n", "\n"), SelectedAll(f));
    }

    [AvaloniaFact]
    public void An_edit_or_a_stream_builds_the_blocks_left_before_it_updates_the_document()
    {
        using var f = new Fixture(Paragraphs(300), viewer => viewer.IsIncrementalRenderingEnabled = true, idle: false);
        var first = f.Root.Children[0];
        Assert.True(f.Viewer.PendingBlockCount > 0);

        f.Viewer.Markdown = Paragraphs(300).Replace("Paragraph 299", "Last");
        f.Settle();

        Assert.Same(first, f.Root.Children[0]);
        Assert.Equal(0, f.Viewer.PendingBlockCount);
        Assert.EndsWith("Paragraph 298\nLast", SelectedAll(f));

        f.Viewer.IsVirtualizationEnabled = false;
        f.Viewer.IsVirtualizationEnabled = true;
        Assert.True(f.Viewer.PendingBlockCount > 0);
        var session = f.Viewer.BeginStreaming();
        session.Append("\n\nStreamed");
        session.Complete();
        f.Settle();

        Assert.Equal(0, f.Viewer.PendingBlockCount);
        Assert.EndsWith("Last\nStreamed", SelectedAll(f));
    }

    [AvaloniaFact]
    public void Blocks_are_weighed_by_their_lines_of_code_or_of_wrapped_text()
    {
        var document = Markdig.Markdown.Parse(
            "```\na\nb\nc\n```\n\n" + new string('x', 150) + "\n\n- one\n- two\n\n| a | b |\n|---|---|\n| " + new string('y', 150) + " | z |",
            new MarkdownPipelineBuilder().UsePipeTables().Build());

        Assert.Equal(4, VirtualizingBlockPanel.Weight(document[0], 80));
        Assert.Equal(3, VirtualizingBlockPanel.Weight(document[1], 80));
        // The list, each item and each item's paragraph.
        Assert.Equal(1 + 2 * (1 + 2), VirtualizingBlockPanel.Weight(document[2], 80));
        // The table, then each row as its tallest cell (the cell and its paragraph): one line in the header, two in the body row.
        Assert.Equal(1 + (1 + 2) + (1 + 3), VirtualizingBlockPanel.Weight(document[3], 80));
        // A leaf without text counts as one line, and a block of another kind as its space alone.
        Assert.Equal(2, VirtualizingBlockPanel.Weight(new ThematicBreakBlock(null), 80));
        Assert.Equal(1, VirtualizingBlockPanel.Weight(new OtherBlock(), 80));
    }

    private sealed class OtherBlock() : Block(null);
}
