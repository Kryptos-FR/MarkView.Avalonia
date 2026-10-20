// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>The context menu's Copy table and Copy table as Markdown items.</summary>
public class MarkdownViewerTableCopyTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public MenuFlyout Flyout => Assert.IsType<MenuFlyout>(Viewer.ContextFlyout);

        public MenuItem Item(string name) =>
            Flyout.Items.OfType<MenuItem>().Single(i => i.Classes.Contains($"markdown-context-{name}"));

        public Grid Table => Viewer.GetLogicalDescendants().OfType<Grid>().First(g => g.Classes.Contains("markdown-table"));

        public Border Cell(int row, int column) =>
            Table.Children.OfType<Border>().Single(c => Grid.GetRow(c) == row && Grid.GetColumn(c) == column);

        public List<CopyingToClipboardEventArgs> Copies { get; } = [];

        public void Dispose() => Window.Close();
    }

    private const string Table = """
        Intro paragraph

        | Name | Role |
        |:-----|-----:|
        | Ada | **Engineer** |
        | Grace | `Admiral` |

        Outro paragraph
        """;

    private static Fixture Show(string markdown, Action<MarkdownViewer>? configure = null)
    {
        // Transparent, as the default theme makes it, so that a click outside the text reaches the viewer.
        var viewer = new MarkdownViewer { Background = Brushes.Transparent };
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 500, Height = 400, Content = viewer };
        // The headless text drawing has no hit-testable glyphs; a transparent background stands in.
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        window.Show();
        var fixture = new Fixture(window, viewer);
        viewer.CopyingToClipboard += (_, e) => fixture.Copies.Add(e);
        return fixture;
    }

    private static Point Center(Window window, Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

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

    private static MarkdownSelectableTextBlock Paragraph(Fixture f, string text) =>
        f.Viewer.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>()
            .First(t => MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!) == text);

    // ── Visibility ────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Right_clicking_a_table_offers_to_copy_it()
    {
        using var f = Show(Table);

        RightClick(f.Window, Center(f.Window, f.Cell(1, 0)));

        Assert.True(f.Flyout.IsOpen);
        Assert.True(f.Item("copy-table").IsVisible);
        Assert.True(f.Item("copy-table-markdown").IsVisible);
        Assert.Equal("Copy table", f.Item("copy-table").Header);
        Assert.Equal("Copy table as Markdown", f.Item("copy-table-markdown").Header);
    }

    [AvaloniaFact]
    public void Right_clicking_between_the_cells_text_still_offers_to_copy_the_table()
    {
        using var f = Show(Table);
        var cell = f.Cell(1, 1);
        // The right edge of a right-aligned cell is outside its text when the text is short.
        var point = cell.TranslatePoint(new Point(2, cell.Bounds.Height / 2), f.Window)!.Value;

        RightClick(f.Window, point);

        Assert.True(f.Item("copy-table").IsVisible);
    }

    [AvaloniaFact]
    public void Right_clicking_outside_a_table_hides_the_table_items()
    {
        using var f = Show(Table);

        RightClick(f.Window, Center(f.Window, Paragraph(f, "Intro paragraph")));

        Assert.False(f.Item("copy-table").IsVisible);
        Assert.False(f.Item("copy-table-markdown").IsVisible);
    }

    [AvaloniaFact]
    public void The_table_is_forgotten_once_the_menu_closes()
    {
        using var f = Show(Table);
        RightClick(f.Window, Center(f.Window, f.Cell(1, 0)));
        f.Flyout.Hide();

        RightClick(f.Window, Center(f.Window, Paragraph(f, "Outro paragraph")));

        Assert.False(f.Item("copy-table").IsVisible);
    }

    [AvaloniaFact]
    public void The_menu_requested_on_a_focused_link_in_a_table_offers_to_copy_the_table()
    {
        using var f = Show("| Site |\n|---|\n| [docs](https://example.com) |");
        var target = f.Viewer.GetVisualDescendants().OfType<LinkFocusTarget>().Single();
        target.Focus();

        target.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        Dispatcher.UIThread.RunJobs();

        Assert.True(f.Item("copy-link").IsVisible);
        Assert.True(f.Item("copy-table").IsVisible);
    }

    [AvaloniaFact]
    public void The_menu_requested_on_a_focused_link_outside_a_table_hides_the_table_items()
    {
        using var f = Show("[docs](https://example.com)\n\n| A |\n|---|\n| 1 |");
        var target = f.Viewer.GetVisualDescendants().OfType<LinkFocusTarget>().Single();
        target.Focus();

        target.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        Dispatcher.UIThread.RunJobs();

        Assert.False(f.Item("copy-table").IsVisible);
    }

    private sealed class FakeImage : IImage
    {
        public Size Size { get; } = new(100, 50);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) => context.FillRectangle(Brushes.Transparent, destRect);
    }

    private sealed class FakeLoader : IImageLoader, IMarkViewExtension
    {
        public bool CanLoad(string url) => url.StartsWith("fake:", StringComparison.Ordinal);
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult<IImage?>(new FakeImage());
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, this);
    }

    [AvaloniaFact]
    public async Task A_context_menu_over_the_lightbox_offers_no_table_items()
    {
        using var f = Show($"| Picture |\n|---|\n| ![logo](fake://{Guid.NewGuid()}.png) |", v => v.Extensions.Add(new FakeLoader()));
        var image = f.Viewer.GetVisualDescendants().OfType<Image>().First(i => i.Classes.Contains("markdown-image"));
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        f.Window.UpdateLayout();
        var point = Center(f.Window, image);
        f.Window.MouseDown(point, MouseButton.Left);
        f.Window.MouseUp(point, MouseButton.Left);
        Assert.NotNull(f.Viewer.Lightbox);

        RightClick(f.Window, point);

        Assert.False(f.Item("copy-table").IsVisible);
    }

    // ── Copy table ────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Copy_table_copies_tab_separated_values_and_an_html_table()
    {
        using var f = Show(Table);
        RightClick(f.Window, Center(f.Window, f.Cell(2, 1)));

        Click(f.Item("copy-table"));

        var copy = Assert.Single(f.Copies);
        Assert.Equal(MarkdownCopyOrigin.Table, copy.Origin);
        Assert.Equal("Name\tRole\nAda\tEngineer\nGrace\tAdmiral", copy.Text);
        Assert.Equal("Name\tRole\nAda\tEngineer\nGrace\tAdmiral".ReplaceLineEndings(), await f.Window.Clipboard!.TryGetTextAsync());
        if (HtmlClipboardFormat.IsSupported)
        {
            Assert.NotNull(copy.Html);
            Assert.Contains("<table>", copy.Html);
            Assert.Contains("<strong>Engineer</strong>", copy.Html);
            Assert.Contains("<code>Admiral</code>", copy.Html);
            Assert.DoesNotContain("Intro", copy.Html);
        }
    }

    [AvaloniaFact]
    public void Copy_table_writes_no_html_when_html_copy_is_off()
    {
        using var f = Show(Table, v => v.IsHtmlCopyEnabled = false);
        RightClick(f.Window, Center(f.Window, f.Cell(1, 0)));

        Click(f.Item("copy-table"));

        Assert.Null(Assert.Single(f.Copies).Html);
    }

    [AvaloniaFact]
    public void Copy_table_ignores_the_selection_and_keeps_it()
    {
        using var f = Show(Table);
        f.Viewer.SelectAll();
        var selected = f.Viewer.SelectedText;
        RightClick(f.Window, Center(f.Window, f.Cell(1, 0)));

        Click(f.Item("copy-table"));

        Assert.Equal("Name\tRole\nAda\tEngineer\nGrace\tAdmiral", Assert.Single(f.Copies).Text);
        Assert.Equal(selected, f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void A_cell_break_or_tab_becomes_a_space_and_an_image_its_alt_text()
    {
        using var f = Show("| A | B |\n|---|---|\n| one<br>two | ![logo](missing.png) x |");
        RightClick(f.Window, Center(f.Window, f.Cell(1, 0)));

        Click(f.Item("copy-table"));

        Assert.Equal("A\tB\none two\tlogo x", Assert.Single(f.Copies).Text);
    }

    [AvaloniaFact]
    public void An_empty_cell_is_an_empty_value()
    {
        using var f = Show("| A | B | C |\n|---|---|---|\n| 1 |  | 3 |");
        RightClick(f.Window, Center(f.Window, f.Cell(1, 0)));

        Click(f.Item("copy-table"));

        Assert.Equal("A\tB\tC\n1\t\t3", Assert.Single(f.Copies).Text);
    }

    [AvaloniaFact]
    public void A_cell_spanning_columns_is_followed_by_empty_ones()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGridTables().Build();
        using var f = Show("""
            +-----+-----+-----+
            | A   | B   | C   |
            +=====+=====+=====+
            | Wide      | c   |
            +-----+-----+-----+
            """, v => v.Pipeline = pipeline);
        RightClick(f.Window, Center(f.Window, f.Table));

        Click(f.Item("copy-table"));

        Assert.Equal("A\tB\tC\nWide\t\tc", Assert.Single(f.Copies).Text);
    }

    [AvaloniaFact]
    public async Task A_cancelled_copy_writes_nothing()
    {
        using var f = Show(Table);
        await f.Window.Clipboard!.SetTextAsync("before");
        f.Viewer.CopyingToClipboard += (_, e) => e.Cancel = true;
        RightClick(f.Window, Center(f.Window, f.Cell(1, 0)));

        Click(f.Item("copy-table"));

        Assert.Equal("before", await f.Window.Clipboard!.TryGetTextAsync());
    }

    // ── Copy table as Markdown ────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Copy_table_as_markdown_copies_a_top_level_table_as_its_source()
    {
        using var f = Show(Table);
        RightClick(f.Window, Center(f.Window, f.Cell(1, 0)));

        Click(f.Item("copy-table-markdown"));

        const string source = "| Name | Role |\n|:-----|-----:|\n| Ada | **Engineer** |\n| Grace | `Admiral` |";
        var copy = Assert.Single(f.Copies);
        Assert.Equal(MarkdownCopyOrigin.TableAsMarkdown, copy.Origin);
        Assert.Equal(source, copy.Text);
        Assert.Null(copy.Html);
        Assert.Equal(source.ReplaceLineEndings(), await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public void Copy_table_as_markdown_rebuilds_a_table_inside_a_quote_without_the_quote()
    {
        using var f = Show("> | A | B |\n> |---|---|\n> | *x* | 2 |");
        RightClick(f.Window, Center(f.Window, f.Cell(1, 0)));

        Click(f.Item("copy-table-markdown"));

        Assert.Equal("| A | B |\n| --- | --- |\n| *x* | 2 |", Assert.Single(f.Copies).Text);
    }

    [AvaloniaFact]
    public void The_html_of_a_table_inside_a_quote_is_the_table_alone()
    {
        if (!HtmlClipboardFormat.IsSupported) return;
        using var f = Show("> Quoted\n>\n> | A |\n> |---|\n> | 1 |");
        RightClick(f.Window, Center(f.Window, f.Cell(1, 0)));

        Click(f.Item("copy-table"));

        var html = Assert.Single(f.Copies).Html!;
        Assert.StartsWith("<table>", html);
        Assert.DoesNotContain("blockquote", html);
    }
}
