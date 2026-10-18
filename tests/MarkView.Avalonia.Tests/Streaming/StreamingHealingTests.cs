// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Streaming;

/// <summary>
/// How a streaming session heals the inline syntax the text so far leaves open, and renders the block
/// that contains the end of the text as provisional: closers are parse input only, the final update is
/// not healed, and links and images wait for their block to close.
/// </summary>
public class StreamingHealingTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public LinkFocusTarget[] Targets => Viewer.GetVisualDescendants().OfType<LinkFocusTarget>().ToArray();

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

    private static StackPanel Root(MarkdownViewer viewer) =>
        Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);

    private static TextBlock TextBlockAt(MarkdownViewer viewer, int index) =>
        Assert.IsAssignableFrom<TextBlock>(Root(viewer).Children[index]);

    private static IEnumerable<Inline> Flatten(InlineCollection inlines) =>
        inlines.SelectMany(i => i is Span span ? Flatten(span.Inlines).Prepend(i) : [i]);

    private static List<Run> Runs(TextBlock textBlock) => Flatten(textBlock.Inlines!).OfType<Run>().ToList();

    private static MarkdownHyperlink Hyperlink(MarkdownViewer viewer, int index) =>
        Assert.Single(Flatten(TextBlockAt(viewer, index).Inlines!).OfType<MarkdownHyperlink>());

    private static ToggleButton[] Toggles(MarkdownViewer viewer) =>
        Assert.IsType<Grid>(viewer.Content).GetLogicalDescendants().OfType<ToggleButton>().ToArray();

    /// <summary>An image of a fixed size that draws a transparent rectangle, so it can be hit.</summary>
    private sealed class FakeImage(double width, double height) : IImage
    {
        public Size Size { get; } = new(width, height);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) => context.FillRectangle(Brushes.Transparent, destRect);
    }

    /// <summary>A loader of <c>fake:</c> URLs that records every URL it was asked to load.</summary>
    private sealed class RecordingLoader(IImage image) : IImageLoader
    {
        public List<string> Requested { get; } = [];
        public bool CanLoad(string url) => url.StartsWith("fake:", StringComparison.Ordinal);

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Requested.Add(url);
            return Task.FromResult<IImage?>(image);
        }
    }

    private sealed class LoaderExtension(IImageLoader loader) : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, loader);
    }

    [AvaloniaFact]
    public void Unclosed_emphasis_renders_as_emphasis_while_streaming()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("intro **bold");
        session.ApplyPendingUpdate();

        var block = TextBlockAt(f.Viewer, 0);
        Assert.Contains(block.Inlines!, i => i is Bold);
        Assert.Equal("intro **bold", f.Viewer.Markdown);
    }

    [AvaloniaFact]
    public void Healing_can_be_turned_off()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming(healIncompleteSyntax: false);
        session.Append("intro **bold");
        session.ApplyPendingUpdate();

        Assert.DoesNotContain(TextBlockAt(f.Viewer, 0).Inlines!, i => i is Span);
    }

    [AvaloniaFact]
    public void Final_update_is_not_healed()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("intro **bold");
        session.ApplyPendingUpdate();
        session.Complete();

        Assert.DoesNotContain(TextBlockAt(f.Viewer, 0).Inlines!, i => i is Span);
        f.Viewer.SelectAll();
        Assert.Equal("intro **bold", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Selection_copies_no_closer()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a `co");
        session.ApplyPendingUpdate();
        f.Viewer.SelectAll();
        Assert.Equal("a co", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Literal_asterisk_in_a_closed_paragraph_stays_literal()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("2 * 3 * 4\n\nnext *i");
        session.ApplyPendingUpdate();
        session.Append(" more");
        session.ApplyPendingUpdate();

        Assert.DoesNotContain(TextBlockAt(f.Viewer, 0).Inlines!, i => i is Span);
        Assert.Contains(TextBlockAt(f.Viewer, 1).Inlines!, i => i is Italic);
    }

    [AvaloniaFact]
    public void Heading_being_typed_is_healed()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("# **Title");
        session.ApplyPendingUpdate();

        Assert.Contains(TextBlockAt(f.Viewer, 0).Inlines!, i => i is Bold);
    }

    [AvaloniaFact]
    public void Setext_heading_being_typed_is_not_healed()
    {
        // A closer after the underline would turn the heading back into a paragraph.
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("intro **b\n===");
        session.ApplyPendingUpdate();

        var block = TextBlockAt(f.Viewer, 0);
        Assert.Contains("markdown-h1", block.Classes);
        Assert.DoesNotContain(block.Inlines!, i => i is Span);
    }

    [AvaloniaFact]
    public void Code_block_being_streamed_is_not_healed()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("```\na **b");
        session.ApplyPendingUpdate();
        f.Viewer.SelectAll();
        Assert.Equal("a **b", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Link_in_the_open_paragraph_has_no_address_and_no_tab_stop_until_the_paragraph_closes()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("see [docs](https://example.com/do");
        session.ApplyPendingUpdate();
        f.Window.UpdateLayout();
        Assert.Null(Hyperlink(f.Viewer, 0).NavigateUri);
        Assert.Empty(f.Targets);

        session.Append("cs)\n\nnext");
        session.ApplyPendingUpdate();
        f.Window.UpdateLayout();
        Assert.Equal("https://example.com/docs", Hyperlink(f.Viewer, 0).NavigateUri!.ToString());
        Assert.Single(f.Targets);
    }

    [AvaloniaFact]
    public void Heading_rendered_provisionally_is_rendered_again_by_the_final_update()
    {
        // Markdig closes an ATX heading at once: the block is unchanged, but its link was provisional.
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("# [docs](https://example.com/docs)");
        session.ApplyPendingUpdate();
        Assert.Null(Hyperlink(f.Viewer, 0).NavigateUri);

        session.Complete();
        f.Window.UpdateLayout();
        Assert.Equal("https://example.com/docs", Hyperlink(f.Viewer, 0).NavigateUri!.ToString());
        Assert.Single(f.Targets);
    }

    [AvaloniaFact]
    public async Task Image_is_not_requested_until_its_block_closes()
    {
        var loader = new RecordingLoader(new FakeImage(20, 10));
        using var f = Show("", v => v.Extensions.Add(new LoaderExtension(loader)));
        var url = $"fake://{Guid.NewGuid()}.png";   // the shared image cache remembers URLs across tests
        var session = f.Viewer.BeginStreaming();
        session.Append($"![alt]({url}");
        session.ApplyPendingUpdate();
        f.Window.UpdateLayout();
        Assert.Empty(loader.Requested);
        Assert.Equal("alt", Runs(TextBlockAt(f.Viewer, 0)).Single().Text);

        session.Append(")\n\nnext");
        session.ApplyPendingUpdate();
        await AsyncTestHelpers.WaitUntilAsync(() => loader.Requested.Count == 1);
        Assert.Equal([url], loader.Requested);
    }

    [AvaloniaFact]
    public void Closed_blocks_keep_their_controls_across_a_healed_update()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("first\n\nsecond **b");
        session.ApplyPendingUpdate();
        var first = Root(f.Viewer).Children[0];
        session.Append("old");
        session.ApplyPendingUpdate();
        Assert.Same(first, Root(f.Viewer).Children[0]);
        Assert.Equal(2, Root(f.Viewer).Children.Count);
    }

    [AvaloniaFact]
    public void Task_toggle_positions_are_unaffected_by_healing()
    {
        using var f = Show("", v =>
        {
            v.Pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
            v.AreTaskListsInteractive = true;
        });
        var session = f.Viewer.BeginStreaming();
        session.Append("- [ ] a\n- [ ] **b");
        session.ApplyPendingUpdate();
        Toggles(f.Viewer)[0].IsChecked = true;
        session.Append(" more");
        session.ApplyPendingUpdate();
        Assert.StartsWith("- [x] a", f.Viewer.Markdown, StringComparison.Ordinal);
    }
}
