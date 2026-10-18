// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using Markdig;
using Markdig.Syntax;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Streaming;

public class LiveDocumentTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();

    private static LiveDocument Live() =>
        new(new AvaloniaRenderer { AreTaskListsInteractive = true }, _ => { });

    private static MarkdownDocument Doc(string text) => Markdown.Parse(text, Pipeline);

    private static UpdateResult Update(LiveDocument live, string text) => live.Update(Doc(text));

    [AvaloniaFact]
    public void First_update_renders_every_block()
    {
        var live = Live();

        var result = Update(live, "a\n\nb");

        Assert.Equal(0, result.ReusedBlocks);
        Assert.Equal(2, live.RootPanel.Children.Count);
        Assert.Equal(2, live.BlockCount);
    }

    [AvaloniaFact]
    public void Closed_block_keeps_its_control_across_updates()
    {
        var live = Live();
        Update(live, "a\n\nb");
        var first = live.RootPanel.Children[0];
        var second = live.RootPanel.Children[1];

        var result = Update(live, "a\n\nb more");

        Assert.Equal(1, result.ReusedBlocks);
        Assert.Equal(2, live.RootPanel.Children.Count);
        Assert.Same(first, live.RootPanel.Children[0]);
        Assert.NotSame(second, live.RootPanel.Children[1]);
    }

    [AvaloniaFact]
    public void Open_block_is_re_rendered_until_it_closes()
    {
        var live = Live();
        Update(live, "a\n\nb");

        var second = Update(live, "a\n\nb\n\nc");
        var third = Update(live, "a\n\nb\n\nc\n\nd");

        Assert.Equal(1, second.ReusedBlocks);
        Assert.Equal(2, third.ReusedBlocks);
        Assert.Equal(4, live.RootPanel.Children.Count);
    }

    [AvaloniaFact]
    public void Open_fence_is_rebuilt_then_kept()
    {
        var live = Live();
        Update(live, "```\nx");

        var second = Update(live, "```\nx\ny");
        Assert.Equal(0, second.ReusedBlocks);
        Assert.Single(live.RootPanel.Children);

        var third = Update(live, "```\nx\ny\n```\n\np");
        Assert.Equal(0, third.ReusedBlocks);
        Assert.Equal(2, live.RootPanel.Children.Count);
        var fence = live.RootPanel.Children[0];

        var fourth = Update(live, "```\nx\ny\n```\n\np q");
        Assert.Equal(1, fourth.ReusedBlocks);
        Assert.Same(fence, live.RootPanel.Children[0]);
    }

    [AvaloniaFact]
    public void Open_list_is_rebuilt_whole()
    {
        var live = Live();
        Update(live, "- a\n- b");
        Assert.Single(live.RootPanel.Children);

        var result = Update(live, "- a\n- b\n- c");

        Assert.Equal(0, result.ReusedBlocks);
        Assert.Single(live.RootPanel.Children);
    }

    [AvaloniaFact]
    public void Selection_entries_follow_the_tail()
    {
        var live = Live();
        Update(live, "a\n\nb");

        Update(live, "a\n\nbc");

        live.SelectionLayer.SelectAll();
        Assert.Equal("a\nbc", live.SelectionLayer.GetSelectedText());
        Assert.Equal(2, live.SelectionLayer.EntryCount);
    }

    [AvaloniaFact]
    public void Link_stops_follow_the_tail()
    {
        var live = Live();

        Update(live, "[a](x)\n\n[b](y");
        Assert.Equal(1, live.LinkLayer.StopCount);

        Update(live, "[a](x)\n\n[b](y)");
        Assert.Equal(2, live.LinkLayer.StopCount);
        Assert.Equal(2, live.LinkLayer.TargetCount);
    }

    [AvaloniaFact]
    public void Late_link_reference_definition_re_renders_everything()
    {
        var live = Live();
        Update(live, "[a]\n\n");

        var result = Update(live, "[a]\n\n[a]: /u");

        Assert.Equal(0, result.ReusedBlocks);
        var text = Assert.IsAssignableFrom<TextBlock>(live.RootPanel.Children[0]);
        Assert.Contains(text.Inlines!, i => i is MarkdownHyperlink);
    }

    [AvaloniaFact]
    public void Late_footnote_definition_re_renders_everything()
    {
        var live = Live();
        Update(live, "x[^1]\n\n");

        var result = Update(live, "x[^1]\n\n[^1]: n");

        Assert.Equal(0, result.ReusedBlocks);
    }

    [AvaloniaFact]
    public void Headings_keep_slugs_and_anchors_across_updates()
    {
        var live = Live();
        Update(live, "# A\n\n# A\n\ntext");
        Assert.Equal(["a", "a-1"], live.Renderer.HeadingEntries.Select(h => h.Slug));
        var anchor = live.Renderer.Anchors["a-1"];

        var result = Update(live, "# A\n\n# A\n\ntext more");

        Assert.Equal(["a", "a-1"], live.Renderer.HeadingEntries.Select(h => h.Slug));
        Assert.Same(anchor, live.Renderer.Anchors["a-1"]);
        Assert.False(result.HeadingsChanged);
    }

    [AvaloniaFact]
    public void HeadingsChanged_when_a_heading_is_added_or_discarded()
    {
        // A closed heading keeps its entry, so only the new one counts as a change.
        var added = Live();
        Update(added, "a\n\n# B");
        Assert.True(Update(added, "a\n\n# B\n\n# C").HeadingsChanged);
        Assert.Equal(["B", "C"], added.Renderer.HeadingEntries.Select(h => h.Text));

        // A heading in an open block is discarded and rendered again.
        var rebuilt = Live();
        Update(rebuilt, "> # B");
        Assert.Equal(1, rebuilt.Renderer.HeadingCount);
        Assert.True(Update(rebuilt, "> # B\n> more").HeadingsChanged);
        Assert.Equal(1, rebuilt.Renderer.HeadingCount);

        // Nothing heading-related happens in the tail.
        var unchanged = Live();
        Update(unchanged, "a\n\nb");
        Assert.False(Update(unchanged, "a\n\nb more").HeadingsChanged);
        var closedHeading = Live();
        Update(closedHeading, "a\n\n# B");
        Assert.False(Update(closedHeading, "a\n\n# B\n\nc").HeadingsChanged);
    }

    [AvaloniaFact]
    public void Focus_in_a_discarded_block_is_reported()
    {
        var live = Live();
        var host = new Grid();
        host.Children.Add(live.RootPanel);
        host.Children.Add(live.LinkLayer);
        var window = new Window { Content = host };
        window.Show();
        try
        {
            Update(live, "[a](x)\n\n[b](y)");
            var stops = live.LinkLayer.GetActiveStops();
            Assert.Equal(2, stops.Count);
            var first = Assert.IsType<LinkFocusTarget>(stops[0]);
            var second = Assert.IsType<LinkFocusTarget>(stops[1]);
            second.Focus();
            Assert.True(second.IsFocused);

            var kept = live.Update(Doc("[a](x)\n\n[b](y) z"), first);
            Assert.False(kept.FocusDiscarded);

            var secondAgain = Assert.IsType<LinkFocusTarget>(live.LinkLayer.GetActiveStops()[1]);
            var discarded = live.Update(Doc("[a](x)\n\n[b](y) z w"), secondAgain);
            Assert.True(discarded.FocusDiscarded);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Link_reference_definition_group_is_reused()
    {
        var live = Live();
        Update(live, "[a]: /u\n\np");

        var second = Update(live, "[a]: /u\n\np\n\nq");
        Assert.Equal(1, second.ReusedBlocks);
        Assert.Equal(2, live.RootPanel.Children.Count);

        var third = Update(live, "[a]: /u\n\np\n\nq r");
        Assert.Equal(2, third.ReusedBlocks);
    }

    [AvaloniaFact]
    public void Footnote_group_is_re_rendered_each_update()
    {
        var live = Live();
        Update(live, "x[^1]\n\n[^1]: n\n\np\n\n");
        var footnotes = live.RootPanel.Children[^1];

        var result = Update(live, "x[^1]\n\n[^1]: n\n\np\n\nq");

        Assert.Equal(3, result.ReusedBlocks);
        Assert.NotSame(footnotes, live.RootPanel.Children[^1]);
    }
}
