// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Parsing;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// A language model's reasoning (<see cref="MarkdownExtensions.UseThinkingBlocks"/>): how the block looks and folds
/// in a document, and how it follows the reasoning during a streaming session.
/// </summary>
public class MarkdownViewerThinkingBlockTests
{
    private const string Output = "<think>\nThe user wants **two** things.\n</think>\n\nHere they are.";

    /// <summary>A clock that only moves when told to.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }

    private static MarkdownViewer Viewer(string markdown, ThinkingBlockOptions? options = null, ManualClock? clock = null)
    {
        var viewer = new MarkdownViewer().UseThinkingBlocks(options);
        if (clock is not null)
            viewer.TimeProvider = clock;
        viewer.Markdown = markdown;
        return viewer;
    }

    private static Border? FindBlock(MarkdownViewer viewer) =>
        viewer.GetLogicalDescendants().OfType<Border>().SingleOrDefault(b => b.Classes.Contains("markdown-thinking"));

    private static Border Block(MarkdownViewer viewer) => Assert.IsType<Border>(FindBlock(viewer));

    private static TextBlock Header(MarkdownViewer viewer) =>
        viewer.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-thinking-header"));

    private static string Title(MarkdownViewer viewer) => ((Run)Header(viewer).Inlines!.Last()).Text!;

    private static CollapsibleAlert Fold(MarkdownViewer viewer) => CollapsibleAlert.Get(Block(viewer))!;

    private static Border Body(MarkdownViewer viewer) =>
        viewer.GetLogicalDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-thinking-content"));

    [AvaloniaFact]
    public void The_reasoning_is_markdown_folded_under_a_header_before_the_answer()
    {
        var viewer = Viewer(Output);

        var root = Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);
        var block = Assert.IsType<Border>(root.Children[0]);
        Assert.Contains("markdown-thinking", block.Classes);
        Assert.DoesNotContain(":thinking", block.Classes);
        Assert.DoesNotContain(":expanded", block.Classes);
        Assert.Equal("Thoughts", Title(viewer));
        Assert.Contains("markdown-thinking-chevron", Assert.IsType<InlineUIContainer>(Header(viewer).Inlines![0]).Child!.Classes);
        Assert.False(Body(viewer).IsVisible);

        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.IsType<StackPanel>(Body(viewer).Child).Children.Single());
        Assert.Contains(paragraph.Inlines!, inline => inline is Bold);
        Assert.IsType<MarkdownSelectableTextBlock>(root.Children[1]);
    }

    [AvaloniaFact]
    public void Clicking_the_header_shows_the_reasoning_which_selection_then_includes()
    {
        var viewer = Viewer(Output);
        viewer.SelectAll();
        Assert.Equal("Here they are.", viewer.GetSelectedText());

        Fold(viewer).Toggle();

        Assert.Contains(":expanded", Block(viewer).Classes);
        Assert.True(Body(viewer).IsVisible);
        viewer.SelectAll();
        Assert.Equal("The user wants two things.\nHere they are.", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Finding_a_match_in_folded_reasoning_expands_it()
    {
        var viewer = Viewer(Output);

        Assert.Equal(1, viewer.Find("wants"));
        Assert.True(viewer.FindNext());

        Assert.True(Fold(viewer).IsExpanded);
    }

    [AvaloniaFact]
    public void Screen_readers_see_a_group_named_by_the_title()
    {
        var viewer = Viewer(Output);

        var peer = ControlAutomationPeer.CreatePeerForElement(Block(viewer));
        Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
        Assert.Equal("Thoughts", AutomationProperties.GetName(Block(viewer)));
        Assert.True(Header(viewer).Focusable);
    }

    [AvaloniaFact]
    public void An_empty_block_renders_nothing()
    {
        var viewer = Viewer("<think>\n\n</think>\n\nAnswer");

        Assert.Null(FindBlock(viewer));
        var root = Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);
        Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children));
    }

    [AvaloniaFact]
    public void Reasoning_cut_off_before_its_closing_tag_is_shown_as_complete_outside_a_session()
    {
        var viewer = Viewer("<think>\nHalf a tho");

        Assert.Equal("Thoughts", Title(viewer));
        Assert.DoesNotContain(":thinking", Block(viewer).Classes);
        Assert.False(Fold(viewer).IsExpanded);
    }

    [AvaloniaFact]
    public void Titles_and_the_expanded_state_can_be_chosen()
    {
        var viewer = Viewer(Output, new ThinkingBlockOptions { Title = "Raisonnement", ExpandWhenDone = true });

        Assert.Equal("Raisonnement", Title(viewer));
        Assert.True(Fold(viewer).IsExpanded);
    }

    [AvaloniaFact]
    public void A_bare_renderer_takes_the_options_from_the_pipeline()
    {
        var pipeline = new MarkdownPipelineBuilder().UseThinkingBlocks(new ThinkingBlockOptions { Title = "Notes" }).Build();
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);

        renderer.Render(Markdown.Parse(Output, pipeline));

        var block = Assert.IsType<Border>(renderer.RootPanel.Children[0]);
        Assert.Equal("Notes", AutomationProperties.GetName(block));
    }

    [AvaloniaFact]
    public void A_renderer_without_a_thinking_renderer_is_left_as_it_is()
    {
        var pipeline = new MarkdownPipelineBuilder().UseThinkingBlocks().Build();
        var renderer = new AvaloniaRenderer();
        renderer.ObjectRenderers.RemoveAll(r => r is ThinkingBlockRenderer);

        pipeline.Setup(renderer);

        Assert.DoesNotContain(renderer.ObjectRenderers, r => r is ThinkingBlockRenderer);
    }

    [AvaloniaFact]
    public void The_viewer_shortcut_keeps_the_default_pipeline()
    {
        var viewer = new MarkdownViewer().UseThinkingBlocks();

        Assert.NotNull(viewer.Pipeline!.Extensions.Find<ThinkingBlockExtension>());
        Assert.NotNull(viewer.Pipeline.Extensions.Find<Markdig.Extensions.Tables.PipeTableExtension>());
    }

    // ── Streaming ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void While_the_model_thinks_the_block_is_expanded_and_then_folds_away_with_the_time_it_took()
    {
        var clock = new ManualClock();
        var viewer = Viewer("", clock: clock);
        var session = viewer.BeginStreaming();

        session.Append("<think>\nThe user ");
        session.ApplyPendingUpdate();
        Assert.Equal("Thinking…", Title(viewer));
        Assert.Contains(":thinking", Block(viewer).Classes);
        Assert.True(Fold(viewer).IsExpanded);

        clock.Advance(TimeSpan.FromSeconds(7));
        session.Append("wants two things.\n");
        session.ApplyPendingUpdate();
        Assert.Equal("Thinking…", Title(viewer));

        clock.Advance(TimeSpan.FromSeconds(5));
        session.Append("</think>\n\nHere");
        session.ApplyPendingUpdate();
        Assert.Equal("Thought for 12 seconds", Title(viewer));
        Assert.DoesNotContain(":thinking", Block(viewer).Classes);
        Assert.False(Fold(viewer).IsExpanded);

        clock.Advance(TimeSpan.FromSeconds(30));
        session.Append(" they are.");
        session.Complete();
        Assert.Equal("Thought for 12 seconds", Title(viewer));
    }

    [AvaloniaFact]
    public void Even_an_empty_block_shows_that_the_model_is_thinking()
    {
        var viewer = Viewer("");
        var session = viewer.BeginStreaming();

        session.Append("<think>\n");
        session.ApplyPendingUpdate();

        Assert.Equal("Thinking…", Title(viewer));
    }

    [AvaloniaFact]
    public void Reasoning_whose_session_ends_before_the_closing_tag_shows_as_complete()
    {
        var clock = new ManualClock();
        var viewer = Viewer("", clock: clock);
        var session = viewer.BeginStreaming();
        session.Append("<think>\nHalf a tho");
        session.ApplyPendingUpdate();

        clock.Advance(TimeSpan.FromSeconds(3));
        session.Complete();

        Assert.Equal("Thought for 3 seconds", Title(viewer));
        Assert.DoesNotContain(":thinking", Block(viewer).Classes);
    }

    [AvaloniaFact]
    public void Without_a_duration_title_the_complete_block_shows_its_title()
    {
        var viewer = Viewer("", new ThinkingBlockOptions { DurationTitle = null, ThinkingTitle = "Réflexion…" });
        var session = viewer.BeginStreaming();
        session.Append("<think>\nIdea");
        session.ApplyPendingUpdate();
        Assert.Equal("Réflexion…", Title(viewer));

        session.Append("\n</think>\n\nAnswer");
        session.ApplyPendingUpdate();

        Assert.Equal("Thoughts", Title(viewer));
    }

    [AvaloniaFact]
    public void A_block_the_reader_collapsed_while_the_model_thinks_stays_collapsed()
    {
        var viewer = Viewer("", new ThinkingBlockOptions { ExpandWhenDone = true });
        var session = viewer.BeginStreaming();
        session.Append("<think>\nIdea");
        session.ApplyPendingUpdate();
        Fold(viewer).Toggle();

        session.Append(" grows");
        session.ApplyPendingUpdate();
        Assert.False(Fold(viewer).IsExpanded);

        session.Append("\n</think>\n\nAnswer");
        session.ApplyPendingUpdate();
        Assert.False(Fold(viewer).IsExpanded);
    }

    [AvaloniaFact]
    public void A_block_the_reader_expanded_again_stays_expanded_once_the_reasoning_is_complete()
    {
        var viewer = Viewer("");
        var session = viewer.BeginStreaming();
        session.Append("<think>\nIdea");
        session.ApplyPendingUpdate();
        Fold(viewer).Toggle();
        Fold(viewer).Toggle();

        session.Append("\n</think>\n\nAnswer");
        session.ApplyPendingUpdate();

        Assert.True(Fold(viewer).IsExpanded);
    }

    [AvaloniaFact]
    public void A_block_can_stay_collapsed_while_the_model_thinks()
    {
        var viewer = Viewer("", new ThinkingBlockOptions { ExpandWhileThinking = false });
        var session = viewer.BeginStreaming();

        session.Append("<think>\nIdea");
        session.ApplyPendingUpdate();

        Assert.False(Fold(viewer).IsExpanded);
    }

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Reasoning_streamed_in_chunks_renders_like_reasoning_streamed_at_once(int seed)
    {
        const string text = """
            <think>
            First, the [session](https://example.com/session) API:

            - `BeginStreaming()` returns a session.
            - `Append()` adds text.

            ## Edge cases

            ```xml
            </think>
            ```

            > A quote with a [link](https://example.com/quote).

            Then [the edge cases](#edge-cases) again, and **more
            """;
        var options = new ThinkingBlockOptions { ExpandWhileThinking = true };
        var random = new Random(seed);
        var actual = Viewer("", options);
        var session = actual.BeginStreaming();
        for (var position = 0; position < text.Length;)
        {
            var length = Math.Min(random.Next(1, 40), text.Length - position);
            session.Append(text.AsSpan(position, length));
            session.ApplyPendingUpdate();
            position += length;

            var expected = Viewer("", options);
            var once = expected.BeginStreaming();
            once.Append(text.AsSpan(0, position));
            once.ApplyPendingUpdate();

            Assert.Equal(AllText(expected), AllText(actual));
            Assert.Equal(expected.AnchorIds.Order(), actual.AnchorIds.Order());
            Assert.Equal(LinkTargets(expected), LinkTargets(actual));
            Assert.Equal(Body(expected).Child is StackPanel e ? e.Children.Count : -1, Body(actual).Child is StackPanel a ? a.Children.Count : -1);
        }

        static string AllText(MarkdownViewer viewer)
        {
            viewer.SelectAll();
            return viewer.GetSelectedText();
        }

        static string[] LinkTargets(MarkdownViewer viewer) =>
            [.. Assert.IsType<LinkFocusLayer>(Assert.IsType<Grid>(viewer.Content).Children[2]).Children
                .OfType<LinkFocusTarget>().Select(t => t.AccessibleName)];
    }

    [AvaloniaFact]
    public void Text_after_the_reasoning_in_the_same_update_renders_after_it()
    {
        var viewer = Viewer("");
        var session = viewer.BeginStreaming();
        session.Append("<think>\nIdea\n\nMore");
        session.ApplyPendingUpdate();

        session.Append(" ideas\n</think>\n\nAnswer");
        session.ApplyPendingUpdate();

        var root = Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);
        Assert.Equal(2, root.Children.Count);
        Assert.Same(Block(viewer), root.Children[0]);
        Assert.Equal(2, Assert.IsType<StackPanel>(Body(viewer).Child).Children.Count);
        viewer.SelectAll();
        Assert.Equal("Answer", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Paragraphs_streamed_into_the_reasoning_keep_the_controls_of_those_before_them()
    {
        var viewer = Viewer("");
        var session = viewer.BeginStreaming();
        session.Append("<think>\nFirst\n\nSecond");
        session.ApplyPendingUpdate();
        var block = Block(viewer);
        var first = Assert.IsType<StackPanel>(Body(viewer).Child).Children[0];

        session.Append(" grows\n\nThird");
        session.ApplyPendingUpdate();

        Assert.Same(block, Block(viewer));
        var children = Assert.IsType<StackPanel>(Body(viewer).Child).Children;
        Assert.Equal(3, children.Count);
        Assert.Same(first, children[0]);
    }

    [AvaloniaFact]
    public void A_foldable_heading_in_the_reasoning_folds_what_streams_after_it()
    {
        var viewer = Viewer("");
        viewer.AreHeadingsFoldable = true;
        var session = viewer.BeginStreaming();
        session.Append("<think>\n## Plan\n\nStep one\n\n");
        session.ApplyPendingUpdate();
        var heading = viewer.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>().Single(tb => tb.Classes.Contains("markdown-h2"));
        HeadingFold.Get(heading)!.Toggle();

        session.Append("Step two");
        session.ApplyPendingUpdate();

        var children = Assert.IsType<StackPanel>(Body(viewer).Child).Children;
        Assert.Equal(3, children.Count);
        Assert.False(children[2].IsVisible);
    }

    [AvaloniaFact]
    public void Focus_on_a_link_in_the_reasoning_stays_while_later_children_grow_and_returns_to_the_viewer_when_its_child_is_discarded()
    {
        var viewer = Viewer("");
        viewer.Template = ScrollingViewerTemplate.Create();
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        try
        {
            var session = viewer.BeginStreaming();
            session.Append("<think>\n[a](x)\n\n- [b](y)\n- c");
            session.ApplyPendingUpdate();
            window.UpdateLayout();
            var targets = viewer.GetVisualDescendants().OfType<LinkFocusTarget>().ToArray();
            Assert.Equal(2, targets.Length);

            Assert.True(targets[0].Focus());
            session.Append(" tail");
            session.ApplyPendingUpdate();
            Assert.True(targets[0].IsFocused);

            window.UpdateLayout();
            var listLink = viewer.GetVisualDescendants().OfType<LinkFocusTarget>().Single(t => t.AccessibleName == "b");
            Assert.True(listLink.Focus());
            session.Append(" more");
            session.ApplyPendingUpdate();
            Assert.True(viewer.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Incomplete_syntax_in_the_reasoning_is_healed()
    {
        var viewer = Viewer("");
        var session = viewer.BeginStreaming();

        session.Append("<think>\nSome **bold");
        session.ApplyPendingUpdate();
        Dispatcher.UIThread.RunJobs();

        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.IsType<StackPanel>(Body(viewer).Child).Children.Single());
        Assert.Contains(paragraph.Inlines!, inline => inline is Bold);
    }
}
