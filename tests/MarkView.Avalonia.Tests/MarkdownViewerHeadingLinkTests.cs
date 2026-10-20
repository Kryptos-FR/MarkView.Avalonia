// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

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

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>The context menu's Copy link to heading item.</summary>
public class MarkdownViewerHeadingLinkTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public MenuFlyout Flyout => Assert.IsType<MenuFlyout>(Viewer.ContextFlyout);

        public MenuItem Item => Flyout.Items.OfType<MenuItem>().Single(i => i.Classes.Contains("markdown-context-copy-heading-link"));

        public List<CopyingToClipboardEventArgs> Copies { get; } = [];

        public MarkdownSelectableTextBlock Block(string text) =>
            Viewer.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>()
                .First(t => MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!) == text);

        public void Dispose() => Window.Close();
    }

    private const string Document = """
        # Title

        Intro paragraph

        ## Second heading

        Body text[^1]

        [^1]: A footnote
        """;

    private static Fixture Show(Action<MarkdownViewer> configure)
    {
        // Transparent, as the default theme makes it, so that a click outside the text reaches the viewer.
        var viewer = new MarkdownViewer { Background = Brushes.Transparent };
        configure(viewer);
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

    private static Fixture Show(string markdown = Document) => Show(v => v.Markdown = markdown);

    private static void RightClick(Fixture f, Control control, double x = 4)
    {
        var point = control.TranslatePoint(new Point(x, control.Bounds.Height / 2), f.Window)!.Value;
        f.Window.MouseDown(point, MouseButton.Right);
        f.Window.MouseUp(point, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(MenuItem item)
    {
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Right_clicking_a_heading_copies_a_link_to_its_anchor()
    {
        using var f = Show();

        RightClick(f, f.Block("Second heading"));
        Assert.True(f.Flyout.IsOpen);
        Assert.True(f.Item.IsVisible);
        Assert.Equal("Copy link to heading", f.Item.Header);
        Click(f.Item);

        var copy = Assert.Single(f.Copies);
        Assert.Equal(MarkdownCopyOrigin.HeadingLink, copy.Origin);
        Assert.Equal("#second-heading", copy.Text);
        Assert.Null(copy.Html);
        Assert.Equal("#second-heading", await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public void Right_clicking_after_a_short_heading_text_still_offers_the_link()
    {
        using var f = Show();
        var heading = f.Block("Title");

        RightClick(f, heading, heading.Bounds.Width - 2);
        Click(f.Item);

        Assert.Equal("#title", Assert.Single(f.Copies).Text);
    }

    [AvaloniaTheory]
    [InlineData("Intro paragraph")]
    [InlineData("A footnote")]
    public void Right_clicking_something_else_hides_the_item(string text)
    {
        using var f = Show();

        RightClick(f, f.Block(text));

        Assert.True(f.Flyout.IsOpen);
        Assert.False(f.Item.IsVisible);
    }

    [AvaloniaFact]
    public void The_heading_is_forgotten_when_the_menu_closes()
    {
        using var f = Show();
        RightClick(f, f.Block("Title"));
        f.Flyout.Hide();
        Dispatcher.UIThread.RunJobs();

        Click(f.Item);

        Assert.Empty(f.Copies);
    }

    [AvaloniaFact]
    public void The_menu_key_hides_the_item()
    {
        using var f = Show();
        RightClick(f, f.Block("Title"));
        f.Flyout.Hide();
        f.Viewer.Focus();

        f.Window.KeyPressQwerty(PhysicalKey.ContextMenu, RawInputModifiers.None);
        f.Window.KeyReleaseQwerty(PhysicalKey.ContextMenu, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(f.Flyout.IsOpen);
        Assert.False(f.Item.IsVisible);
    }

    [AvaloniaFact]
    public void A_heading_hidden_in_a_folded_block_is_not_offered()
    {
        using var f = Show("<details open>\n<summary>More</summary>\n\n## Hidden\n\n</details>\n\nAfter");
        var heading = f.Block("Hidden");
        var point = heading.TranslatePoint(new Point(4, heading.Bounds.Height / 2), f.Window)!.Value;
        var summary = f.Viewer.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-details-summary"));
        var toggle = summary.TranslatePoint(new Point(4, summary.Bounds.Height / 2), f.Window)!.Value;
        f.Window.MouseDown(toggle, MouseButton.Left);
        f.Window.MouseUp(toggle, MouseButton.Left);
        f.Window.UpdateLayout();
        Assert.False(heading.IsEffectivelyVisible);

        f.Window.MouseDown(point, MouseButton.Right);
        f.Window.MouseUp(point, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();

        Assert.True(f.Flyout.IsOpen);
        Assert.False(f.Item.IsVisible);
    }

    [AvaloniaFact]
    public async Task A_document_loaded_from_a_file_gives_the_file_address_with_the_anchor()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.md");
        await File.WriteAllTextAsync(path, Document);
        try
        {
            var source = new Uri(path);
            using var f = Show(v => v.Source = source);
            await AsyncTestHelpers.WaitUntilAsync(() => f.Viewer.AnchorIds.Contains("second-heading"));
            f.Window.UpdateLayout();

            RightClick(f, f.Block("Second heading"));
            Click(f.Item);

            Assert.Equal($"{source.AbsoluteUri}#second-heading", Assert.Single(f.Copies).Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task A_document_loaded_from_the_web_keeps_its_query_and_replaces_its_fragment()
    {
        using var server = new LoopbackHttpServer(new Dictionary<string, Action<System.Net.HttpListenerResponse>>
        {
            ["/docs/page.md"] = LoopbackHttpServer.Content("text/markdown", Encoding.UTF8.GetBytes(Document)),
        });
        using var f = Show(v => v.Source = new Uri(server.Url("/docs/page.md?v=2#title")));
        await AsyncTestHelpers.WaitUntilAsync(() => f.Viewer.AnchorIds.Contains("second-heading"));
        f.Window.UpdateLayout();

        RightClick(f, f.Block("Second heading"));
        Click(f.Item);

        Assert.Equal(server.Url("/docs/page.md?v=2") + "#second-heading", Assert.Single(f.Copies).Text);
    }

    [AvaloniaFact]
    public async Task A_document_from_the_application_resources_gives_the_anchor_alone()
    {
        using var f = Show(v => v.Source = new Uri("avares://MarkView.Avalonia.Tests/TestAssets/test.md"));
        await AsyncTestHelpers.WaitUntilAsync(() => f.Viewer.AnchorIds.Any());
        f.Window.UpdateLayout();
        var heading = f.Viewer.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>()
            .First(c => c.Classes.Contains("markdown-h1"));

        RightClick(f, heading);
        Click(f.Item);

        Assert.Equal($"#{heading.Tag}", Assert.Single(f.Copies).Text);
    }
}
