// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

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

    private static MarkdownHyperlink FirstLink(LiveDocument live)
    {
        var text = Assert.IsAssignableFrom<TextBlock>(live.RootPanel.Children[0]);
        return Assert.IsType<MarkdownHyperlink>(Assert.Single(text.Inlines!, i => i is MarkdownHyperlink));
    }

    /// <summary>The document rendered in one update, as a direct set renders it.</summary>
    private static LiveDocument Direct(string text, MarkdownPipeline? pipeline = null)
    {
        var live = Live();
        live.Update(Markdown.Parse(text, pipeline ?? Pipeline));
        return live;
    }

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
        Assert.Equal(3, live.RootPanel.Children.Count);
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
    public void Block_of_another_type_with_the_same_span_is_re_rendered()
    {
        var live = Live();
        Update(live, "xyz");
        var paragraph = live.RootPanel.Children[0];

        var result = Update(live, "***");

        Assert.Equal(0, result.ReusedBlocks);
        Assert.Single(live.RootPanel.Children);
        Assert.NotSame(paragraph, live.RootPanel.Children[0]);
        Assert.IsNotType<TextBlock>(live.RootPanel.Children[0]);
    }

    [AvaloniaFact]
    public void Block_with_the_same_end_but_another_start_is_re_rendered()
    {
        var live = Live();
        Update(live, "a\n\n\nb");
        var second = live.RootPanel.Children[1];

        var result = Update(live, "a\n\nbb");

        Assert.Equal(1, result.ReusedBlocks);
        Assert.NotSame(second, live.RootPanel.Children[1]);
        live.SelectionLayer.SelectAll();
        Assert.Equal("a\nbb", live.SelectionLayer.GetSelectedText());
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
    public void Growing_definition_url_re_renders_the_paragraph_that_uses_it()
    {
        const string partial = "[t][ref]\n\n[ref]: https://exam";
        const string full = "[t][ref]\n\n[ref]: https://example.com";
        var live = Live();
        Update(live, partial);
        Assert.Equal("https://exam/", FirstLink(live).NavigateUri!.AbsoluteUri);

        var result = Update(live, full);

        Assert.Equal(0, result.ReusedBlocks);
        Assert.Equal("https://example.com/", FirstLink(live).NavigateUri!.AbsoluteUri);
        Assert.Equal(FirstLink(Direct(full)).NavigateUri, FirstLink(live).NavigateUri);
    }

    [AvaloniaFact]
    public void Growing_definition_title_re_renders_the_paragraph_that_uses_it()
    {
        // Markdig drops a definition whose title is not closed yet, so the title arrives whole, in one
        // update, after the label and URL are final.
        const string partial = "[t][ref]\n\n[ref]: /u";
        const string full = "[t][ref]\n\n[ref]: /u \"title\"";
        var live = Live();
        Update(live, partial);
        Assert.Null(FirstLink(live).Title);

        var result = Update(live, full);

        Assert.Equal(0, result.ReusedBlocks);
        Assert.Equal("title", FirstLink(live).Title);
        Assert.Equal(FirstLink(Direct(full)).Title, FirstLink(live).Title);
        Assert.Equal("/u", FirstLink(live).NavigateUri!.OriginalString);
    }

    [AvaloniaFact]
    public void Growing_abbreviation_re_renders_the_paragraph_that_uses_it()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().UseAbbreviations().Build();
        const string partial = "HTML is\n\n*[HTML]: Hyper";
        const string full = "HTML is\n\n*[HTML]: Hyper Text";
        static TextBlock Abbreviation(LiveDocument live)
        {
            var paragraph = Assert.IsAssignableFrom<TextBlock>(live.RootPanel.Children[0]);
            var container = Assert.IsType<InlineUIContainer>(paragraph.Inlines![0]);
            var abbreviation = Assert.IsType<TextBlock>(container.Child);
            Assert.Contains("markdown-abbr", abbreviation.Classes);
            return abbreviation;
        }
        var live = Live();
        live.Update(Markdown.Parse(partial, pipeline));
        Assert.Equal("Hyper", ToolTip.GetTip(Abbreviation(live)));

        var result = live.Update(Markdown.Parse(full, pipeline));

        Assert.Equal(0, result.ReusedBlocks);
        Assert.Equal("Hyper Text", ToolTip.GetTip(Abbreviation(live)));
        Assert.Equal(ToolTip.GetTip(Abbreviation(Direct(full, pipeline))), ToolTip.GetTip(Abbreviation(live)));
        Assert.Equal("HTML", Abbreviation(live).Text);
    }

    [AvaloniaFact]
    public void Unchanged_abbreviation_keeps_the_paragraph_that_uses_it()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().UseAbbreviations().Build();
        var live = Live();
        live.Update(Markdown.Parse("HTML is\n\n*[HTML]: Hyper Text\n\nmore", pipeline));
        var paragraph = live.RootPanel.Children[0];

        var result = live.Update(Markdown.Parse("HTML is\n\n*[HTML]: Hyper Text\n\nmore text", pipeline));

        Assert.Equal(1, result.ReusedBlocks);
        Assert.Same(paragraph, live.RootPanel.Children[0]);
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
        Assert.Equal(["b"], rebuilt.Renderer.HeadingEntries.Select(h => h.Slug));
        Assert.Equal(1, rebuilt.Renderer.AnchorCount);

        // Nothing heading-related happens in the tail.
        var unchanged = Live();
        Update(unchanged, "a\n\nb");
        Assert.False(Update(unchanged, "a\n\nb more").HeadingsChanged);
        var closedHeading = Live();
        Update(closedHeading, "a\n\n# B");
        Assert.False(Update(closedHeading, "a\n\n# B\n\nc").HeadingsChanged);
    }

    [AvaloniaFact]
    public void HeadingsChanged_when_the_discarded_tail_loses_its_heading()
    {
        var live = Live();
        Update(live, "a\n\nb\n-");
        Assert.IsType<HeadingBlock>(Doc("a\n\nb\n-")[1]);
        Assert.Equal(1, live.Renderer.HeadingCount);

        var result = Update(live, "a\n\nb\n- c");

        Assert.IsType<ParagraphBlock>(live.Document![1]);
        Assert.IsType<ListBlock>(live.Document[2]);
        Assert.True(result.HeadingsChanged);
        Assert.Equal(0, live.Renderer.HeadingCount);
    }

    [AvaloniaFact]
    public void A_re_rendered_heading_takes_the_next_free_slug()
    {
        var live = Live();
        Update(live, "# A\n\nx");
        var kept = live.Renderer.Anchors["a"];

        Update(live, "# A\n\nx\n\n# A");

        Assert.Equal(["a", "a-1"], live.Renderer.HeadingEntries.Select(h => h.Slug));
        Assert.Same(kept, live.Renderer.Anchors["a"]);
        Assert.Same(live.RootPanel.Children[0], live.Renderer.Anchors["a"]);
        Assert.Same(live.RootPanel.Children[2], live.Renderer.Anchors["a-1"]);
        Assert.Equal(2, live.Renderer.AnchorCount);
    }

    [AvaloniaFact]
    public void A_discarded_heading_leaves_no_anchor_behind()
    {
        var live = Live();
        Update(live, "> # B");
        Assert.True(live.Renderer.Anchors.ContainsKey("b"));

        Update(live, "> # Bc");

        Assert.False(live.Renderer.Anchors.ContainsKey("b"));
        Assert.True(live.Renderer.Anchors.ContainsKey("bc"));
        Assert.Equal(1, live.Renderer.AnchorCount);
        Assert.Equal(["bc"], live.Renderer.HeadingEntries.Select(h => h.Slug));
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

            first.Focus();
            Assert.True(first.IsFocused);
            var kept = live.Update(Doc("[a](x)\n\n[b](y) z"), first);
            Assert.False(kept.FocusDiscarded);
            Assert.Equal(1, kept.ReusedBlocks);

            var secondAgain = Assert.IsType<LinkFocusTarget>(live.LinkLayer.GetActiveStops()[1]);
            secondAgain.Focus();
            Assert.True(secondAgain.IsFocused);
            var discarded = live.Update(Doc("[a](x)\n\n[b](y) z w"), secondAgain);
            Assert.True(discarded.FocusDiscarded);
            Assert.NotSame(second, secondAgain);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_on_a_task_toggle_is_reported_by_the_block_that_holds_it()
    {
        var live = Live();
        Update(live, "- [ ] a");
        var inTail = live.RootPanel.GetVisualDescendants().OfType<ToggleButton>().Single();

        var discarded = live.Update(Doc("- [ ] a b"), inTail);
        Assert.True(discarded.FocusDiscarded);
        Assert.Equal(0, discarded.ReusedBlocks);

        var kept = Live();
        Update(kept, "- [ ] a\n\nb");
        var inKept = kept.RootPanel.GetVisualDescendants().OfType<ToggleButton>().Single();

        var result = kept.Update(Doc("- [ ] a\n\nb more"), inKept);
        Assert.False(result.FocusDiscarded);
        Assert.Equal(1, result.ReusedBlocks);
        Assert.Same(inKept, kept.RootPanel.GetVisualDescendants().OfType<ToggleButton>().Single());
    }

    [AvaloniaFact]
    public void Blocks_that_merge_leave_fewer_records()
    {
        var live = Live();
        Update(live, "- a\n\nb");
        Assert.Equal(2, live.BlockCount);

        var result = Update(live, "- a\n\n  b");

        Assert.Single(live.Document!);
        Assert.Equal(0, result.ReusedBlocks);
        Assert.Equal(1, live.BlockCount);
        Assert.Single(live.RootPanel.Children);
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
    public void Closed_table_before_the_tail_is_reused()
    {
        // Markdig keeps a pipe table open after the blank line that ends it (see
        // MarkdigStreamingFactsTests); it still cannot change once a block follows it.
        var live = Live();
        Update(live, "| a |\n|---|\n| 1 |\n\np");
        var table = live.RootPanel.Children[0];

        var result = Update(live, "| a |\n|---|\n| 1 |\n\np q");

        Assert.Equal(1, result.ReusedBlocks);
        Assert.Equal(2, live.RootPanel.Children.Count);
        Assert.Same(table, live.RootPanel.Children[0]);
    }

    [AvaloniaFact]
    public void Open_table_at_the_end_is_re_rendered()
    {
        var live = Live();
        Update(live, "| a |\n|---|\n| 1 |");
        var table = live.RootPanel.Children[0];

        var result = Update(live, "| a |\n|---|\n| 1 |\n| 2 |");

        Assert.Equal(0, result.ReusedBlocks);
        Assert.Single(live.RootPanel.Children);
        Assert.NotSame(table, live.RootPanel.Children[0]);
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
