// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// Foldable alerts (<c>&gt; [!KIND]-</c> / <c>&gt; [!KIND]+</c>) inside a full <see cref="MarkdownViewer"/>:
/// header clicks, document selection and anchor navigation.
/// </summary>
public class MarkdownViewerFoldableAlertTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public Border Alert => Viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-alert"));
        public TextBlock Header => Alert.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-alert-header"));
        public CollapsibleAlert State => CollapsibleAlert.Get(Alert)!;

        public MarkdownSelectableTextBlock Paragraph(string text) =>
            Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>()
                .Single(tb => MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!).StartsWith(text, StringComparison.Ordinal));

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown)
    {
        var viewer = new MarkdownViewer().UseAlertBlocks();
        viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        // The headless text drawing contributes no hit-testable glyph geometry; a transparent
        // background makes each paragraph's whole row hit-testable instead.
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        window.Show();
        return new Fixture(window, viewer);
    }

    // Mutation testing compiles the library without its AXAML, so the theme include above cannot load
    // there; the tests built on this fixture leave it out to also count under Stryker.
    private static Fixture ShowWithoutTheme(string markdown)
    {
        var viewer = new MarkdownViewer().UseAlertBlocks();
        viewer.Template = ScrollingViewerTemplate.Create();
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        return new Fixture(window, viewer);
    }

    private static Point At(Window window, TextBlock block, int index)
    {
        var caret = block.TextLayout.HitTestTextPosition(index);
        var origin = block.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(origin.X + block.Padding.Left + caret.X + 0.5, origin.Y + block.Padding.Top + caret.Y + caret.Height / 2);
    }

    private static Point HeaderPoint(Fixture f) =>
        f.Header.TranslatePoint(new Point(f.Header.Bounds.Width / 2, f.Header.Bounds.Height / 2), f.Window)!.Value;

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static void Drag(Window window, Point from, Point to)
    {
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to, MouseButton.Left);
    }

    private static DocumentSelectionLayer Layer(MarkdownViewer viewer) =>
        Assert.IsType<DocumentSelectionLayer>(Assert.IsType<Grid>(viewer.Content).Children[1]);

    private const string HiddenBody = """
        Before

        > [!NOTE]-
        > Hidden one
        >
        > Hidden two

        After
        """;

    [AvaloniaFact]
    public void Selection_skips_the_body_of_a_collapsed_alert_until_it_is_expanded()
    {
        var viewer = new MarkdownViewer().UseAlertBlocks();
        viewer.Markdown = HiddenBody;
        var alert = viewer.GetLogicalDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-alert"));

        viewer.SelectAll();
        Assert.Equal("Before\nAfter", viewer.GetSelectedText());

        CollapsibleAlert.Get(alert)!.Toggle();
        viewer.SelectAll();
        Assert.Equal("Before\nHidden one\nHidden two\nAfter", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void A_hidden_viewer_still_copies_its_selection()
    {
        using var f = Show("Alpha\n\nBeta");
        f.Viewer.IsVisible = false;

        f.Viewer.SelectAll();

        Assert.Equal("Alpha\nBeta", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Collapsed_body_is_not_highlighted()
    {
        using var f = Show(HiddenBody);
        var layer = Layer(f.Viewer);
        f.Viewer.SelectAll();

        var group = new DrawingGroup();
        using (var context = group.Open())
            layer.Render(context);

        Assert.Equal(2, group.Children.Count); // "Before" and "After"
    }

    [AvaloniaFact]
    public void Clicking_the_header_toggles_the_body_without_selecting()
    {
        using var f = Show(HiddenBody);
        var hidden = f.Paragraph("Hidden one");
        Assert.False(hidden.IsEffectivelyVisible);

        Click(f.Window, HeaderPoint(f));

        Assert.True(f.State.IsExpanded);
        Assert.True(hidden.IsEffectivelyVisible);
        Assert.Equal(string.Empty, f.Viewer.GetSelectedText());

        f.Window.UpdateLayout();
        Click(f.Window, HeaderPoint(f));

        Assert.False(f.State.IsExpanded);
        Assert.False(hidden.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void Releasing_off_the_header_or_with_another_button_does_not_toggle()
    {
        using var f = Show(HiddenBody);
        var bottom = f.Header.TranslatePoint(new Point(5, f.Header.Bounds.Height - 0.5), f.Window)!.Value;

        f.Window.MouseDown(bottom, MouseButton.Left);
        f.Window.MouseMove(bottom + new Vector(0, 2), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(bottom + new Vector(0, 2), MouseButton.Left);
        Assert.False(f.State.IsExpanded);

        f.Window.MouseDown(bottom, MouseButton.Right);
        f.Window.MouseUp(bottom, MouseButton.Right);
        Assert.False(f.State.IsExpanded);

        Click(f.Window, bottom);
        Assert.True(f.State.IsExpanded);
    }

    [AvaloniaFact]
    public void Dragging_from_the_header_does_not_toggle()
    {
        using var f = Show(HiddenBody);
        var start = HeaderPoint(f);

        Drag(f.Window, start, start + new Vector(0, 40));

        Assert.False(f.State.IsExpanded);
    }

    [AvaloniaFact]
    public void Content_after_a_collapsed_alert_is_selectable_and_clickable_where_the_body_was()
    {
        // The hidden paragraph wraps over several lines, so the bounds it keeps from its last layout
        // cover the place the paragraph after the alert moves up to.
        var hidden = string.Join(" ", Enumerable.Repeat("Hidden words", 30));
        using var f = Show($"""
            > [!NOTE]+
            > {hidden}

            After [link](https://example.com/)
            """);
        var urls = new List<string>();
        f.Viewer.LinkClicked += (_, e) => { urls.Add(e.Url); e.Handled = true; };

        Click(f.Window, HeaderPoint(f));
        f.Window.UpdateLayout();
        Assert.False(f.State.IsExpanded);

        var after = f.Paragraph("After");
        Drag(f.Window, At(f.Window, after, 0), At(f.Window, after, 3));
        Assert.Equal("Aft", f.Viewer.GetSelectedText());

        Click(f.Window, At(f.Window, after, 7));
        Assert.Equal(["https://example.com/"], urls);
    }

    [AvaloniaFact]
    public void ScrollToAnchor_expands_a_collapsed_alert_and_scrolls_to_its_heading()
    {
        var leading = string.Join("\n\n", Enumerable.Range(0, 30).Select(i => $"Paragraph {i}"));
        var trailing = string.Join("\n\n", Enumerable.Range(0, 30).Select(i => $"Trailing paragraph {i}"));
        using var f = Show($"""
            {leading}

            > [!NOTE]- Details
            > Intro
            >
            > ## Inside heading
            >
            > Body

            {trailing}
            """);
        var scrollViewer = f.Viewer.GetVisualDescendants().OfType<ScrollViewer>().Single();
        var heading = f.Viewer.GetVisualDescendants().OfType<TextBlock>().Single(tb => tb.Classes.Contains("markdown-h2"));

        f.Viewer.ScrollToAnchor("inside-heading");
        f.Window.UpdateLayout();

        Assert.True(f.State.IsExpanded);
        Assert.Equal(16, heading.TranslatePoint(new Point(0, 0), scrollViewer)!.Value.Y, 3);
    }

    [AvaloniaFact]
    public void ScrollToAnchor_lays_out_the_expanded_alert_before_measuring_its_heading()
    {
        var leading = string.Join("\n\n", Enumerable.Range(0, 30).Select(i => $"Paragraph {i}"));
        var trailing = string.Join("\n\n", Enumerable.Range(0, 30).Select(i => $"Trailing paragraph {i}"));
        using var f = ShowWithoutTheme($"""
            {leading}

            > [!NOTE]-
            > Intro
            >
            > ## Inside heading

            {trailing}
            """);
        var scrollViewer = f.Viewer.GetVisualDescendants().OfType<ScrollViewer>().Single();
        var heading = f.Viewer.GetVisualDescendants().OfType<TextBlock>().Single(tb => tb.Classes.Contains("markdown-h2"));

        f.Viewer.ScrollToAnchor("inside-heading");
        f.Window.UpdateLayout();

        Assert.True(f.State.IsExpanded);
        Assert.Equal(16, heading.TranslatePoint(new Point(0, 0), scrollViewer)!.Value.Y, 3);
    }

    [AvaloniaFact]
    public void Hit_testing_ignores_the_bounds_a_collapsed_body_kept_from_its_last_layout()
    {
        var hidden = string.Join(" ", Enumerable.Repeat("Hidden words", 30));
        using var f = ShowWithoutTheme($"""
            > [!NOTE]+
            > {hidden}

            After
            """);
        var layer = Layer(f.Viewer);
        var hiddenParagraph = f.Paragraph("Hidden");
        f.State.Toggle();
        f.Window.UpdateLayout();
        var after = f.Paragraph("After");
        var point = after.TranslatePoint(new Point(1, after.Bounds.Height / 2), layer)!.Value;
        Assert.True(hiddenParagraph.Bounds.Height > after.Bounds.Height); // the stale bounds overlap "After"

        var entry = layer.HitTestEntry(point);

        Assert.Same(after, entry?.Element);
        Assert.Equal(entry!.AbsStart, layer.HitTestOffset(point));
    }

    [AvaloniaFact]
    public void ScrollToAnchor_outside_a_window_expands_the_alert()
    {
        var viewer = new MarkdownViewer().UseAlertBlocks();
        viewer.Markdown = "> [!NOTE]-\n> ## Inside heading";
        var alert = viewer.GetLogicalDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-alert"));

        viewer.ScrollToAnchor("inside-heading");

        Assert.True(CollapsibleAlert.Get(alert)!.IsExpanded);
    }

    [AvaloniaFact]
    public void ScrollToAnchor_leaves_an_expanded_alert_expanded()
    {
        var viewer = new MarkdownViewer().UseAlertBlocks();
        viewer.Markdown = "> [!NOTE]+\n> ## Inside heading";
        var alert = viewer.GetLogicalDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-alert"));

        viewer.ScrollToAnchor("inside-heading");

        Assert.True(CollapsibleAlert.Get(alert)!.IsExpanded);
    }
}
