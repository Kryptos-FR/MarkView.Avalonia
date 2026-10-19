// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Markdig;
using Xunit;

namespace MarkView.Avalonia.Tests.Accessibility;

/// <summary>
/// The header of a foldable alert, a <c>&lt;details&gt;</c> element or a thinking block, as a screen reader
/// sees it: a button named by the block's title that expands and collapses the block.
/// </summary>
public class FoldHeaderAutomationTests : RenderTestBase
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseExtendedAlertBlocks().UseHtmlDetails().UseThinkingBlocks().Build();

    private static TextBlock Header(StackPanel root, string headerClass) =>
        root.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains(headerClass));

    [AvaloniaTheory]
    [InlineData("> [!TIP]+ Read *me*\n> Body", "markdown-alert-header", "Read me", true)]
    [InlineData("> [!NOTE]-\n> Body", "markdown-alert-header", "NOTE", false)]
    [InlineData("<details open>\n<summary>More</summary>\n\nBody\n\n</details>", "markdown-details-summary", "More", true)]
    [InlineData("<details>\n\nBody\n\n</details>", "markdown-details-summary", "Details", false)]
    [InlineData("<think>\nBody\n</think>", "markdown-thinking-header", "Thoughts", false)]
    public void The_header_is_a_button_named_by_the_title_that_expands_and_collapses(string markdown, string headerClass, string name, bool expanded)
    {
        var root = Render(markdown, Pipeline);
        var border = Assert.IsType<Border>(Assert.Single(root.Children));
        var peer = ControlAutomationPeer.CreatePeerForElement(Header(root, headerClass));
        var expandCollapse = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer);

        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
        Assert.Equal(name, peer.GetName());
        Assert.True(peer.IsContentElement());
        Assert.True(peer.IsControlElement());
        Assert.False(expandCollapse.ShowsMenu);
        Assert.Equal(expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed, expandCollapse.ExpandCollapseState);

        var changes = new List<(object? Old, object? New)>();
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
                changes.Add((e.OldValue, e.NewValue));
        };

        expandCollapse.Expand();
        expandCollapse.Expand();
        Assert.Equal(ExpandCollapseState.Expanded, expandCollapse.ExpandCollapseState);
        Assert.Contains(":expanded", border.Classes);

        expandCollapse.Collapse();
        expandCollapse.Collapse();
        Assert.Equal(ExpandCollapseState.Collapsed, expandCollapse.ExpandCollapseState);
        Assert.DoesNotContain(":expanded", border.Classes);

        (object?, object?)[] expected = expanded
            ? [(ExpandCollapseState.Expanded, ExpandCollapseState.Collapsed)]
            : [(ExpandCollapseState.Collapsed, ExpandCollapseState.Expanded), (ExpandCollapseState.Expanded, ExpandCollapseState.Collapsed)];
        Assert.Equal(expected, changes);
    }

    [AvaloniaFact]
    public void An_explicit_automation_name_wins_over_the_title()
    {
        var root = Render("> [!TIP]-\n> Body", Pipeline);
        var header = Header(root, "markdown-alert-header");
        AutomationProperties.SetName(header, "Show the tip");

        Assert.Equal("Show the tip", ControlAutomationPeer.CreatePeerForElement(header).GetName());
    }

    [AvaloniaFact]
    public void The_header_of_an_alert_that_does_not_fold_is_text()
    {
        var root = Render("> [!TIP]\n> Body", Pipeline);

        var peer = ControlAutomationPeer.CreatePeerForElement(Header(root, "markdown-alert-header"));

        Assert.IsNotAssignableFrom<IExpandCollapseProvider>(peer);
        Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
    }
}
