// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>The viewer's default context menu.</summary>
public class MarkdownViewerContextMenuTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public MarkdownSelectableTextBlock[] Blocks =>
            Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().ToArray();

        public MenuFlyout Flyout => Assert.IsType<MenuFlyout>(Viewer.ContextFlyout);

        public MenuItem Item(string name) =>
            Flyout.Items.OfType<MenuItem>().Single(i => i.Classes.Contains($"markdown-context-{name}"));

        public void Dispose() => Window.Close();
    }

    private const string LinkParagraph = "Intro text\n\nRead [the docs](https://example.com/docs) now";

    private static Fixture Show(string markdown, Action<MarkdownViewer>? configure = null, Window? window = null)
    {
        var viewer = new MarkdownViewer();
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        window ??= new Window();
        window.Width = 400;
        window.Height = 300;
        window.Content = viewer;
        // The headless text drawing has no hit-testable glyphs; a transparent background stands in.
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        window.Show();
        return new Fixture(window, viewer);
    }

    private static Point At(Window window, TextBlock block, int index)
    {
        var caret = block.TextLayout.HitTestTextPosition(index);
        var origin = block.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(origin.X + block.Padding.Left + caret.X + 0.5, origin.Y + block.Padding.Top + caret.Y + caret.Height / 2);
    }

    private static void RightClick(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(MenuItem item)
    {
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    // ── Default and replacement ───────────────────────────────────────────────

    [AvaloniaFact]
    public void A_viewer_has_a_default_context_menu()
    {
        using var f = Show("Alpha");

        Assert.Equal(
            ["markdown-context-copy", "markdown-context-copy-markdown", "markdown-context-copy-link",
                "markdown-context-copy-heading-link", "markdown-context-copy-table", "markdown-context-copy-table-markdown",
                "markdown-context-save-image", "markdown-context-select-all"],
            f.Flyout.Items.OfType<MenuItem>().Select(i => i.Classes.Single(c => c.StartsWith("markdown-context-"))));
    }

    [AvaloniaFact]
    public void A_context_flyout_set_by_the_application_is_kept()
    {
        var own = new MenuFlyout();
        using var f = Show("Alpha", v => v.ContextFlyout = own);

        Assert.Same(own, f.Viewer.ContextFlyout);
    }

    [AvaloniaFact]
    public void A_context_flyout_set_to_null_removes_the_menu()
    {
        using var f = Show("Alpha", v => v.ContextFlyout = null);

        Assert.Null(f.Viewer.ContextFlyout);
    }

    [AvaloniaFact]
    public void A_context_menu_set_by_the_application_is_kept_without_a_flyout()
    {
        var own = new ContextMenu();
        using var f = Show("Alpha", v => v.ContextMenu = own);

        Assert.Same(own, f.Viewer.ContextMenu);
        Assert.Null(f.Viewer.ContextFlyout);
    }

    [AvaloniaFact]
    public void A_context_flyout_from_a_style_is_kept()
    {
        var own = new MenuFlyout();
        var window = new Window();
        window.Styles.Add(new Style(x => x.OfType<MarkdownViewer>())
        {
            Setters = { new Setter(Control.ContextFlyoutProperty, own) },
        });
        using var f = Show("Alpha", window: window);

        Assert.Same(own, f.Viewer.ContextFlyout);
    }

    [AvaloniaFact]
    public void A_later_local_value_replaces_the_default()
    {
        using var f = Show("Alpha");
        var own = new MenuFlyout();

        f.Viewer.ContextFlyout = own;

        Assert.Same(own, f.Viewer.ContextFlyout);
    }

    // ── Opening ───────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Right_clicking_opens_the_menu_with_english_labels_and_the_copy_items_disabled_without_a_selection()
    {
        using var f = Show(LinkParagraph);

        RightClick(f.Window, At(f.Window, f.Blocks[0], 2));

        Assert.True(f.Flyout.IsOpen);
        Assert.Equal(["Copy", "Copy as Markdown", "Copy link", "Copy link to heading", "Copy table", "Copy table as Markdown",
                "Save image as…", "Select all"],
            f.Flyout.Items.OfType<MenuItem>().Select(i => i.Header as string));
        Assert.False(f.Item("copy").IsEnabled);
        Assert.False(f.Item("copy-markdown").IsEnabled);
        Assert.False(f.Item("copy-link").IsVisible);
        Assert.False(f.Item("copy-heading-link").IsVisible);
        Assert.False(f.Item("copy-table").IsVisible);
        Assert.False(f.Item("copy-table-markdown").IsVisible);
        Assert.False(f.Item("save-image").IsVisible);
        Assert.True(f.Item("select-all").IsEnabled);
    }

    [AvaloniaFact]
    public void The_labels_come_from_the_resources()
    {
        using var f = Show(LinkParagraph);
        f.Window.Resources["StringTextFlyoutCopyText"] = "Copier";
        f.Window.Resources["MarkdownStringCopyAsMarkdown"] = "Copier en Markdown";
        f.Window.Resources["MarkdownStringCopyLink"] = "Copier le lien";
        f.Window.Resources["MarkdownStringCopyHeadingLink"] = "Copier le lien vers le titre";
        f.Window.Resources["MarkdownStringCopyTable"] = "Copier le tableau";
        f.Window.Resources["MarkdownStringCopyTableAsMarkdown"] = "Copier le tableau en Markdown";
        f.Window.Resources["MarkdownStringSaveImage"] = "Enregistrer l'image sous…";
        f.Window.Resources["MarkdownStringSelectAll"] = "Tout sélectionner";

        RightClick(f.Window, At(f.Window, f.Blocks[0], 2));

        Assert.Equal(["Copier", "Copier en Markdown", "Copier le lien", "Copier le lien vers le titre", "Copier le tableau", "Copier le tableau en Markdown",
                "Enregistrer l'image sous…", "Tout sélectionner"],
            f.Flyout.Items.OfType<MenuItem>().Select(i => i.Header as string));
    }

    [AvaloniaFact]
    public void The_menu_key_on_the_focused_viewer_opens_the_menu()
    {
        using var f = Show(LinkParagraph);
        f.Viewer.Focus();

        f.Window.KeyPressQwerty(PhysicalKey.ContextMenu, RawInputModifiers.None);
        f.Window.KeyReleaseQwerty(PhysicalKey.ContextMenu, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(f.Flyout.IsOpen);
        Assert.False(f.Item("copy-link").IsVisible);
    }

    [AvaloniaFact]
    public void The_copy_items_are_enabled_with_a_selection_and_show_their_hotkeys()
    {
        using var f = Show(LinkParagraph);
        f.Viewer.SelectAll();
        var hotkeys = Application.Current!.PlatformSettings!.HotkeyConfiguration;

        RightClick(f.Window, At(f.Window, f.Blocks[0], 2));

        Assert.True(f.Item("copy").IsEnabled);
        Assert.True(f.Item("copy-markdown").IsEnabled);
        Assert.Equal(hotkeys.Copy.First(), f.Item("copy").InputGesture);
        Assert.Equal(hotkeys.SelectAll.First(), f.Item("select-all").InputGesture);
    }

    [AvaloniaFact]
    public void Right_clicking_inside_the_selection_keeps_it()
    {
        using var f = Show(LinkParagraph);
        f.Viewer.SelectAll();

        RightClick(f.Window, At(f.Window, f.Blocks[0], 2));

        Assert.True(f.Viewer.HasSelection);
    }

    [AvaloniaFact]
    public void Right_clicking_text_outside_the_selection_clears_it()
    {
        using var f = Show(LinkParagraph);
        var layer = Assert.IsType<DocumentSelectionLayer>(Assert.IsType<Grid>(f.Viewer.Content).Children[1]);
        layer.SetSelectionForTest(0, 5);

        RightClick(f.Window, At(f.Window, f.Blocks[1], 2));

        Assert.False(f.Viewer.HasSelection);
    }

    // ── Items ─────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Copy_and_copy_as_markdown_copy_the_selection()
    {
        using var f = Show("Some **bold** text");
        f.Viewer.SelectAll();
        var origins = new List<MarkdownCopyOrigin>();
        f.Viewer.CopyingToClipboard += (_, e) => origins.Add(e.Origin);
        RightClick(f.Window, At(f.Window, f.Blocks[0], 2));

        Click(f.Item("copy"));
        Assert.Equal("Some bold text", await f.Window.Clipboard!.TryGetTextAsync());

        Click(f.Item("copy-markdown"));
        Assert.Equal("Some **bold** text", await f.Window.Clipboard!.TryGetTextAsync());

        Assert.Equal([MarkdownCopyOrigin.Selection, MarkdownCopyOrigin.SelectionAsMarkdown], origins);
    }

    [AvaloniaFact]
    public void Select_all_selects_the_document()
    {
        using var f = Show("Alpha\n\nBeta");
        RightClick(f.Window, At(f.Window, f.Blocks[0], 2));

        Click(f.Item("select-all"));

        Assert.Equal("Alpha\nBeta", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public async Task Right_clicking_a_link_offers_to_copy_its_address()
    {
        using var f = Show(LinkParagraph);
        var raised = new List<(MarkdownCopyOrigin, string)>();
        f.Viewer.CopyingToClipboard += (_, e) => raised.Add((e.Origin, e.Text));

        RightClick(f.Window, At(f.Window, f.Blocks[1], 7));
        Assert.True(f.Item("copy-link").IsVisible);
        Click(f.Item("copy-link"));

        Assert.Equal([(MarkdownCopyOrigin.Link, "https://example.com/docs")], raised);
        Assert.Equal("https://example.com/docs", await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaTheory]
    [InlineData(MouseButton.Right)]
    [InlineData(MouseButton.Middle)]
    public void Only_a_left_click_opens_a_link(MouseButton button)
    {
        using var f = Show(LinkParagraph);
        var clicked = new List<string>();
        f.Viewer.LinkClicked += (_, e) => clicked.Add(e.Url);
        var point = At(f.Window, f.Blocks[1], 7);

        f.Window.MouseDown(point, button);
        f.Window.MouseUp(point, button);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(clicked);

        f.Flyout.Hide();
        f.Window.MouseDown(point, MouseButton.Left);
        f.Window.MouseUp(point, MouseButton.Left);
        Assert.Equal(["https://example.com/docs"], clicked);
    }

    [AvaloniaFact]
    public void The_link_is_forgotten_once_the_menu_closes()
    {
        using var f = Show(LinkParagraph);
        RightClick(f.Window, At(f.Window, f.Blocks[1], 7));
        f.Flyout.Hide();

        RightClick(f.Window, At(f.Window, f.Blocks[0], 2));

        Assert.False(f.Item("copy-link").IsVisible);
    }

    [AvaloniaFact]
    public void The_menu_requested_on_a_focused_link_offers_to_copy_its_address()
    {
        using var f = Show(LinkParagraph);
        var target = f.Viewer.GetVisualDescendants().OfType<LinkFocusTarget>().Single();
        target.Focus();

        target.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        Dispatcher.UIThread.RunJobs();

        Assert.True(f.Flyout.IsOpen);
        Assert.True(f.Item("copy-link").IsVisible);
    }
}
