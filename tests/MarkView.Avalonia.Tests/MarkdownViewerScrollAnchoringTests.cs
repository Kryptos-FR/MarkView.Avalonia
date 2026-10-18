// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// Content that grows above the visible part of the document (an image loading, a formula rendered once
/// near) moves the scroll offset with it, so the text in view stays where it is.
/// </summary>
public class MarkdownViewerScrollAnchoringTests
{
    private sealed class TallImage : IImage
    {
        public Size Size => new(20, 200);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
    }

    private sealed class TallImageLoader : IImageLoader
    {
        public bool CanLoad(string url) => url.StartsWith("tall:", StringComparison.Ordinal);

        // Completes on a later dispatcher job, as a download does, not during the layout pass that starts it.
        public async Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return new TallImage();
        }
    }

    private sealed class LoaderExtension(IImageLoader loader) : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, loader);
    }

    private static string Paragraphs(string prefix, int count) =>
        string.Join("\n\n", Enumerable.Range(0, count).Select(i => $"{prefix} {i}"));

    private sealed class Fixture : IDisposable
    {
        public MarkdownViewer Viewer { get; }
        public Window Window { get; }

        public Fixture(string markdown)
        {
            Viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create() };
            Viewer.Extensions.Add(new LoaderExtension(new TallImageLoader()));
            Viewer.Markdown = markdown;
            Window = new Window { Width = 400, Height = 300, Content = Viewer };
            Window.Show();
        }

        public ScrollViewer ScrollViewer => Viewer.GetVisualDescendants().OfType<ScrollViewer>().First();

        public StackPanel Root => Assert.IsType<StackPanel>(Assert.IsType<Grid>(Viewer.Content).Children[0]);

        public double Top(Control control) => control.TranslatePoint(default, ScrollViewer)!.Value.Y;

        public TextBlock Paragraph(string text) =>
            Root.Children.OfType<TextBlock>().Single(t => MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!) == text);

        public Control FirstInView() => Root.Children.First(c => Top(c) >= 0);

        public void ScrollTo(double y)
        {
            ScrollViewer.Offset = new Vector(0, y);
            Settle();
        }

        // A layout pass that moves the offset to keep the anchor queues another one, which runs before
        // the next frame is drawn; a test runs it by hand.
        public void Settle()
        {
            Window.UpdateLayout();
            Window.UpdateLayout();
        }

        public void Dispose() => Window.Close();
    }

    [AvaloniaFact]
    public void Block_growing_above_the_visible_area_does_not_move_the_text_in_view()
    {
        using var f = new Fixture(Paragraphs("Paragraph", 60));
        f.ScrollTo(600);
        var inView = f.FirstInView();
        var top = f.Top(inView);
        var offset = f.ScrollViewer.Offset.Y;

        f.Root.Children[1].MinHeight = 250;
        f.Settle();

        Assert.Equal(top, f.Top(inView), 0.5);
        Assert.True(f.ScrollViewer.Offset.Y > offset + 200);
    }

    [AvaloniaFact]
    public void Block_growing_in_view_below_the_first_block_in_view_pushes_what_follows()
    {
        using var f = new Fixture(Paragraphs("Paragraph", 60));
        f.ScrollTo(600);
        var inView = f.FirstInView();
        var next = f.Root.Children[f.Root.Children.IndexOf(inView) + 1];
        var top = f.Top(inView);
        var nextTop = f.Top(next);

        inView.MinHeight = 100;
        f.Settle();

        Assert.Equal(top, f.Top(inView), 0.5);
        Assert.True(f.Top(next) > nextTop + 50);
    }

    [AvaloniaFact]
    public void Block_growing_above_the_view_while_it_jumps_away_does_not_move_the_new_view()
    {
        using var f = new Fixture(Paragraphs("Paragraph", 200));
        f.ScrollTo(600);

        // The anchor chosen at 600 is pushed down, but is far out of the view the document jumped to.
        f.Root.Children[1].MinHeight = 250;
        f.ScrollTo(3000);
        f.Settle();

        Assert.Equal(3000, f.ScrollViewer.Offset.Y, 0.5);
    }

    [AvaloniaFact]
    public void Block_growing_above_the_view_of_a_new_document_does_not_move_the_text_in_view()
    {
        using var f = new Fixture(Paragraphs("Old", 60));
        f.ScrollTo(600);
        f.Viewer.Markdown = Paragraphs("New", 60);
        f.ScrollTo(600);
        var inView = f.FirstInView();
        var top = f.Top(inView);

        f.Root.Children[1].MinHeight = 250;
        f.Settle();

        Assert.Equal(top, f.Top(inView), 0.5);
    }

    [AvaloniaFact]
    public async Task Images_loading_above_a_heading_scrolled_to_leave_it_at_the_top()
    {
        var images = string.Join("\n\n", Enumerable.Range(0, 3).Select(_ => $"![tall](tall://{Guid.NewGuid()}.png)"));
        using var f = new Fixture($"{Paragraphs("Before", 60)}\n\n{images}\n\n# Target\n\n{Paragraphs("After", 60)}");
        var heading = f.Root.Children.OfType<Control>().Single(c => c.Classes.Contains("markdown-h1"));
        var loaded = f.Viewer.GetVisualDescendants().OfType<Image>().ToArray();
        Assert.All(loaded, image => Assert.Null(image.Source));

        f.Viewer.ScrollToAnchor("target");
        f.Window.UpdateLayout();
        await AsyncTestHelpers.WaitUntilAsync(() => loaded.All(image => image.Source is not null));
        f.Settle();

        Assert.Equal(16, f.Top(heading), 0.5);
    }

    [AvaloniaFact]
    public async Task Images_loading_above_a_find_match_leave_it_where_it_was_shown()
    {
        var images = string.Join("\n\n", Enumerable.Range(0, 3).Select(_ => $"![tall](tall://{Guid.NewGuid()}.png)"));
        using var f = new Fixture($"{Paragraphs("Before", 60)}\n\n{images}\n\nNeedle\n\n{Paragraphs("After", 60)}");
        var match = f.Paragraph("Needle");
        var loaded = f.Viewer.GetVisualDescendants().OfType<Image>().ToArray();

        f.Viewer.Find("Needle");
        f.Viewer.FindNext();
        f.Window.UpdateLayout();
        var top = f.Top(match);
        await AsyncTestHelpers.WaitUntilAsync(() => loaded.All(image => image.Source is not null));
        f.Settle();

        Assert.Equal(top, f.Top(match), 0.5);
    }

    [AvaloniaFact]
    public async Task Scrolling_after_jumping_to_a_heading_anchors_the_text_then_in_view()
    {
        var images = string.Join("\n\n", Enumerable.Range(0, 3).Select(_ => $"![tall](tall://{Guid.NewGuid()}.png)"));
        using var f = new Fixture($"# Target\n\n{Paragraphs("Middle", 60)}\n\n{images}\n\n{Paragraphs("After", 60)}");
        f.Viewer.ScrollToAnchor("target");
        f.Window.UpdateLayout();
        var loaded = f.Viewer.GetVisualDescendants().OfType<Image>().ToArray();

        // Scrolled to just below the images, which then load above the view.
        var paragraph = f.Paragraph("After 0");
        f.ScrollTo(f.ScrollViewer.Offset.Y + f.Top(paragraph));
        var top = f.Top(paragraph);
        await AsyncTestHelpers.WaitUntilAsync(() => loaded.All(image => image.Source is not null));
        f.Settle();

        Assert.Equal(top, f.Top(paragraph), 0.5);
    }

    [AvaloniaFact]
    public async Task Streamed_update_while_reading_the_middle_keeps_the_text_in_view()
    {
        using var f = new Fixture("");
        var session = f.Viewer.BeginStreaming();
        session.Append(Paragraphs("Paragraph", 60));
        session.ApplyPendingUpdate();
        f.ScrollTo(600);
        var inView = f.FirstInView();
        var top = f.Top(inView);

        session.Append(" continued\n\n" + Paragraphs("More", 10));
        session.ApplyPendingUpdate();
        f.Settle();
        await AsyncTestHelpers.PumpAsync();

        Assert.Equal(top, f.Top(inView), 0.5);
        session.Complete();
    }
}
