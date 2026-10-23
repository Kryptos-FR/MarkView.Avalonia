// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Streaming;

/// <summary>
/// A live document updated with an edited text (<c>reuseAfterChange</c>) keeps the blocks before and
/// after the change.
/// </summary>
public class LiveDocumentEditTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();

    private static LiveDocument Live(string text)
    {
        var live = new LiveDocument(new AvaloniaRenderer { AreTaskListsInteractive = true }, _ => { });
        Edit(live, text);
        return live;
    }

    private static UpdateResult Edit(LiveDocument live, string text, IInputElement? focused = null) =>
        live.Update(Markdown.Parse(text, Pipeline), text, focused, reuseAfterChange: true);

    private static string AllText(LiveDocument live)
    {
        live.SelectionLayer.SelectAll();
        return live.SelectionLayer.GetSelectedText();
    }

    private static Control[] Children(LiveDocument live) => [.. live.RootPanel.Children];

    [AvaloniaFact]
    public void Edit_keeps_the_blocks_before_and_after_the_change()
    {
        var live = Live("a\n\nb\n\nc");
        var before = Children(live);

        var result = Edit(live, "a\n\nbe\n\nc");

        Assert.Equal(2, result.ReusedBlocks);
        Assert.Equal(2, result.UnchangedBlocks);
        Assert.Same(before[0], live.RootPanel.Children[0]);
        Assert.NotSame(before[1], live.RootPanel.Children[1]);
        Assert.Same(before[2], live.RootPanel.Children[2]);
        Assert.Equal("a\nbe\nc", AllText(live));
    }

    [AvaloniaFact]
    public void Inserted_block_goes_between_the_kept_ones()
    {
        var live = Live("a\n\nc");
        var before = Children(live);

        var result = Edit(live, "a\n\nb\n\nc");

        Assert.Equal(2, result.ReusedBlocks);
        Assert.Equal(3, live.RootPanel.Children.Count);
        Assert.Same(before[0], live.RootPanel.Children[0]);
        Assert.Same(before[1], live.RootPanel.Children[2]);
        Assert.Equal("a\nb\nc", AllText(live));
    }

    [AvaloniaFact]
    public void Removed_block_leaves_the_others_in_place()
    {
        var live = Live("a\n\nb\n\nc");
        var before = Children(live);

        Edit(live, "a\n\nc");

        Assert.Equal([before[0], before[2]], Children(live));
        Assert.Equal("a\nc", AllText(live));
    }

    [AvaloniaFact]
    public void Change_that_keeps_the_length_re_renders_the_block()
    {
        var live = Live("abc\n\nz");
        var before = Children(live);

        var result = Edit(live, "abd\n\nz");

        Assert.Equal(1, result.ReusedBlocks);
        Assert.NotSame(before[0], live.RootPanel.Children[0]);
        Assert.Same(before[1], live.RootPanel.Children[1]);
        Assert.Equal("abd\nz", AllText(live));
    }

    [AvaloniaFact]
    public void Without_reuse_after_the_change_the_later_blocks_are_rendered_again()
    {
        var live = Live("a\n\nb");
        var before = Children(live);

        var result = live.Update(Markdown.Parse("A\n\nb", Pipeline), "A\n\nb");

        Assert.Equal(0, result.ReusedBlocks);
        Assert.NotSame(before[1], live.RootPanel.Children[1]);
    }

    [AvaloniaFact]
    public void Text_added_to_an_unclosed_fence_is_rendered()
    {
        // Markdig leaves the span of a fence still open at the end on its first line.
        var live = Live("a\n\n```\ncode");

        Edit(live, "a\n\n```\ncode more");

        Assert.Equal("a\ncode more", AllText(live));
    }

    [AvaloniaFact]
    public void Blank_lines_between_blocks_change_nothing()
    {
        var live = Live("a\n\nb");
        var before = Children(live);

        var result = Edit(live, "a\n\n\n\nb");

        Assert.Equal(2, result.ReusedBlocks);
        Assert.Equal(before, Children(live));
    }

    [AvaloniaFact]
    public void Heading_after_the_change_takes_the_slug_its_text_now_gets()
    {
        var live = Live("a\n\n# Intro");
        var kept = live.RootPanel.Children[1];

        var result = Edit(live, "# Intro\n\na\n\n# Intro");

        // The kept heading was "intro" and is now "intro-1", so it is rendered again.
        Assert.Equal(1, result.ReusedBlocks);
        Assert.NotSame(kept, live.RootPanel.Children[2]);
        Assert.Equal(["intro", "intro-1"], live.Renderer.HeadingEntries.Select(h => h.Slug));
        Assert.Same(live.RootPanel.Children[0], live.Renderer.Anchors["intro"]);
        Assert.Same(live.RootPanel.Children[2], live.Renderer.Anchors["intro-1"]);
        Assert.True(result.HeadingsChanged);
    }

    [AvaloniaFact]
    public void Heading_after_the_change_keeps_its_control_and_anchor()
    {
        var live = Live("# A\n\nx\n\n# B");
        var heading = live.RootPanel.Children[2];

        var result = Edit(live, "# A\n\nxyz\n\n# B");

        Assert.Same(heading, live.RootPanel.Children[2]);
        Assert.Same(heading, live.Renderer.Anchors["b"]);
        Assert.Equal(["a", "b"], live.Renderer.HeadingEntries.Select(h => h.Slug));
        Assert.Equal(["a", "b"], live.Renderer.AnchorRegistrations.Select(a => a.Id));
        Assert.False(result.HeadingsChanged);
    }

    [AvaloniaFact]
    public void Task_after_the_change_reports_where_it_now_is()
    {
        var live = Live("x\n\n- [ ] t");
        var list = live.RootPanel.Children[1];
        TaskToggledEventArgs? toggled = null;
        live.Renderer.TaskToggled += (_, e) => toggled = e;

        Edit(live, "x\ny z\n\n- [ ] t");
        Assert.Same(list, live.RootPanel.Children[1]);
        list.GetLogicalDescendants().OfType<ToggleButton>().Single().IsChecked = true;

        Assert.NotNull(toggled);
        Assert.Equal("x\ny z\n\n- ".Length, toggled.SourceStart);
        Assert.Equal(3, toggled.Line);
    }

    [AvaloniaFact]
    public void Patched_text_keeps_the_toggled_block()
    {
        var live = Live("- [ ] a\n\nb");
        var list = live.RootPanel.Children[0];

        live.PatchText(2, 3, "[x]");
        var result = Edit(live, "- [x] a\n\nb more");

        Assert.Same(list, live.RootPanel.Children[0]);
        Assert.Equal(1, result.ReusedBlocks);
    }

    [AvaloniaFact]
    public void Patch_that_does_not_fit_the_text_is_ignored()
    {
        var live = Live("- [ ] a\n\nb");

        live.PatchText(9, 3, "[x]");
        live.PatchText(2, 3, "[xx]");
        live.PatchText(-1, 3, "[x]");

        // The text is unchanged, so the same text keeps the list.
        var list = live.RootPanel.Children[0];
        Edit(live, "- [ ] a\n\nb more");
        Assert.Same(list, live.RootPanel.Children[0]);
    }

    [AvaloniaFact]
    public void Footnotes_show_a_changed_definition()
    {
        // Markdig moves the definition into the footnote group, rendered last.
        var live = Live("p\n\nx[^1]\n\n[^1]: one\n\nq");
        var paragraph = live.RootPanel.Children[0];

        Edit(live, "p\n\nx[^1]\n\n[^1]: two\n\nq");

        Assert.Same(paragraph, live.RootPanel.Children[0]);
        Assert.Contains("two", AllText(live));
        Assert.DoesNotContain("one", AllText(live));
    }

    [AvaloniaFact]
    public void Changed_reference_definition_re_renders_every_block()
    {
        var live = Live("[a]\n\nb\n\n[a]: https://x.test/");

        var result = Edit(live, "[a]\n\nb\n\n[a]: https://y.test/");

        Assert.Equal(0, result.ReusedBlocks);
        Assert.True(result.UnchangedBlocks > 0);
        var link = live.RootPanel.GetLogicalDescendants().OfType<MarkdownHyperlink>().Single();
        Assert.Equal("https://y.test/", link.NavigateUri?.OriginalString);
    }

    [AvaloniaFact]
    public void Link_stops_stay_in_reading_order()
    {
        var live = Live("[a](https://a.test/)\n\n[c](https://c.test/)");

        Edit(live, "[a](https://a.test/)\n\n[b](https://b.test/)\n\n[c](https://c.test/)");

        Assert.Equal(["a", "b", "c"], live.LinkLayer.Children.Cast<LinkFocusTarget>().Select(t => t.Link.NavigateUri!.Host[..1]));
        Assert.Equal(3, live.LinkLayer.StopCount);
    }

    [AvaloniaFact]
    public void Focus_is_reported_discarded_only_in_a_changed_block()
    {
        var live = Live("- [ ] a\n\nb\n\n- [ ] c");
        var toggles = live.RootPanel.GetLogicalDescendants().OfType<ToggleButton>().ToArray();

        Assert.False(Edit(live, "- [ ] a\n\nbe\n\n- [ ] c", toggles[1]).FocusDiscarded);
        Assert.False(Edit(live, "- [ ] a\n\nbee\n\n- [ ] c", toggles[0]).FocusDiscarded);
        Assert.True(Edit(live, "- [ ] a\n\nbee\n\n- [ ] cc", toggles[1]).FocusDiscarded);
    }

    [AvaloniaFact]
    public void A_limited_update_leaves_blocks_pending_that_Extend_renders_as_a_single_update_would()
    {
        const string text = "# A\n\n[link](https://example.com) one[^1]\n\n# A\n\ntwo\n\n[^1]: note";
        var whole = Live(text);
        var live = new LiveDocument(new AvaloniaRenderer { AreTaskListsInteractive = true }, _ => { });

        live.Update(Markdown.Parse(text, Pipeline), text, limit: 2);

        Assert.Equal(2, live.BlockCount);
        Assert.Equal(whole.BlockCount - 2, live.PendingCount);
        Assert.Equal(1, live.Extend(1));
        Assert.Equal(whole.BlockCount - 3, live.Extend(100));
        Assert.Equal(0, live.PendingCount);
        Assert.Equal(0, live.Extend(1));
        Assert.Equal(AllText(whole), AllText(live));
        Assert.Equal(whole.RootPanel.Children.Count, live.RootPanel.Children.Count);
        Assert.Equal(whole.LinkLayer.StopCount, live.LinkLayer.StopCount);
        Assert.Equal(whole.Renderer.HeadingEntries, live.Renderer.HeadingEntries);
        Assert.Equal(whole.Renderer.Anchors.Keys.Order(), live.Renderer.Anchors.Keys.Order());
    }

    [AvaloniaFact]
    public void An_update_first_renders_the_blocks_the_last_one_left_pending()
    {
        var live = new LiveDocument(new AvaloniaRenderer(), _ => { });
        live.Update(Markdown.Parse("a\n\nb\n\nc", Pipeline), "a\n\nb\n\nc", limit: 1);
        var first = live.RootPanel.Children[0];

        var result = Edit(live, "a\n\nb\n\ncd");

        // Compared with the whole last document: only the changed block is rendered again.
        Assert.Equal(2, result.ReusedBlocks);
        Assert.Same(first, live.RootPanel.Children[0]);
        Assert.Equal(0, live.PendingCount);
        Assert.Equal("a\nb\ncd", AllText(live));
    }

    [AvaloniaFact]
    public void A_limit_never_drops_the_blocks_kept_from_the_last_document()
    {
        var live = Live("a\n\nb\n\nc");

        var result = live.Update(Markdown.Parse("a\n\nb\n\nc\n\nd", Pipeline), "a\n\nb\n\nc\n\nd", limit: 1);

        // The blocks kept, and none rendered past them.
        Assert.InRange(result.ReusedBlocks, 2, 3);
        Assert.Equal(result.ReusedBlocks, live.BlockCount);
        Assert.Equal(4 - live.BlockCount, live.PendingCount);
    }
}
