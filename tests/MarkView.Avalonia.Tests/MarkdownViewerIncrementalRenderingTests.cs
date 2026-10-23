// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// <see cref="MarkdownViewer.IsIncrementalRenderingEnabled"/>: a new <see cref="MarkdownViewer.Markdown"/>
/// text re-renders only the blocks that changed, and keeps the rest of the viewer's state.
/// </summary>
public class MarkdownViewerIncrementalRenderingTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public ScrollViewer ScrollViewer => Viewer.GetVisualDescendants().OfType<ScrollViewer>().First();

        public StackPanel Root => Assert.IsType<StackPanel>(Assert.IsType<Grid>(Viewer.Content).Children[0]);

        public DocumentSelectionLayer Layer =>
            Assert.IsType<DocumentSelectionLayer>(Assert.IsType<Grid>(Viewer.Content).Children[1]);

        public LinkFocusTarget[] Targets => Viewer.GetVisualDescendants().OfType<LinkFocusTarget>().ToArray();

        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

        public void Dispose() => Window.Close();
    }

    // Mutation testing compiles the library without its AXAML, so the viewer gets a minimal template with
    // the PART_ScrollViewer instead of the theme's.
    private static Fixture Show(string markdown, bool incremental = true, Action<MarkdownViewer>? configure = null)
    {
        var viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create(), IsIncrementalRenderingEnabled = incremental };
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        return new Fixture(window, viewer);
    }

    private static string Paragraphs(string prefix, int count) =>
        string.Join("\n\n", Enumerable.Range(0, count).Select(i => $"{prefix} {i}"));

    [AvaloniaFact]
    public void Is_off_by_default()
    {
        Assert.False(new MarkdownViewer().IsIncrementalRenderingEnabled);
    }

    [AvaloniaFact]
    public void When_off_a_new_text_renders_everything_again()
    {
        using var f = Show("a\n\nb", incremental: false);
        var grid = f.Viewer.Content;

        f.Viewer.Markdown = "a\n\nbe";

        Assert.NotSame(grid, f.Viewer.Content);
    }

    [AvaloniaFact]
    public void Edit_keeps_the_content_and_the_unchanged_blocks()
    {
        using var f = Show("a\n\nb\n\nc");
        var grid = f.Viewer.Content;
        var before = f.Root.Children.ToArray();

        f.Viewer.Markdown = "a\n\nbe\n\nc";

        Assert.Same(grid, f.Viewer.Content);
        Assert.Same(before[0], f.Root.Children[0]);
        Assert.NotSame(before[1], f.Root.Children[1]);
        Assert.Same(before[2], f.Root.Children[2]);
        f.Viewer.SelectAll();
        Assert.Equal("a\nbe\nc", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public async Task Edit_keeps_the_scroll_offset()
    {
        using var f = Show(Paragraphs("Paragraph", 40));
        await AsyncTestHelpers.PumpAsync();
        f.ScrollViewer.Offset = new Vector(0, 500);
        await AsyncTestHelpers.PumpAsync();

        f.Viewer.Markdown = Paragraphs("Paragraph", 40).Replace("Paragraph 20", "Paragraph twenty", StringComparison.Ordinal);
        await AsyncTestHelpers.PumpAsync();

        Assert.Equal(500, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public async Task Another_document_scrolls_to_the_top()
    {
        using var f = Show(Paragraphs("Paragraph", 40));
        await AsyncTestHelpers.PumpAsync();
        f.ScrollViewer.Offset = new Vector(0, 500);
        await AsyncTestHelpers.PumpAsync();

        f.Viewer.Markdown = Paragraphs("Line", 40);
        await AsyncTestHelpers.PumpAsync();

        Assert.Equal(0, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public async Task Edit_of_a_one_block_document_keeps_the_scroll_offset()
    {
        var list = string.Join("\n", Enumerable.Range(0, 60).Select(i => $"- item {i}"));
        using var f = Show(list);
        await AsyncTestHelpers.PumpAsync();
        f.ScrollViewer.Offset = new Vector(0, 300);
        await AsyncTestHelpers.PumpAsync();

        f.Viewer.Markdown = list.Replace("item 30", "item thirty", StringComparison.Ordinal);
        await AsyncTestHelpers.PumpAsync();

        Assert.Equal(300, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public async Task Edit_from_one_block_to_several_keeps_the_scroll_offset()
    {
        var list = string.Join("\n", Enumerable.Range(0, 60).Select(i => $"- item {i}"));
        using var f = Show(list);
        await AsyncTestHelpers.PumpAsync();
        f.ScrollViewer.Offset = new Vector(0, 300);
        await AsyncTestHelpers.PumpAsync();

        f.Viewer.Markdown = list.Replace("item 30", "item thirty", StringComparison.Ordinal) + "\n\nafter";
        await AsyncTestHelpers.PumpAsync();

        Assert.Equal(300, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public async Task Edit_from_several_blocks_to_one_keeps_the_scroll_offset()
    {
        var list = string.Join("\n", Enumerable.Range(0, 60).Select(i => $"- item {i}"));
        using var f = Show(list + "\n\nafter");
        await AsyncTestHelpers.PumpAsync();
        f.ScrollViewer.Offset = new Vector(0, 300);
        await AsyncTestHelpers.PumpAsync();

        f.Viewer.Markdown = list.Replace("item 30", "item thirty", StringComparison.Ordinal);
        await AsyncTestHelpers.PumpAsync();

        Assert.Equal(300, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public void Focus_returns_to_the_viewer_when_its_block_is_rendered_again()
    {
        using var f = Show("[a](x)\n\nb");
        f.Window.UpdateLayout();
        Assert.True(f.Targets.Single().Focus());

        f.Viewer.Markdown = "[a](x) more\n\nb";

        Assert.True(f.Viewer.IsFocused);
        Assert.Same(f.Viewer, f.Focused);
    }

    [AvaloniaFact]
    public void Screen_readers_are_told_only_when_a_block_is_rendered_again()
    {
        using var f = Show("a\n\nb");
        var peer = ControlAutomationPeer.CreatePeerForElement(f.Viewer);
        var changes = 0;
        peer.ChildrenChanged += (_, _) => changes++;

        // A blank line more between two blocks keeps both.
        f.Viewer.Markdown = "a\n\n\nb";
        Assert.Equal(0, changes);

        f.Viewer.Markdown = "a\n\n\nc";
        Assert.Equal(1, changes);
    }

    [AvaloniaFact]
    public void Selection_in_a_kept_block_survives()
    {
        using var f = Show("alpha\n\nbeta\n\ngamma");
        f.Layer.SetSelectionForTest(12, 15);   // "amm"

        f.Viewer.Markdown = "a\n\nbeta\n\ngamma";

        Assert.Equal("amm", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Search_goes_on_in_the_edited_text()
    {
        using var f = Show("cat\n\ndog\n\ncat");
        Assert.Equal(2, f.Viewer.Find("cat"));
        f.Viewer.FindNext();

        f.Viewer.Markdown = "cat\n\ncat dog\n\ncat";

        Assert.Equal(3, f.Viewer.FindMatchCount);
        Assert.Equal(3, f.Layer.FindMatches.Count);
    }

    [AvaloniaFact]
    public void Folded_alert_after_the_change_stays_folded()
    {
        using var f = Show("a\n\n> [!NOTE]+\n> Hidden", configure: v => v.UseAlertBlocks());
        var alert = Assert.IsType<Border>(f.Root.Children[1]);
        CollapsibleAlert.Get(alert)!.Toggle();
        Assert.False(CollapsibleAlert.Get(alert)!.IsExpanded);

        f.Viewer.Markdown = "ab\n\n> [!NOTE]+\n> Hidden";

        Assert.Same(alert, f.Root.Children[1]);
        Assert.False(CollapsibleAlert.Get(alert)!.IsExpanded);
    }

    [AvaloniaFact]
    public void Folded_alert_moved_by_an_edit_stays_folded_when_rendered_again()
    {
        using var f = Show("a\n\n> [!NOTE]+\n> Hidden", configure: v => v.UseAlertBlocks());
        CollapsibleAlert.Get((Border)f.Root.Children[1])!.Toggle();
        f.Viewer.Markdown = "abc\n\n> [!NOTE]+\n> Hidden";

        f.Viewer.Markdown = "abc\n\n> [!NOTE]+\n> Hidden text";

        Assert.False(CollapsibleAlert.Get((Border)f.Root.Children[1])!.IsExpanded);
    }

    [AvaloniaFact]
    public void Folded_alert_moved_by_an_edit_before_it_was_folded_stays_folded_when_rendered_again()
    {
        using var f = Show("a\n\n> [!NOTE]+\n> Hidden", configure: v => v.UseAlertBlocks());
        f.Viewer.Markdown = "abc\n\n> [!NOTE]+\n> Hidden";
        CollapsibleAlert.Get((Border)f.Root.Children[1])!.Toggle();

        f.Viewer.Markdown = "abc\n\n> [!NOTE]+\n> Hidden text";

        Assert.False(CollapsibleAlert.Get((Border)f.Root.Children[1])!.IsExpanded);
    }

    [AvaloniaFact]
    public void Block_inserted_in_a_collapsed_section_is_hidden_with_it()
    {
        using var f = Show("# A\n\nx\n\n# B\n\ny", configure: v => v.AreHeadingsFoldable = true);
        HeadingFold.Get(f.Root.Children[0])!.Toggle();

        f.Viewer.Markdown = "# A\n\nx\n\nnew\n\n# B\n\ny";

        Assert.Equal([true, false, false, true, true], f.Root.Children.Select(c => c.IsVisible));
    }

    [AvaloniaFact]
    public void Collapsed_heading_moved_by_an_edit_keeps_its_section_hidden()
    {
        using var f = Show("intro\n\n# A\n\nx", configure: v => v.AreHeadingsFoldable = true);
        var heading = f.Root.Children[1];
        HeadingFold.Get(heading)!.Toggle();

        f.Viewer.Markdown = "intro text\n\n# A\n\nx";
        f.Viewer.Markdown = "intro text\n\n# A\n\nx changed";

        Assert.Same(heading, f.Root.Children[1]);
        Assert.False(f.Root.Children[2].IsVisible);
    }

    [AvaloniaFact]
    public void Copy_as_markdown_reads_the_new_source_of_blocks_kept_or_rendered_again()
    {
        using var f = Show("one\n\n| h |\n|---|\n| c |\n\ntwo");

        f.Viewer.Markdown = "one more\n\n| h |\n|---|\n| c |\n\ntwo";
        f.Viewer.SelectAll();
        Assert.Equal("one more\n\n| h |\n|---|\n| c |\n\ntwo", f.Viewer.GetSelectedMarkdown());

        f.Viewer.Markdown = "one more\n\n| h |\n|---|\n| d |\n\ntwo";
        Assert.Same(AvaloniaRenderer.GetTopLevelBlock(f.Root.Children[1]), f.Layer.Entries.First(e => e.PlainText == "d").TopLevelBlock);
        f.Viewer.SelectAll();
        Assert.Equal("one more\n\n| h |\n|---|\n| d |\n\ntwo", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Reasoning_still_in_progress_renders_only_its_new_children_and_moves_with_an_edit_before_it()
    {
        using var f = Show("<think>\nfirst\n\nsecond", configure: v => v.UseThinkingBlocks());
        var block = f.Root.Children[0];
        var first = block.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>().First();

        f.Viewer.Markdown = "<think>\nfirst\n\nsecond and more";
        Assert.Same(block, f.Root.Children[0]);
        Assert.Same(first, block.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>().First());

        f.Viewer.Markdown = "intro\n\n<think>\nfirst\n\nsecond and more";
        Assert.Same(block, f.Root.Children[1]);

        f.Viewer.Markdown = "intro\n\n<think>\nfirst\n\nsecond and more\n\nthird";
        Assert.Equal(["first", "second and more", "third"],
            f.Root.Children[1].GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>()
                .Select(t => MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!)).Where(t => t is "first" or "second and more" or "third"));
    }

    [AvaloniaFact]
    public void Task_toggled_then_written_back_keeps_its_block()
    {
        using var f = Show("- [ ] a\n\nb", configure: v => v.AreTaskListsInteractive = true);
        var list = f.Root.Children[0];
        string? written = null;
        f.Viewer.TaskToggled += (_, e) => written = f.Viewer.Markdown!.Remove(e.SourceStart, e.SourceLength).Insert(e.SourceStart, e.IsChecked ? "[x]" : "[ ]");

        list.GetLogicalDescendants().OfType<ToggleButton>().Single().IsChecked = true;
        f.Viewer.Markdown = written + " more";

        Assert.Same(list, f.Root.Children[0]);
    }

    [AvaloniaFact]
    public void Task_after_an_edit_reports_its_new_position()
    {
        using var f = Show("x\n\n- [ ] a", configure: v => v.AreTaskListsInteractive = true);
        TaskToggledEventArgs? toggled = null;
        f.Viewer.TaskToggled += (_, e) => toggled = e;

        f.Viewer.Markdown = "xyz\n\n- [ ] a";
        f.Root.Children[1].GetLogicalDescendants().OfType<ToggleButton>().Single().IsChecked = true;

        Assert.Equal("xyz\n\n- ".Length, toggled!.SourceStart);
    }

    [AvaloniaFact]
    public void Table_of_contents_follows_the_edit()
    {
        using var f = Show("# A\n\nx\n\n# B");

        f.Viewer.Markdown = "# A\n\nx\n\n## A2\n\n# B";

        Assert.Equal(["A", "B"], f.Viewer.TableOfContents.Select(e => e.Text));
        Assert.Equal(["A2"], f.Viewer.TableOfContents[0].Children.Select(e => e.Text));
    }

    [AvaloniaFact]
    public void Emptied_text_clears_the_content_and_the_next_one_renders()
    {
        using var f = Show("a");

        f.Viewer.Markdown = "";
        Assert.Null(f.Viewer.Content);

        f.Viewer.Markdown = "b\n\nc";
        var grid = f.Viewer.Content;
        f.Viewer.Markdown = "b\n\ncd";
        Assert.Same(grid, f.Viewer.Content);
    }

    [AvaloniaFact]
    public void Rendering_property_change_renders_everything_again()
    {
        using var f = Show("a\n\nb");
        var grid = f.Viewer.Content;

        f.Viewer.ShowCodeBlockLineNumbers = !f.Viewer.ShowCodeBlockLineNumbers;
        Assert.NotSame(grid, f.Viewer.Content);

        // And the new content is edited in place again.
        grid = f.Viewer.Content;
        f.Viewer.Markdown = "a\n\nbe";
        Assert.Same(grid, f.Viewer.Content);
    }

    [AvaloniaFact]
    public void Turned_off_the_next_text_renders_everything_again()
    {
        using var f = Show("a\n\nb");
        var grid = f.Viewer.Content;

        f.Viewer.IsIncrementalRenderingEnabled = false;
        f.Viewer.Markdown = "a\n\nbe";

        Assert.NotSame(grid, f.Viewer.Content);
    }

    [AvaloniaFact]
    public void Turned_off_and_on_again_the_next_text_renders_everything()
    {
        using var f = Show("a\n\nb");
        var grid = f.Viewer.Content;

        f.Viewer.IsIncrementalRenderingEnabled = false;
        f.Viewer.IsIncrementalRenderingEnabled = true;
        f.Viewer.Markdown = "a\n\nbe";

        Assert.NotSame(grid, f.Viewer.Content);
    }

    [AvaloniaFact]
    public void Turned_on_the_next_text_renders_everything_once()
    {
        using var f = Show("a\n\nb", incremental: false);

        f.Viewer.IsIncrementalRenderingEnabled = true;
        f.Viewer.Markdown = "a\n\nbe";
        var grid = f.Viewer.Content;
        f.Viewer.Markdown = "a\n\nbee";

        Assert.Same(grid, f.Viewer.Content);
    }

    [AvaloniaFact]
    public void Completed_session_leaves_a_document_edited_in_place()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a\n\nb");
        session.Complete();
        var grid = f.Viewer.Content;
        var first = f.Root.Children[0];

        f.Viewer.Markdown = "a\n\nbe";

        Assert.Same(grid, f.Viewer.Content);
        Assert.Same(first, f.Root.Children[0]);
        f.Viewer.SelectAll();
        Assert.Equal("a\nbe", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Direct_set_during_a_session_ends_it_and_edits_in_place()
    {
        using var f = Show("a\n\nb");
        var grid = f.Viewer.Content;
        var session = f.Viewer.BeginStreaming();
        session.Append(" more");
        session.ApplyPendingUpdate();

        f.Viewer.Markdown = "a\n\nc";

        Assert.False(f.Viewer.IsStreaming);
        Assert.Same(grid, f.Viewer.Content);
        f.Viewer.SelectAll();
        Assert.Equal("a\nc", f.Viewer.GetSelectedText());
        Assert.Throws<InvalidOperationException>(() => session.Append("x"));
    }

    [AvaloniaFact]
    public void Direct_set_during_a_session_renders_as_outside_one()
    {
        var capture = new RendererCapture();
        using var f = Show("", configure: v =>
        {
            v.UseThinkingBlocks();
            v.Extensions.Add(capture);
        });
        var session = f.Viewer.BeginStreaming();
        session.Append("<think>\nidea");
        session.ApplyPendingUpdate();

        f.Viewer.Markdown = "<think>\nidea and more";

        // Custom renderers see what they see outside a session, and an unclosed thinking block is no
        // longer in progress.
        var renderer = capture.Renderer!;
        Assert.False(renderer.IsStreaming);
        Assert.Equal(int.MaxValue, renderer.ProvisionalStart);
        var header = f.Root.GetLogicalDescendants().OfType<FoldHeader>().Single();
        Assert.DoesNotContain(new ThinkingBlockOptions().ThinkingTitle, MarkdownSelectableTextBlock.ExtractPlainText(header.Inlines!), StringComparison.Ordinal);
    }

    private sealed class RendererCapture : IMarkViewExtension
    {
        public AvaloniaRenderer? Renderer { get; private set; }

        public void Register(AvaloniaRenderer renderer) => Renderer = renderer;
    }

    [AvaloniaFact]
    public void Session_starts_from_the_document_already_shown()
    {
        using var f = Show("a\n\nb");
        var first = f.Root.Children[0];

        var session = f.Viewer.BeginStreaming();
        session.Append("\n\nc");
        session.ApplyPendingUpdate();

        Assert.Same(first, f.Root.Children[0]);
    }

    [AvaloniaFact]
    public void Turning_off_during_a_session_keeps_the_session_document()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a\n\nb");
        session.ApplyPendingUpdate();
        var grid = f.Viewer.Content;

        f.Viewer.IsIncrementalRenderingEnabled = false;
        session.Append(" more");
        session.Complete();

        Assert.Same(grid, f.Viewer.Content);
        f.Viewer.Markdown = "a\n\nc";
        Assert.NotSame(grid, f.Viewer.Content);
    }
}
