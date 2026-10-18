// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Accessibility;

/// <summary>
/// Links are spans inside a text block, so the viewer stands a focusable <see cref="LinkFocusTarget"/>
/// over each one: Tab reaches it, Enter opens it, and screen readers see it as a hyperlink.
/// </summary>
public class LinkFocusTests
{
    private const string Links = """
        Intro [one](https://example.com/1) and [two](https://example.com/2 "Second link").

        # [Three](https://example.com/3)

        - **[four](https://example.com/4)**
        """;

    private sealed record Fixture(Window Window, Button Before, MarkdownViewer Viewer, Button After) : IDisposable
    {
        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

        public LinkFocusTarget[] Targets => Viewer.GetVisualDescendants().OfType<LinkFocusTarget>().ToArray();

        public void Tab(bool shift = false)
        {
            Window.KeyPress(Key.Tab, shift ? RawInputModifiers.Shift : RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
        }

        public string? FocusedLink => (Focused as LinkFocusTarget)?.AccessibleName;

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown, MarkdownPipeline? pipeline = null, double width = 400, bool scrolling = false)
    {
        var before = new Button { Content = "Before" };
        var after = new Button { Content = "After" };
        var viewer = new MarkdownViewer { Pipeline = pipeline, Markdown = markdown };
        if (scrolling)
        {
            viewer.Template = ScrollingViewerTemplate.Create();
            viewer.Height = 100;
        }
        var window = new Window
        {
            Width = width,
            Height = 600,
            Content = new StackPanel { Children = { before, viewer, after } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Fixture(window, before, viewer, after);
    }

    [AvaloniaFact]
    public void Tab_goes_through_the_viewer_and_its_links_in_reading_order()
    {
        using var f = Show(Links);
        f.Before.Focus();

        f.Tab();
        Assert.Same(f.Viewer, f.Focused);

        var visited = new List<string?>();
        for (var i = 0; i < 4; i++)
        {
            f.Tab();
            visited.Add(f.FocusedLink);
        }
        Assert.Equal(["one", "two", "Three", "four"], visited);

        f.Tab();
        Assert.Same(f.After, f.Focused);
    }

    [AvaloniaFact]
    public void Shift_Tab_enters_on_the_last_link_and_walks_back_to_the_viewer()
    {
        using var f = Show(Links);
        f.After.Focus();

        var visited = new List<string?>();
        for (var i = 0; i < 4; i++)
        {
            f.Tab(shift: true);
            visited.Add(f.FocusedLink);
        }
        Assert.Equal(["four", "Three", "two", "one"], visited);

        f.Tab(shift: true);
        Assert.Same(f.Viewer, f.Focused);
        f.Tab(shift: true);
        Assert.Same(f.Before, f.Focused);
    }

    [AvaloniaFact]
    public void Tab_without_links_moves_past_the_viewer()
    {
        using var f = Show("Just text");
        f.Viewer.Focus();

        Assert.Empty(f.Targets);
        f.Tab();
        Assert.Same(f.After, f.Focused);
    }

    [AvaloniaFact]
    public void Shift_Tab_into_a_viewer_without_links_stops_on_the_viewer()
    {
        using var f = Show("Just text");
        f.After.Focus();

        f.Tab(shift: true);
        Assert.Same(f.Viewer, f.Focused);
    }

    [AvaloniaFact]
    public void Only_Tab_and_Shift_Tab_are_custom_navigation()
    {
        using var f = Show(Links);
        var navigation = (ICustomKeyboardNavigation)f.Viewer;

        Assert.Equal((false, null), navigation.GetNext(f.Viewer, NavigationDirection.Down));
        Assert.Equal((false, null), navigation.GetNext(f.Targets[0], NavigationDirection.Right));
        Assert.Equal((false, null), ((ICustomKeyboardNavigation)new MarkdownViewer()).GetNext(f.Viewer, NavigationDirection.Next));
    }

    [AvaloniaFact]
    public void Tab_skips_links_in_hidden_content()
    {
        using var f = Show(Links);
        f.Targets.Single(t => t.AccessibleName == "Three").TextBlock.IsVisible = false;
        f.Viewer.Focus();

        f.Tab();
        f.Tab();
        f.Tab();
        Assert.Equal("four", f.FocusedLink);
    }

    [AvaloniaFact]
    public void Embedded_focusable_controls_are_stops_in_reading_order()
    {
        var pipeline = new MarkdownPipelineBuilder().UseMediaLinks().Build();
        using var f = Show("[before](https://example.com/b) ![Trailer](https://www.youtube.com/watch?v=dQw4w9WgXcQ) [after](https://example.com/a)", pipeline);
        f.Viewer.Focus();

        f.Tab();
        Assert.Equal("before", f.FocusedLink);
        f.Tab();
        Assert.IsType<Button>(f.Focused);
        f.Tab();
        Assert.Equal("after", f.FocusedLink);
        f.Tab(shift: true);
        Assert.IsType<Button>(f.Focused);
    }

    [AvaloniaFact]
    public void Focusable_parts_of_a_fence_renderers_control_template_are_stops_in_reading_order()
    {
        using var f = Show("[before](https://example.com/b)\n\n```chart\nx\n```\n\n[after](https://example.com/a)");
        f.Viewer.UseFenceRenderer("chart", (code, _) => new Expander { Header = code, IsExpanded = true });
        Dispatcher.UIThread.RunJobs();
        f.Viewer.Focus();

        f.Tab();
        Assert.Equal("before", f.FocusedLink);
        f.Tab();
        var header = Assert.IsType<ToggleButton>(f.Focused);
        f.Tab();
        Assert.Equal("after", f.FocusedLink);
        f.Tab(shift: true);
        Assert.Same(header, f.Focused);
    }

    [AvaloniaFact]
    public void Embedded_control_keeps_focus_when_its_paragraph_is_laid_out_again()
    {
        // A text block re-adds its embedded controls on every arrange, e.g. once the video
        // thumbnail has loaded; Avalonia drops the focus of a control that is detached.
        var pipeline = new MarkdownPipelineBuilder().UseMediaLinks().Build();
        using var f = Show("[before](https://example.com/b) ![Trailer](https://www.youtube.com/watch?v=dQw4w9WgXcQ)", pipeline);
        f.Viewer.Focus();
        f.Tab();
        f.Tab();
        var button = Assert.IsType<Button>(f.Focused);
        var detached = 0;
        button.DetachedFromVisualTree += (_, _) => detached++;

        var paragraph = f.Targets.Single().TextBlock;
        paragraph.InvalidateMeasure();
        paragraph.InvalidateArrange();
        f.Window.UpdateLayout();

        Assert.True(detached > 0, "The paragraph did not re-add its embedded controls.");
        Assert.Same(button, f.Focused);
        Assert.Contains(":focus-visible", button.Classes);

        // Focus the button with the pointer instead: a re-layout keeps it without the focus ring.
        f.Viewer.Focus();
        button.Focus(NavigationMethod.Pointer);
        Assert.DoesNotContain(":focus-visible", button.Classes);
        paragraph.InvalidateMeasure();
        f.Window.UpdateLayout();
        Assert.Same(button, f.Focused);
        Assert.DoesNotContain(":focus-visible", button.Classes);
    }

    [AvaloniaFact]
    public void Disabled_embedded_controls_are_skipped()
    {
        var pipeline = new MarkdownPipelineBuilder().UseMediaLinks().Build();
        using var f = Show("[before](https://example.com/b) ![Trailer](https://www.youtube.com/watch?v=dQw4w9WgXcQ) [after](https://example.com/a)", pipeline);
        f.Viewer.GetVisualDescendants().OfType<Button>().Single().IsEnabled = false;
        f.Viewer.Focus();

        f.Tab();
        f.Tab();
        Assert.Equal("after", f.FocusedLink);
    }

    [AvaloniaFact]
    public async Task Tab_moves_past_a_zoomable_image_that_could_not_be_loaded()
    {
        // A zoomable image is a stop; the placeholder that replaces it when it fails to load is not.
        using var f = Show("[before](https://example.com/b) ![Broken](unknown://broken.png) [after](https://example.com/a)");
        await AsyncTestHelpers.WaitUntilAsync(() =>
            f.Viewer.GetVisualDescendants().OfType<Border>().Any(b => b.Classes.Contains("markdown-image-failed")));
        f.Viewer.Focus();

        f.Tab();
        Assert.Equal("before", f.FocusedLink);
        f.Tab();
        Assert.Equal("after", f.FocusedLink);
        f.Tab(shift: true);
        Assert.Equal("before", f.FocusedLink);
    }

    private const string Controls = """
        [first](https://example.com/1)

        > [!NOTE]-
        > Folded [hidden](https://example.com/h)

        ```cs
        var x = 1;
        ```

        - [ ] task [last](https://example.com/2)
        """;

    private static string? Describe(IInputElement? element) => element switch
    {
        LinkFocusTarget target => target.AccessibleName,
        StyledElement styled when styled.Classes.Contains("markdown-alert-header") => "alert header",
        StyledElement styled when styled.Classes.Contains("markdown-code-copy") => "copy button",
        StyledElement styled when styled.Classes.Contains("markdown-task-toggle") => "task checkbox",
        StyledElement styled when styled.Classes.Contains("markdown-details-summary") => "details summary",
        _ => element?.GetType().Name,
    };

    [AvaloniaFact]
    public void Details_summary_is_a_stop_and_hides_the_folded_links()
    {
        var pipeline = new MarkdownPipelineBuilder().UseHtmlDetails().Build();
        using var f = Show("""
            [first](https://example.com/1)

            <details>
            <summary>More</summary>

            Folded [hidden](https://example.com/h)

            </details>

            [last](https://example.com/2)
            """, pipeline);
        f.Viewer.Focus();

        var visited = new List<string?>();
        for (var i = 0; i < 3; i++)
        {
            f.Tab();
            visited.Add(Describe(f.Focused));
        }
        Assert.Equal(["first", "details summary", "last"], visited);
    }

    [AvaloniaFact]
    public void Alert_header_copy_button_and_task_checkbox_are_stops_in_reading_order()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().UseExtendedAlertBlocks().Build();
        using var f = Show(Controls, pipeline);
        f.Viewer.AreTaskListsInteractive = true;
        Dispatcher.UIThread.RunJobs();
        f.Viewer.Focus();

        var visited = new List<string?>();
        for (var i = 0; i < 5; i++)
        {
            f.Tab();
            visited.Add(Describe(f.Focused));
        }
        // The folded alert's link is skipped until the alert is expanded.
        Assert.Equal(["first", "alert header", "copy button", "task checkbox", "last"], visited);

        f.Tab(shift: true);
        f.Tab(shift: true);
        f.Tab(shift: true);
        Assert.Equal("alert header", Describe(f.Focused));
        f.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        f.Tab();
        Assert.Equal("hidden", Describe(f.Focused));
    }

    [AvaloniaFact]
    public void Enter_opens_the_focused_link()
    {
        using var f = Show(Links);
        var clicked = new List<string>();
        f.Viewer.LinkClicked += (_, e) => { clicked.Add(e.Url); e.Handled = true; };
        var unhandled = new List<Key>();
        f.Window.KeyDown += (_, e) => unhandled.Add(e.Key);
        f.Viewer.Focus();
        f.Tab();
        f.Tab();

        f.Window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
        f.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Assert.Empty(clicked);
        Assert.Equal([Key.Enter, Key.Space], unhandled);

        f.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.Equal(["https://example.com/2"], clicked);
        Assert.Equal([Key.Enter, Key.Space], unhandled);
    }

    [AvaloniaFact]
    public void Enter_on_an_anchor_link_scrolls_instead_of_raising_LinkClicked()
    {
        using var f = Show("[Go](#target)\n\n# Target");
        var raised = false;
        f.Viewer.LinkClicked += (_, _) => raised = true;

        f.Targets.Single().Activate();

        Assert.False(raised);
    }

    [AvaloniaFact]
    public void Focused_link_draws_a_ring_around_each_of_its_lines()
    {
        using var f = Show("Some text [a link long enough to wrap onto a second line in a narrow viewer](https://example.com)", width: 160);
        var target = f.Targets.Single();

        Assert.True(target.Lines.Count >= 2, $"Expected the link to wrap, got {target.Lines.Count} line(s).");
        Assert.All(target.Lines, line => Assert.True(new Rect(target.Bounds.Size).Contains(line), $"{line} is outside {target.Bounds}."));
        Assert.Empty(Draw(target));

        f.Viewer.Focus();
        f.Tab();
        Assert.Same(target, f.Focused);

        var rings = Draw(target);
        Assert.Equal(target.Lines.Count, rings.Count);
        Assert.All(rings, ring =>
        {
            Assert.Null(ring.Brush);
            Assert.Equal(2, ring.Pen!.Thickness);
            Assert.Same(Application.Current!.FindResource(ThemeVariant.Light, "SystemControlFocusVisualPrimaryBrush"), ring.Pen.Brush);
        });
        Assert.Equal(target.Lines[0].Inflate(1), rings[0].Geometry!.Bounds);
    }

    [AvaloniaFact]
    public void Ring_falls_back_to_black_or_white_without_FluentTheme()
    {
        Assert.Equal(Colors.White, RingFallback(ThemeVariant.Dark));
        Assert.Equal(Colors.Black, RingFallback(ThemeVariant.Light));
    }

    [AvaloniaFact]
    public void Link_sits_over_its_text_and_follows_it_when_the_text_moves()
    {
        using var f = Show("[one](https://example.com/1)");
        var target = f.Targets.Single();
        var textOrigin = target.TextBlock.TranslatePoint(default, target.GetVisualParent()!)!.Value;

        Assert.Equal(textOrigin.Y, target.Bounds.Y, 1);
        Assert.True(target.Bounds.Width > 0);

        target.TextBlock.Margin = new Thickness(0, 50, 0, 0);
        f.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        f.Window.UpdateLayout();

        Assert.Equal(textOrigin.Y + 50, target.Bounds.Y, 1);
    }

    [AvaloniaFact]
    public void Link_follows_its_text_when_only_the_text_moves()
    {
        // A negative bottom margin keeps the document's height, so only the layout pass's
        // aftermath can tell the links that their paragraph moved.
        using var f = Show("[one](https://example.com/1) [uno](https://example.com/2)\n\nSecond");
        var targets = f.Targets;
        var before = targets.Select(t => t.Bounds.Y).ToArray();

        targets[0].TextBlock.Margin = new Thickness(0, 50, 0, -50);
        f.Window.UpdateLayout();
        f.Window.UpdateLayout();

        Assert.Equal(before.Select(y => y + 50), targets.Select(t => t.Bounds.Y));
    }

    [AvaloniaFact]
    public void Link_rewraps_when_its_paragraph_narrows()
    {
        using var f = Show("Some text [a link that wraps when narrow](https://example.com)", width: 2000);
        var target = f.Targets.Single();
        Assert.Single(target.Lines);

        f.Viewer.Width = 160;
        f.Window.UpdateLayout();
        f.Window.UpdateLayout();

        Assert.True(target.Lines.Count >= 2, $"Expected the link to wrap, got {target.Lines.Count} line(s).");
    }

    [AvaloniaFact]
    public void Link_accounts_for_its_paragraph_padding()
    {
        using var f = Show("[one](https://example.com/1)");
        var target = f.Targets.Single();
        var x = target.Bounds.X;

        target.TextBlock.Padding = new Thickness(20, 10, 0, 0);
        f.Window.UpdateLayout();
        f.Window.UpdateLayout();

        Assert.Equal(x + 20, target.Bounds.X, 1);
    }

    [AvaloniaFact]
    public void Focused_link_scrolls_into_view()
    {
        var filler = string.Join("\n\n", Enumerable.Range(1, 30).Select(i => $"Paragraph {i}"));
        using var f = Show(filler + "\n\n[last](https://example.com/last)", scrolling: true);
        var scrollViewer = f.Viewer.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Assert.Equal(0, scrollViewer.Offset.Y);

        f.Viewer.Focus();
        f.Tab();
        f.Window.UpdateLayout();

        Assert.Equal("last", f.FocusedLink);
        Assert.True(scrollViewer.Offset.Y > 0);
    }

    [AvaloniaFact]
    public void Re_rendering_while_a_link_is_focused_keeps_focus_in_the_viewer()
    {
        using var f = Show(Links);
        f.Viewer.Focus();
        f.Tab();
        Assert.IsType<LinkFocusTarget>(f.Focused);

        f.Viewer.Markdown = "Other [link](https://example.com/x)";
        Assert.Same(f.Viewer, f.Focused);

        f.Tab();
        Assert.IsType<LinkFocusTarget>(f.Focused);
        f.Viewer.Markdown = null;
        Assert.Same(f.Viewer, f.Focused);
    }

    [AvaloniaFact]
    public void Screen_readers_see_links_as_invokable_hyperlinks_inside_their_paragraph()
    {
        using var f = Show(Links);
        var clicked = new List<string>();
        f.Viewer.LinkClicked += (_, e) => { clicked.Add(e.Url); e.Handled = true; };
        var paragraph = f.Targets[0].TextBlock;
        var paragraphPeer = ControlAutomationPeer.CreatePeerForElement(paragraph);

        var links = paragraphPeer.GetChildren();
        Assert.Equal(["one", "two"], links.Select(l => l.GetName()));
        Assert.All(links, l =>
        {
            Assert.Equal(AutomationControlType.Hyperlink, l.GetAutomationControlType());
            Assert.True(l.IsKeyboardFocusable());
            Assert.Same(paragraphPeer, l.GetParent());
        });
        Assert.Equal("Second link", links[1].GetHelpText());
        Assert.Equal(string.Empty, links[0].GetHelpText());

        links[1].GetProvider<IInvokeProvider>()!.Invoke();
        Assert.Equal(["https://example.com/2"], clicked);

        // The paragraph is still read as one string.
        Assert.Equal("Intro one and two.", paragraphPeer.GetName());
    }

    [AvaloniaFact]
    public void Paragraph_lists_embedded_controls_before_its_links()
    {
        using var f = Show("![A square](https://example.com/square.png) and [one](https://example.com/1)");

        var children = ControlAutomationPeer.CreatePeerForElement(f.Targets.Single().TextBlock).GetChildren();

        Assert.Equal([AutomationControlType.Image, AutomationControlType.Hyperlink], children.Select(c => c.GetAutomationControlType()));
    }

    [AvaloniaFact]
    public void Link_peer_finds_its_paragraph_without_the_tree_being_walked_first()
    {
        using var f = Show(Links);
        var target = f.Targets[2];

        var peer = ControlAutomationPeer.CreatePeerForElement(target);

        Assert.Same(ControlAutomationPeer.CreatePeerForElement(target.TextBlock), peer.GetParent());
        Assert.Equal(1, ControlAutomationPeer.CreatePeerForElement(target.TextBlock).GetHeadingLevel());
    }

    [AvaloniaFact]
    public void Link_layer_reports_no_children_of_its_own()
    {
        using var f = Show(Links);
        var layer = f.Viewer.GetVisualDescendants().OfType<LinkFocusLayer>().Single();

        var peer = ControlAutomationPeer.CreatePeerForElement(layer);

        Assert.Empty(peer.GetChildren());
        Assert.False(peer.IsControlElement());
    }

    [AvaloniaTheory]
    [InlineData("[![Build status](https://example.com/badge.png)](https://ci.example.com)", "Build status")]
    [InlineData("[](https://example.com/empty)", null)]
    [InlineData("[two  \nlines](https://example.com/l)", "two lines")]
    [InlineData("[**bold** text](https://example.com/b)", "bold text")]
    public void Link_name_reads_images_as_their_alt_text(string markdown, string? name)
    {
        using var f = Show(markdown);
        var targets = f.Targets;

        if (name is null)
        {
            // An empty link has no text to sit over, so it isn't a stop.
            Assert.Empty(targets);
            return;
        }
        var peer = ControlAutomationPeer.CreatePeerForElement(Assert.Single(targets));
        Assert.Equal(name, peer.GetName());
    }

    [AvaloniaFact]
    public void Link_name_falls_back_to_its_address()
    {
        var target = new LinkFocusTarget(new MarkdownSelectableTextBlock(),
            new MarkdownHyperlink { NavigateUri = new Uri("https://example.com/x") }, 0, 1, _ => { });

        Assert.Equal("https://example.com/x", target.AccessibleName);
        Assert.Equal(string.Empty, new LinkFocusTarget(new MarkdownSelectableTextBlock(),
            new MarkdownHyperlink(), 0, 1, _ => { }).AccessibleName);
    }

    [AvaloniaFact]
    public void Automation_name_overrides_the_link_text()
    {
        using var f = Show("[one](https://example.com/1)");
        var target = f.Targets.Single();
        AutomationProperties.SetName(target, "Custom");
        AutomationProperties.SetHelpText(target, "Help");

        var peer = ControlAutomationPeer.CreatePeerForElement(target);

        Assert.Equal("Custom", peer.GetName());
        Assert.Equal("Help", peer.GetHelpText());
    }

    private static List<GeometryDrawing> Draw(Control control)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            control.Render(context);
        return group.Children.OfType<GeometryDrawing>().ToList();
    }

    private static Color RingFallback(ThemeVariant variant) =>
        ((ISolidColorBrush)LinkFocusTarget.FallbackRingBrush(variant)).Color;
}
