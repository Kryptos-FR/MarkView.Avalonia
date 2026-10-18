// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Streaming;

/// <summary>
/// What a streaming session keeps of the viewer's state from one update to the next: scroll position,
/// auto-scroll, selection, search, task toggles, focus, lightbox and the installed content.
/// </summary>
public class StreamingViewerBehaviourTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public ScrollViewer ScrollViewer => Viewer.GetVisualDescendants().OfType<ScrollViewer>().First();

        public DocumentSelectionLayer Layer =>
            Assert.IsType<DocumentSelectionLayer>(Assert.IsType<Grid>(Viewer.Content).Children[1]);

        public LinkFocusTarget[] Targets => Viewer.GetVisualDescendants().OfType<LinkFocusTarget>().ToArray();

        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

        public double Bottom => ScrollViewer.Extent.Height - ScrollViewer.Viewport.Height;

        public void Dispose() => Window.Close();
    }

    // Mutation testing compiles the library without its AXAML, so the viewer gets a minimal template with
    // the PART_ScrollViewer instead of the theme's.
    private static Fixture Show(string markdown, Action<MarkdownViewer>? configure = null)
    {
        var viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create() }.UseAlertBlocks();
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        return new Fixture(window, viewer);
    }

    private static string Paragraphs(string prefix, int count) =>
        string.Join("\n\n", Enumerable.Range(0, count).Select(i => $"{prefix} {i}"));

    private static void AssertAtBottom(Fixture f)
    {
        Assert.True(f.Bottom > 0, "the document must be taller than the viewport");
        Assert.InRange(f.ScrollViewer.Offset.Y, f.Bottom - 1, f.Bottom + 1);
    }

    // ── Scrolling ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Update_does_not_change_the_scroll_offset()
    {
        using var f = Show(Paragraphs("Paragraph", 40));
        await AsyncTestHelpers.PumpAsync();
        f.ScrollViewer.Offset = new Vector(0, 500);
        await AsyncTestHelpers.PumpAsync();
        Assert.Equal(500, f.ScrollViewer.Offset.Y);
        var session = f.Viewer.BeginStreaming();

        session.Append("\n\n" + Paragraphs("More", 5));
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();

        Assert.Equal(500, f.ScrollViewer.Offset.Y);

        session.Append("\n\n" + Paragraphs("Again", 5));
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();

        Assert.Equal(500, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public async Task AutoScrollToEnd_follows_new_content()
    {
        using var f = Show("", v => v.AutoScrollToEnd = true);
        var session = f.Viewer.BeginStreaming();

        session.Append(Paragraphs("Paragraph", 40));
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();

        AssertAtBottom(f);

        var previousBottom = f.Bottom;
        session.Append("\n\n" + Paragraphs("More", 10));
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();

        Assert.True(f.Bottom > previousBottom);
        AssertAtBottom(f);
    }

    [AvaloniaFact]
    public void AutoScrollToEnd_defaults_to_false()
    {
        Assert.False(new MarkdownViewer().AutoScrollToEnd);
    }

    [AvaloniaFact]
    public async Task AutoScrollToEnd_off_does_not_scroll()
    {
        using var f = Show("", v => v.AutoScrollToEnd = false);
        var session = f.Viewer.BeginStreaming();

        session.Append(Paragraphs("Paragraph", 40));
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();

        Assert.True(f.Bottom > 0);
        Assert.Equal(0, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public async Task AutoScrollToEnd_pauses_when_the_user_scrolls_up()
    {
        using var f = Show("", v => v.AutoScrollToEnd = true);
        var session = f.Viewer.BeginStreaming();
        session.Append(Paragraphs("Paragraph", 40));
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();
        AssertAtBottom(f);

        f.ScrollViewer.Offset = new Vector(0, 0);
        await AsyncTestHelpers.PumpAsync();
        session.Append("\n\n" + Paragraphs("More", 10));
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();

        Assert.Equal(0, f.ScrollViewer.Offset.Y);
    }

    [AvaloniaFact]
    public async Task AutoScrollToEnd_resumes_at_the_bottom()
    {
        using var f = Show("", v => v.AutoScrollToEnd = true);
        var session = f.Viewer.BeginStreaming();
        session.Append(Paragraphs("Paragraph", 40));
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();
        f.ScrollViewer.Offset = new Vector(0, 0);
        await AsyncTestHelpers.PumpAsync();
        session.Append("\n\n" + Paragraphs("More", 10));
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();
        Assert.Equal(0, f.ScrollViewer.Offset.Y);

        f.ScrollViewer.ScrollToEnd();
        await AsyncTestHelpers.PumpAsync();
        var previousBottom = f.Bottom;
        session.Append("\n\n" + Paragraphs("Again", 10));
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();

        Assert.True(f.Bottom > previousBottom);
        AssertAtBottom(f);
    }

    [AvaloniaFact]
    public async Task Following_restarts_with_each_session()
    {
        using var f = Show("", v => v.AutoScrollToEnd = true);
        var first = f.Viewer.BeginStreaming();
        first.Append(Paragraphs("Paragraph", 40));
        first.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();
        f.ScrollViewer.Offset = new Vector(0, 0);
        await AsyncTestHelpers.PumpAsync();
        first.Complete();
        await AsyncTestHelpers.PumpAsync();
        Assert.Equal(0, f.ScrollViewer.Offset.Y);

        var second = f.Viewer.BeginStreaming();
        second.Append("\n\n" + Paragraphs("More", 10));
        second.ApplyPendingUpdate();
        await AsyncTestHelpers.PumpAsync();

        AssertAtBottom(f);
    }

    // ── Selection and search ──────────────────────────────────────────────────

    [AvaloniaFact]
    public void Selection_in_kept_text_survives_an_update()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a\n\nb");
        session.ApplyPendingUpdate();
        var layer = f.Layer;
        layer.SetSelectionForTest(0, 1);
        Assert.Equal("a", f.Viewer.GetSelectedText());

        session.Append(" more");
        session.ApplyPendingUpdate();

        Assert.Same(layer, f.Layer);
        Assert.Equal("a", f.Viewer.GetSelectedText());
        Assert.Equal((0, 1), layer.SelectionForTest);
    }

    [AvaloniaFact]
    public void Selection_into_the_tail_is_clamped()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a\n\nb");
        session.ApplyPendingUpdate();
        f.Layer.SetSelectionForTest(0, 3);
        Assert.Equal("a\nb", f.Viewer.GetSelectedText());

        session.Append("c");
        session.ApplyPendingUpdate();

        Assert.Equal("a", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Selection_in_the_tail_is_cleared()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a\n\nb");
        session.ApplyPendingUpdate();
        f.Layer.SetSelectionForTest(2, 3);
        Assert.Equal("b", f.Viewer.GetSelectedText());

        session.Append("c");
        session.ApplyPendingUpdate();

        Assert.Equal("", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Find_matches_follow_the_tail()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("x\n\nx");
        session.ApplyPendingUpdate();
        Assert.Equal(2, f.Viewer.Find("x"));
        f.Viewer.FindNext();
        f.Viewer.FindNext();
        Assert.Equal(1, f.Viewer.CurrentFindMatchIndex);

        session.Append("\n\nx");
        session.ApplyPendingUpdate();

        Assert.Equal(3, f.Viewer.FindMatchCount);
        Assert.Equal(1, f.Viewer.CurrentFindMatchIndex);
        Assert.Equal(3, f.Layer.FindMatches.Count);
    }

    // ── Task lists ────────────────────────────────────────────────────────────

    private static ToggleButton[] Toggles(MarkdownViewer viewer) =>
        Assert.IsType<Grid>(viewer.Content).GetLogicalDescendants().OfType<ToggleButton>().ToArray();

    [AvaloniaFact]
    public void Task_toggle_survives_the_next_update()
    {
        using var f = Show("- [ ] a", v => v.AreTaskListsInteractive = true);
        var session = f.Viewer.BeginStreaming();

        Toggles(f.Viewer)[0].IsChecked = true;
        session.Append("\n- [ ] b");
        session.ApplyPendingUpdate();

        Assert.Equal("- [x] a\n- [ ] b", f.Viewer.Markdown);
        Assert.Equal([true, false], Toggles(f.Viewer).Select(t => t.IsChecked));
    }

    [AvaloniaFact]
    public void Task_toggle_during_a_session_survives_the_next_update()
    {
        using var f = Show("", v => v.AreTaskListsInteractive = true);
        var session = f.Viewer.BeginStreaming();
        session.Append("- [ ] a\n\nnext");
        session.ApplyPendingUpdate();

        Toggles(f.Viewer)[0].IsChecked = true;
        session.Append(" more");
        session.ApplyPendingUpdate();

        Assert.Equal("- [x] a\n\nnext more", f.Viewer.Markdown);
        Assert.Equal([true], Toggles(f.Viewer).Select(t => t.IsChecked));
    }

    [AvaloniaFact]
    public void Task_toggle_after_sized_image_survives_next_update()
    {
        using var f = Show("![i](p.png =10x10)\n\n- [ ] a", v => v.AreTaskListsInteractive = true);
        var session = f.Viewer.BeginStreaming();

        Toggles(f.Viewer)[0].IsChecked = true;
        session.Append("\n- [ ] b");
        session.ApplyPendingUpdate();

        Assert.Equal("![i](p.png =10x10)\n\n- [x] a\n- [ ] b", f.Viewer.Markdown);
    }

    // ── Focus ─────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Focus_returns_to_the_viewer_when_its_block_is_discarded()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("[a](x)\n\n[b](y)");
        session.ApplyPendingUpdate();
        f.Window.UpdateLayout();
        Assert.Equal(2, f.Targets.Length);
        Assert.True(f.Targets[1].Focus());
        Assert.Same(f.Targets[1], f.Focused);

        session.Append(" tail");
        session.ApplyPendingUpdate();

        Assert.True(f.Viewer.IsFocused);
        Assert.Same(f.Viewer, f.Focused);
    }

    [AvaloniaFact]
    public void Focus_in_a_kept_block_is_untouched()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("[a](x)\n\n[b](y)");
        session.ApplyPendingUpdate();
        f.Window.UpdateLayout();
        var target = f.Targets[0];
        Assert.True(target.Focus());

        session.Append(" tail");
        session.ApplyPendingUpdate();

        Assert.Same(target, f.Focused);
        Assert.True(target.IsFocused);
        Assert.False(f.Viewer.IsFocused);
    }

    // ── Screen readers ────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Complete_raises_children_changed_on_the_peer()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a");
        session.ApplyPendingUpdate();
        var peer = ControlAutomationPeer.CreatePeerForElement(f.Viewer);
        var changes = 0;
        peer.ChildrenChanged += (_, _) => changes++;

        session.Append("\n\nb");
        session.ApplyPendingUpdate();

        Assert.Equal(0, changes);

        session.Complete();

        Assert.Equal(1, changes);
    }

    [AvaloniaFact]
    public void A_session_ended_by_a_direct_set_raises_no_children_changed()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a");
        session.ApplyPendingUpdate();
        var peer = ControlAutomationPeer.CreatePeerForElement(f.Viewer);
        var changes = 0;
        peer.ChildrenChanged += (_, _) => changes++;

        f.Viewer.Markdown = "other";

        Assert.True(session.IsCompleted);
        Assert.Equal(0, changes);
    }

    // ── Lightbox and content ──────────────────────────────────────────────────

    /// <summary>An image of a fixed size that draws a transparent rectangle, so it can be hit.</summary>
    private sealed class FakeImage(double width, double height) : IImage
    {
        public Size Size { get; } = new(width, height);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) => context.FillRectangle(Brushes.Transparent, destRect);
    }

    private sealed class FakeLoader(IImage image) : IImageLoader
    {
        public bool CanLoad(string url) => url.StartsWith("fake:", StringComparison.Ordinal);
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult<IImage?>(image);
    }

    private sealed class LoaderExtension(IImageLoader loader) : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, loader);
    }

    [AvaloniaFact]
    public async Task Lightbox_stays_open_across_an_update()
    {
        using var f = Show("", v => v.Extensions.Add(new LoaderExtension(new FakeLoader(new FakeImage(200, 100)))));
        // The headless text drawing contributes no hit-testable glyph geometry; a transparent
        // background makes each paragraph's whole row hit-testable instead.
        f.Window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        var session = f.Viewer.BeginStreaming();
        session.Append($"![A diagram](fake://{Guid.NewGuid()}.png)\n\nintro");
        session.ApplyPendingUpdate();
        Image ImageControl() => f.Viewer.GetVisualDescendants().OfType<Image>().First(i => i.Classes.Contains("markdown-image"));
        await AsyncTestHelpers.WaitUntilAsync(() => ImageControl().Source is not null);
        f.Window.UpdateLayout();
        var image = ImageControl();
        var center = image.TranslatePoint(new Point(image.Bounds.Width / 2, image.Bounds.Height / 2), f.Window)!.Value;
        f.Window.MouseDown(center, MouseButton.Left);
        f.Window.MouseUp(center, MouseButton.Left);
        var lightbox = Assert.IsType<ImageLightbox>(f.Viewer.Lightbox);

        session.Append(" more");
        session.ApplyPendingUpdate();

        Assert.Same(lightbox, f.Viewer.Lightbox);
        Assert.Same(image, ImageControl());
    }

    [AvaloniaFact]
    public void Content_is_installed_once_per_session()
    {
        using var f = Show("before");
        var direct = f.Viewer.Content;
        var first = f.Viewer.BeginStreaming();
        first.Append("\n\na");
        first.ApplyPendingUpdate();
        var grid = Assert.IsType<Grid>(f.Viewer.Content);
        Assert.NotSame(direct, grid);

        first.Append(" b");
        first.ApplyPendingUpdate();
        Assert.Same(grid, f.Viewer.Content);
        first.Complete();
        Assert.Same(grid, f.Viewer.Content);

        var second = f.Viewer.BeginStreaming();
        second.Append(" c");
        second.ApplyPendingUpdate();

        Assert.NotSame(grid, f.Viewer.Content);
        Assert.IsType<Grid>(f.Viewer.Content);
    }
}
