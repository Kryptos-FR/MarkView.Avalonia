// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;

using Markdig;

using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Accessibility;

/// <summary>
/// What a screen reader sees of the header of a foldable block (a foldable alert, a details element, a
/// thinking block): a button, named by the block's title, that reports and changes whether the block is expanded.
/// </summary>
public class FoldStateAutomationTests : RenderTestBase
{
    public static TheoryData<string, string, string, bool> Blocks => new()
    {
        { "> [!TIP]- Read me\n> Body", "markdown-alert-header", "Read me", false },
        { "> [!NOTE]+\n> Body", "markdown-alert-header", "NOTE", true },
        { "<details>\n<summary>More</summary>\n\nBody\n\n</details>", "markdown-details-summary", "More", false },
        { "<details open>\n\nBody\n\n</details>", "markdown-details-summary", "Details", true },
        { "<think>\nBody\n</think>", "markdown-thinking-header", "Thoughts", false },
    };

    private static MarkdownPipeline Pipeline =>
        new MarkdownPipelineBuilder().UseExtendedAlertBlocks().UseHtmlDetails().UseThinkingBlocks().Build();

    private static TextBlock Header(Control root, string headerClass) =>
        root.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains(headerClass));

    private static CollapsibleAlert Fold(Control root) =>
        Assert.IsType<CollapsibleAlert>(CollapsibleAlert.Get(root.GetLogicalDescendants().OfType<Border>().First()));

    [AvaloniaTheory]
    [MemberData(nameof(Blocks))]
    public void Header_is_a_button_named_by_the_title_that_reports_the_fold_state(string markdown, string headerClass, string name, bool expanded)
    {
        var root = Render(markdown, Pipeline);

        var peer = ControlAutomationPeer.CreatePeerForElement(Header(root, headerClass));
        var expandCollapse = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer);
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
        Assert.Equal(name, peer.GetName());
        Assert.True(peer.IsContentElement());
        Assert.True(peer.IsControlElement());
        Assert.Empty(peer.GetChildren());
        Assert.False(expandCollapse.ShowsMenu);
        Assert.Equal(expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed, expandCollapse.ExpandCollapseState);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Blocks))]
    public void Expanding_and_collapsing_from_a_screen_reader_folds_the_block_and_reports_each_change(
        string markdown, string headerClass, string name, bool expanded)
    {
        _ = name;
        var root = Render(markdown, Pipeline);
        var peer = ControlAutomationPeer.CreatePeerForElement(Header(root, headerClass));
        var expandCollapse = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer);
        var changes = new List<(object? Old, object? New)>();
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
                changes.Add((e.OldValue, e.NewValue));
        };

        if (expanded)
        {
            expandCollapse.Collapse();
            expandCollapse.Collapse();
            Assert.False(Fold(root).IsExpanded);
            expandCollapse.Expand();
            Assert.True(Fold(root).IsExpanded);
            Assert.Equal(
                [(ExpandCollapseState.Expanded, ExpandCollapseState.Collapsed), (ExpandCollapseState.Collapsed, ExpandCollapseState.Expanded)],
                changes);
        }
        else
        {
            expandCollapse.Expand();
            expandCollapse.Expand();
            Assert.True(Fold(root).IsExpanded);
            expandCollapse.Collapse();
            Assert.False(Fold(root).IsExpanded);
            Assert.Equal(
                [(ExpandCollapseState.Collapsed, ExpandCollapseState.Expanded), (ExpandCollapseState.Expanded, ExpandCollapseState.Collapsed)],
                changes);
        }
    }

    [AvaloniaFact]
    public void A_click_or_a_search_that_unfolds_the_block_is_reported_too()
    {
        var root = Render("> [!TIP]- Read me\n> Body", Pipeline);
        var peer = ControlAutomationPeer.CreatePeerForElement(Header(root, "markdown-alert-header"));
        var states = new List<object?>();
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
                states.Add(e.NewValue);
        };

        Fold(root).Toggle();
        Fold(root).IsExpanded = true;

        Assert.Equal([ExpandCollapseState.Expanded], states);
    }

    [AvaloniaFact]
    public void An_explicit_automation_name_wins_over_the_title()
    {
        var header = Header(Render("<details>\n<summary>More</summary>\n\nBody\n\n</details>", Pipeline), "markdown-details-summary");
        AutomationProperties.SetName(header, "Show more");

        Assert.Equal("Show more", ControlAutomationPeer.CreatePeerForElement(header).GetName());
    }

    [AvaloniaFact]
    public void A_fixed_alert_header_stays_text()
    {
        var root = Render("> [!TIP] Read me\n> Body", Pipeline);

        var peer = ControlAutomationPeer.CreatePeerForElement(Header(root, "markdown-alert-header"));
        Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
        Assert.Null(peer.GetProvider<IExpandCollapseProvider>());
    }
}
