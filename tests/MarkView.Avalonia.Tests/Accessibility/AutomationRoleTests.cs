// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Accessibility;

/// <summary>
/// What a screen reader sees of each rendered block, read through the same automation peers
/// the platform backends (UIA, NSAccessibility, AT-SPI) query.
/// </summary>
public class AutomationRoleTests : RenderTestBase
{
    private static AutomationPeer Peer(Control control) => ControlAutomationPeer.CreatePeerForElement(control);

    [AvaloniaTheory]
    [InlineData("# H1", 1)]
    [InlineData("## H2", 2)]
    [InlineData("### H3", 3)]
    [InlineData("#### H4", 4)]
    [InlineData("##### H5", 5)]
    [InlineData("###### H6", 6)]
    public void Heading_exposes_its_level(string markdown, int level)
    {
        var peer = Peer(Assert.Single(Render(markdown).Children));

        Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
        Assert.Equal(level, peer.GetHeadingLevel());
        Assert.Equal(markdown.TrimStart('#', ' '), peer.GetName());
    }

    [AvaloniaFact]
    public void Paragraph_is_not_a_heading()
    {
        Assert.Equal(0, Peer(Assert.Single(Render("Plain text").Children)).GetHeadingLevel());
    }

    [AvaloniaFact]
    public void Image_is_named_by_its_alt_text()
    {
        var image = FindAll<Image>(Render("![A red square](https://example.com/red.png)")).Single();
        var peer = Peer(image);

        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
        Assert.Equal("A red square", peer.GetName());
        Assert.True(peer.IsContentElement());
    }

    [AvaloniaFact]
    public void Image_without_alt_text_is_hidden_as_decorative()
    {
        var peer = Peer(FindAll<Image>(Render("![](https://example.com/divider.png)")).Single());

        Assert.False(peer.IsControlElement());
        Assert.False(peer.IsContentElement());
    }

    [AvaloniaTheory]
    [InlineData("![Launch trailer](https://www.youtube.com/watch?v=dQw4w9WgXcQ)", "Launch trailer")]
    [InlineData("![](https://www.youtube.com/watch?v=dQw4w9WgXcQ)", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    public void Youtube_embed_is_a_named_link(string markdown, string name)
    {
        var pipeline = new MarkdownPipelineBuilder().UseMediaLinks().Build();
        var peer = Peer(FindAll<Button>(Render(markdown, pipeline)).Single());

        Assert.Equal(AutomationControlType.Hyperlink, peer.GetAutomationControlType());
        Assert.Equal(name, peer.GetName());
    }

    [AvaloniaFact]
    public void Bullet_list_is_a_list_of_items_with_hidden_bullets()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("- one\n- two").Children));

        AssertRole(list, AutomationControlType.List);
        Assert.Equal(2, list.Children.Count);
        foreach (var item in list.Children.Cast<Grid>())
        {
            AssertRole(item, AutomationControlType.ListItem);
            var marker = item.Children.OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-list-marker"));
            Assert.False(Peer(marker).IsControlElement());
        }
    }

    [AvaloniaTheory]
    [InlineData("- [x] Ship it", true)]
    [InlineData("- [ ] Write **docs**", false)]
    public void Interactive_task_is_a_check_box_named_by_its_text(string markdown, bool isChecked)
    {
        var renderer = new AvaloniaRenderer { AreTaskListsInteractive = true };
        var pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        var toggle = FindAll<ToggleButton>(renderer.RootPanel).Single();
        var peer = Peer(toggle);

        Assert.Equal(AutomationControlType.CheckBox, peer.GetAutomationControlType());
        Assert.True(peer.IsContentElement());
        Assert.Equal(markdown[6..].Replace("**", ""), peer.GetName());
        var toggleProvider = Assert.IsAssignableFrom<IToggleProvider>(peer);
        Assert.Equal(isChecked ? ToggleState.On : ToggleState.Off, toggleProvider.ToggleState);
    }

    [AvaloniaFact]
    public void Ordered_list_keeps_its_numbers_readable()
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render("3. three\n4. four").Children));

        AssertRole(list, AutomationControlType.List);
        var marker = ((Grid)list.Children[0]).Children.OfType<TextBlock>()
            .Single(t => t.Classes.Contains("markdown-list-marker"));
        Assert.True(Peer(marker).IsControlElement());
        Assert.Equal("3.", Peer(marker).GetName());
    }

    [AvaloniaFact]
    public void Table_exposes_header_and_data_cells()
    {
        var pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();
        var table = Assert.IsType<Grid>(Assert.Single(Render("| A | B |\n|---|---|\n| 1 | 2 |", pipeline).Children));

        AssertRole(table, AutomationControlType.Table);
        var cells = table.Children.Cast<Border>().ToList();
        Assert.Equal(4, cells.Count);
        Assert.All(cells.Take(2), c => AssertRole(c, AutomationControlType.HeaderItem));
        Assert.All(cells.Skip(2), c => AssertRole(c, AutomationControlType.DataItem));
    }

    [AvaloniaFact]
    public void Blockquote_is_a_group()
    {
        AssertRole(Assert.Single(Render("> quoted").Children), AutomationControlType.Group);
    }

    [AvaloniaTheory]
    [InlineData("NOTE")]
    [InlineData("CAUTION")]
    public void Alert_is_a_group_named_by_its_title(string kind)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAlertBlocks().Build();
        var alert = Assert.Single(Render($"> [!{kind}]\n> Body", pipeline).Children);

        AssertRole(alert, AutomationControlType.Group);
        Assert.Equal(kind, Peer(alert).GetName());
    }

    [AvaloniaFact]
    public void Figure_is_a_group_named_by_its_caption()
    {
        var pipeline = new MarkdownPipelineBuilder().UseFigures().Build();
        var figure = Assert.Single(Render("^^^\nContent\n^^^ The caption", pipeline).Children);

        AssertRole(figure, AutomationControlType.Group);
        Assert.Equal("The caption", Peer(figure).GetName());
    }

    [AvaloniaFact]
    public void Figure_without_caption_is_an_unnamed_group()
    {
        var pipeline = new MarkdownPipelineBuilder().UseFigures().Build();
        var figure = Assert.Single(Render("^^^\nContent\n^^^", pipeline).Children);

        AssertRole(figure, AutomationControlType.Group);
        Assert.Equal(string.Empty, Peer(figure).GetName());
    }

    [AvaloniaFact]
    public void Thematic_break_is_a_separator()
    {
        var result = Render("Above\n\n---\n\nBelow");
        AssertRole(result.Children.OfType<Separator>().Single(), AutomationControlType.Separator);
    }

    private static void AssertRole(Control control, AutomationControlType role)
    {
        var peer = Peer(control);
        Assert.Equal(role, peer.GetAutomationControlType());
        Assert.True(peer.IsControlElement(), $"{control.GetType().Name} should be in the control view.");
        Assert.True(peer.IsContentElement(), $"{control.GetType().Name} should be in the content view.");
    }

    private static IEnumerable<T> FindAll<T>(Control root) where T : Control
    {
        if (root is T match) yield return match;
        IEnumerable<Control> children = root switch
        {
            Panel panel => panel.Children,
            ContentControl { Content: Control content } => [content],
            Decorator { Child: { } child } => [child],
            TextBlock { Inlines: { } inlines } => Containers(inlines),
            _ => [],
        };
        foreach (var child in children)
            foreach (var found in FindAll<T>(child))
                yield return found;
    }

    private static IEnumerable<Control> Containers(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is InlineUIContainer { Child: { } child })
                yield return child;
            else if (inline is Span span)
                foreach (var nested in Containers(span.Inlines))
                    yield return nested;
        }
    }
}
