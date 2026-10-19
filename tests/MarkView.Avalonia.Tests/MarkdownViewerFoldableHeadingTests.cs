// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// Foldable headings (<see cref="MarkdownViewer.AreHeadingsFoldable"/>): which blocks a collapsed heading
/// hides, the fold button, selection, navigation, find, screen readers and streaming.
/// </summary>
public class MarkdownViewerFoldableHeadingTests
{
    private const string Document = """
        Intro

        # One

        One body

        ## One A

        One A body

        ### One A i

        One A i body

        ## One B

        One B body

        # Two

        Two body
        """;

    private sealed record Fixture(Window Window, Button Before, MarkdownViewer Viewer) : IDisposable
    {
        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

        public void Tab()
        {
            Window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }

    // Mutation testing compiles the library without its AXAML, so the viewer gets a minimal template with
    // the PART_ScrollViewer instead of the theme's.
    private static Fixture Show(string markdown, Action<MarkdownViewer>? configure = null)
    {
        var before = new Button { Content = "Before" };
        var viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create(), AreHeadingsFoldable = true };
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 600, Content = new StackPanel { Children = { before, viewer } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Fixture(window, before, viewer);
    }

    private static MarkdownViewer Render(string markdown) =>
        new() { AreHeadingsFoldable = true, Markdown = markdown };

    private static StackPanel Root(MarkdownViewer viewer) =>
        Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);

    private static MarkdownSelectableTextBlock Block(MarkdownViewer viewer, string text) =>
        viewer.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>()
            .Single(tb => MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!).TrimStart('￼') == text);

    private static HeadingFold Fold(MarkdownViewer viewer, string heading) => HeadingFold.Get(Block(viewer, heading))!;

    private static HeadingFoldToggle Toggle(MarkdownViewer viewer, string heading) =>
        Assert.IsType<HeadingFoldToggle>(Assert.IsType<InlineUIContainer>(Block(viewer, heading).Inlines![0]).Child);

    private static string[] VisibleTexts(MarkdownViewer viewer) =>
        Root(viewer).Children.Where(c => c.IsVisible).OfType<MarkdownSelectableTextBlock>()
            .Select(tb => MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!).TrimStart('￼'))
            .ToArray();

    [AvaloniaFact]
    public void Headings_are_not_foldable_by_default()
    {
        var viewer = new MarkdownViewer { Markdown = "# Title\n\nBody" };

        var heading = Block(viewer, "Title");
        Assert.Null(HeadingFold.Get(heading));
        Assert.DoesNotContain("markdown-heading-foldable", heading.Classes);
        Assert.DoesNotContain(heading.Inlines!, inline => inline is InlineUIContainer);
    }

    [AvaloniaFact]
    public void A_foldable_heading_starts_expanded_with_its_fold_button_first()
    {
        var viewer = Render("## Title\n\nBody");

        var heading = Block(viewer, "Title");
        var toggle = Toggle(viewer, "Title");
        Assert.Contains("markdown-heading-foldable", heading.Classes);
        Assert.Contains(":expanded", heading.Classes);
        Assert.Contains("markdown-heading-fold", toggle.Classes);
        Assert.True(toggle.IsExpanded);
        Assert.Equal(2, HeadingFold.Get(heading)!.Level);
        Assert.Equal(["Title", "Body"], VisibleTexts(viewer));
    }

    [AvaloniaFact]
    public void Collapsing_a_heading_hides_its_section_up_to_the_next_heading_of_the_same_or_a_higher_level()
    {
        var viewer = Render(Document);

        Fold(viewer, "One A").Toggle();

        Assert.Equal(["Intro", "One", "One body", "One A", "One B", "One B body", "Two", "Two body"], VisibleTexts(viewer));
        Assert.DoesNotContain(":expanded", Block(viewer, "One A").Classes);
        Assert.False(Toggle(viewer, "One A").IsExpanded);

        Fold(viewer, "One A").Toggle();

        Assert.Equal(11, VisibleTexts(viewer).Length);
        Assert.Contains(":expanded", Block(viewer, "One A").Classes);
    }

    [AvaloniaFact]
    public void A_collapsed_heading_inside_a_collapsed_section_stays_collapsed_when_the_outer_one_is_expanded()
    {
        var viewer = Render(Document);
        Fold(viewer, "One A").Toggle();

        Fold(viewer, "One").Toggle();
        Assert.Equal(["Intro", "One", "Two", "Two body"], VisibleTexts(viewer));

        Fold(viewer, "One").Toggle();
        Assert.Equal(["Intro", "One", "One body", "One A", "One B", "One B body", "Two", "Two body"], VisibleTexts(viewer));
    }

    [AvaloniaFact]
    public void A_block_hidden_for_another_reason_stays_hidden_when_the_section_is_expanded()
    {
        var viewer = Render(Document);
        Block(viewer, "One body").IsVisible = false;

        Fold(viewer, "One").Toggle();
        Fold(viewer, "One").Toggle();

        Assert.False(Block(viewer, "One body").IsVisible);
        Assert.True(Block(viewer, "One A body").IsVisible);
    }

    [AvaloniaFact]
    public void Folding_stays_within_the_panel_of_the_heading()
    {
        var viewer = Render("> # Quoted\n>\n> Quoted body\n\nAfter the quote");

        Fold(viewer, "Quoted").Toggle();

        Assert.False(Block(viewer, "Quoted body").IsVisible);
        Assert.True(Block(viewer, "After the quote").IsVisible);
    }

    [AvaloniaFact]
    public void Selection_skips_a_collapsed_section_and_never_copies_the_fold_button()
    {
        var viewer = Render("# Title\n\nBody\n\n# Next");

        viewer.SelectAll();
        Assert.Equal("Title\nBody\nNext", viewer.GetSelectedText());

        Fold(viewer, "Title").Toggle();
        viewer.SelectAll();
        Assert.Equal("Title\nNext", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Clicking_the_fold_button_toggles_the_section_without_selecting()
    {
        using var f = Show("# Title\n\nBody");
        var toggle = Toggle(f.Viewer, "Title");
        var point = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), f.Window)!.Value;

        f.Window.MouseDown(point, MouseButton.Left);
        f.Window.MouseUp(point, MouseButton.Left);

        Assert.False(toggle.IsExpanded);
        Assert.False(Block(f.Viewer, "Body").IsVisible);
        Assert.Equal(string.Empty, f.Viewer.GetSelectedText());

        f.Window.MouseDown(point, MouseButton.Right);
        f.Window.MouseUp(point, MouseButton.Right);
        Assert.False(toggle.IsExpanded);
        // The right click opened the viewer's context menu, which would take the next click to close.
        f.Viewer.ContextFlyout!.Hide();

        f.Window.MouseDown(point, MouseButton.Middle);
        f.Window.MouseUp(point, MouseButton.Middle);
        Assert.False(toggle.IsExpanded);

        // A press on the button released away from it is not a click.
        f.Window.MouseDown(point, MouseButton.Left);
        f.Window.MouseUp(point + new Vector(0, toggle.Bounds.Height * 3), MouseButton.Left);
        Assert.False(toggle.IsExpanded);

        f.Window.MouseDown(point, MouseButton.Left);
        f.Window.MouseUp(point, MouseButton.Left);
        Assert.True(toggle.IsExpanded);
    }

    [AvaloniaFact]
    public void The_fold_button_is_a_tab_stop_that_enter_and_space_toggle()
    {
        using var f = Show("# Title\n\nBody");
        var toggle = Toggle(f.Viewer, "Title");
        f.Before.Focus();

        f.Tab();
        f.Tab();
        Assert.Same(toggle, f.Focused);

        f.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.False(toggle.IsExpanded);

        f.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Assert.True(toggle.IsExpanded);

        f.Window.KeyPress(Key.A, RawInputModifiers.None, PhysicalKey.A, null);
        Assert.True(toggle.IsExpanded);
    }

    [AvaloniaFact]
    public void A_click_or_a_key_that_toggles_the_section_goes_no_further()
    {
        using var f = Show("# Title\n\nBody");
        var toggle = Toggle(f.Viewer, "Title");
        var point = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), f.Window)!.Value;
        var releases = 0;
        var keys = 0;
        f.Window.AddHandler(InputElement.PointerReleasedEvent, (_, _) => releases++);
        f.Window.AddHandler(InputElement.KeyDownEvent, (_, _) => keys++);

        f.Window.MouseDown(point, MouseButton.Middle);
        f.Window.MouseUp(point, MouseButton.Middle);
        Assert.Equal(1, releases);
        f.Window.MouseDown(point, MouseButton.Left);
        f.Window.MouseUp(point, MouseButton.Left);
        Assert.Equal(1, releases);

        toggle.Focus(NavigationMethod.Tab);
        f.Window.KeyPress(Key.A, RawInputModifiers.None, PhysicalKey.A, null);
        Assert.Equal(1, keys);
        f.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.Equal(1, keys);
    }

    [AvaloniaFact]
    public void The_fold_button_is_clickable_across_its_box_and_draws_a_focus_ring_only_when_focused_from_the_keyboard()
    {
        using var f = Show("# Title\n\nBody");
        var toggle = Toggle(f.Viewer, "Title");
        var foreground = toggle.GetValue(TextElement.ForegroundProperty);
        Assert.NotNull(foreground);

        var drawings = Draw(toggle);
        Assert.Equal(2, drawings.Length);
        Assert.Same(Brushes.Transparent, drawings[0].Brush);
        Assert.Equal(new Rect(toggle.Bounds.Size), drawings[0].Geometry!.Bounds);
        Assert.Same(foreground, drawings[1].Brush);
        Assert.Equal(toggle.GetTriangle().Bounds, drawings[1].Geometry!.Bounds);

        toggle.Focus(NavigationMethod.Pointer);
        Assert.True(toggle.IsFocused);
        Assert.Equal(2, Draw(toggle).Length);

        f.Before.Focus();
        f.Tab();
        f.Tab();
        Assert.True(toggle.IsFocused);
        var focused = Draw(toggle);
        Assert.Equal(3, focused.Length);
        Assert.Null(focused[2].Brush);
        Assert.Same(foreground, focused[2].Pen!.Brush);
        Assert.Equal(new Rect(toggle.Bounds.Size).Deflate(0.5), focused[2].Geometry!.Bounds);

        f.Before.Focus();
        Assert.False(toggle.IsFocused);
        Assert.Equal(2, Draw(toggle).Length);
    }

    private static GeometryDrawing[] Draw(Control control)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            control.Render(context);
        return group.Children.Cast<GeometryDrawing>().ToArray();
    }

    [AvaloniaFact]
    public void Navigating_to_a_heading_in_a_collapsed_section_expands_the_sections_around_it_but_not_its_own()
    {
        using var f = Show(Document);
        Fold(f.Viewer, "One A i").Toggle();
        Fold(f.Viewer, "One A").Toggle();
        Fold(f.Viewer, "One").Toggle();
        Fold(f.Viewer, "Two").Toggle();

        f.Viewer.ScrollToAnchor("one-a-i");

        Assert.True(Fold(f.Viewer, "One").IsExpanded);
        Assert.True(Fold(f.Viewer, "One A").IsExpanded);
        Assert.False(Fold(f.Viewer, "One A i").IsExpanded);
        Assert.False(Fold(f.Viewer, "Two").IsExpanded);
        Assert.True(Block(f.Viewer, "One A i").IsVisible);
        Assert.False(Block(f.Viewer, "One A i body").IsVisible);
        // Sections the target is not in keep their state.
        Assert.False(Block(f.Viewer, "Two body").IsVisible);
    }

    [AvaloniaFact]
    public void Finding_a_match_in_a_collapsed_section_expands_it()
    {
        using var f = Show(Document);
        Fold(f.Viewer, "One").Toggle();

        Assert.Equal(1, f.Viewer.Find("One A i body"));
        Assert.True(f.Viewer.FindNext());

        Assert.True(Fold(f.Viewer, "One").IsExpanded);
        Assert.True(Block(f.Viewer, "One A i body").IsVisible);
    }

    [AvaloniaFact]
    public void Revealing_a_visible_block_expands_nothing()
    {
        var viewer = Render(Document);
        Fold(viewer, "Two").Toggle();

        Assert.False(HeadingFold.Reveal(Block(viewer, "One body")));
        Assert.False(Fold(viewer, "Two").IsExpanded);
    }

    [AvaloniaFact]
    public void Screen_readers_see_a_button_named_by_the_heading_that_expands_and_collapses()
    {
        using var f = Show("# Title *text*\n\nBody");
        var toggle = Toggle(f.Viewer, "Title text");

        var peer = ControlAutomationPeer.CreatePeerForElement(toggle);
        var expandCollapse = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer);
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
        Assert.Equal("Title text", peer.GetName());
        Assert.True(peer.IsContentElement());
        Assert.True(peer.IsControlElement());
        Assert.False(expandCollapse.ShowsMenu);
        Assert.Equal(ExpandCollapseState.Expanded, expandCollapse.ExpandCollapseState);

        var changes = new List<(object? Old, object? New)>();
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
                changes.Add((e.OldValue, e.NewValue));
        };

        expandCollapse.Collapse();
        expandCollapse.Collapse();
        Assert.Equal(ExpandCollapseState.Collapsed, expandCollapse.ExpandCollapseState);
        Assert.False(Block(f.Viewer, "Body").IsVisible);

        expandCollapse.Expand();
        expandCollapse.Expand();
        Assert.True(Block(f.Viewer, "Body").IsVisible);
        Assert.Equal(
            [(ExpandCollapseState.Expanded, ExpandCollapseState.Collapsed), (ExpandCollapseState.Collapsed, ExpandCollapseState.Expanded)],
            changes);
    }

    [AvaloniaFact]
    public void A_fold_button_outside_a_heading_has_no_name()
    {
        Assert.Equal(string.Empty, ControlAutomationPeer.CreatePeerForElement(new HeadingFoldToggle()).GetName());
    }

    [AvaloniaFact]
    public void An_explicit_automation_name_wins_over_the_heading_text()
    {
        using var f = Show("# Title\n\nBody");
        var toggle = Toggle(f.Viewer, "Title");
        AutomationProperties.SetName(toggle, "Fold Title");

        Assert.Equal("Fold Title", ControlAutomationPeer.CreatePeerForElement(toggle).GetName());
    }

    [AvaloniaFact]
    public void Turning_folding_off_renders_the_headings_again_without_fold_buttons()
    {
        var viewer = Render("# Title\n\nBody");

        viewer.AreHeadingsFoldable = false;

        Assert.Null(HeadingFold.Get(Block(viewer, "Title")));
    }

    // ── Streaming ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Blocks_streamed_after_a_collapsed_heading_join_its_section()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("# One\n\nFirst\n\n");
        session.ApplyPendingUpdate();
        Fold(f.Viewer, "One").Toggle();

        session.Append("Second\n\n# Two\n\nThird");
        session.ApplyPendingUpdate();

        Assert.Equal(["One", "Two", "Third"], VisibleTexts(f.Viewer));

        session.Complete();
        Assert.Equal(["One", "Two", "Third"], VisibleTexts(f.Viewer));
    }

    [AvaloniaFact]
    public void A_heading_rendered_again_by_a_streaming_update_keeps_its_fold_state()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("> # Quoted\n>\n> Body");
        session.ApplyPendingUpdate();
        Fold(f.Viewer, "Quoted").Toggle();

        // The quote is still growing, so the update renders it, and its heading, again.
        session.Append(" grows");
        session.ApplyPendingUpdate();

        Assert.False(Fold(f.Viewer, "Quoted").IsExpanded);
        Assert.False(Block(f.Viewer, "Body grows").IsVisible);
    }

    [AvaloniaFact]
    public void Several_collapsed_headings_rendered_again_in_one_panel_keep_their_sections_hidden()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("> # A\n>\n> A body\n>\n> # B\n>\n> B body\n>\n> # C\n>\n> C body");
        session.ApplyPendingUpdate();
        Fold(f.Viewer, "A").Toggle();
        Fold(f.Viewer, "B").Toggle();

        session.Append(" grows");
        session.ApplyPendingUpdate();

        Assert.False(Block(f.Viewer, "A body").IsVisible);
        Assert.False(Block(f.Viewer, "B body").IsVisible);
        Assert.True(Block(f.Viewer, "C body grows").IsVisible);
    }

    [AvaloniaFact]
    public void An_alert_rendered_again_by_a_streaming_update_keeps_its_fold_state()
    {
        using var f = Show("", viewer => viewer.UseAlertBlocks());
        var session = f.Viewer.BeginStreaming();
        session.Append("> [!NOTE]-\n> Body");
        session.ApplyPendingUpdate();
        Alert(f.Viewer).Toggle();

        session.Append(" grows");
        session.ApplyPendingUpdate();

        Assert.True(Alert(f.Viewer).IsExpanded);
        Assert.True(Block(f.Viewer, "Body grows").IsEffectivelyVisible);

        static CollapsibleAlert Alert(MarkdownViewer viewer) =>
            CollapsibleAlert.Get(viewer.GetLogicalDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-alert")))!;
    }

    [AvaloniaFact]
    public void A_new_document_starts_with_every_section_expanded()
    {
        var viewer = Render("# Title\n\nBody");
        Fold(viewer, "Title").Toggle();

        viewer.Markdown = "# Title\n\nBody!";

        Assert.True(Fold(viewer, "Title").IsExpanded);
    }
}
