// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Svg.Skia;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

public class MermaidDiagramCacheTests
{
    private static MermaidDiagramCache.Key Key(string source) =>
        new(source, "#FFFFFF", "#27272A", "#3B82F6", MermaidStyle.Quiet, FontFamily.Default);

    private static RenderedDiagram Drawing()
    {
        using var stream = new MemoryStream("""<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"/>"""u8.ToArray());
        return new RenderedDiagram(SvgSource.LoadFromStream(stream), []);
    }

    // A render that counts its runs and, while the gate is closed, waits for it to open.
    private sealed class Render
    {
        private readonly ManualResetEventSlim _gate = new(initialState: true);

        public int Runs;

        public void Close() => _gate.Reset();

        public void Open() => _gate.Set();

        public Func<RenderedDiagram> Returning(RenderedDiagram diagram) => () =>
        {
            Interlocked.Increment(ref Runs);
            _gate.Wait(TimeSpan.FromSeconds(5));
            return diagram;
        };
    }

    [AvaloniaFact]
    public async Task Renders_a_diagram_once_and_returns_it_from_then_on()
    {
        var cache = new MermaidDiagramCache(4);
        var render = new Render();
        var drawing = Drawing();

        var first = await cache.GetOrRenderAsync(Key("a"), render.Returning(drawing), CancellationToken.None);
        var second = await cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None);

        Assert.Same(drawing, first);
        Assert.Same(drawing, second);
        Assert.Equal(1, render.Runs);
        Assert.Equal(1, cache.Count);
    }

    [AvaloniaFact]
    public async Task Every_part_of_the_key_tells_diagrams_apart()
    {
        var cache = new MermaidDiagramCache(16);
        var render = new Render();
        var key = Key("a");
        MermaidDiagramCache.Key[] keys =
        [
            key,
            key with { Source = "b" },
            key with { Bg = "#000000" },
            key with { Fg = "#000000" },
            key with { Accent = "#000000" },
            key with { Style = MermaidStyle.Blueprint },
            key with { Font = new FontFamily("Other Font") },
        ];

        foreach (var k in keys)
            await cache.GetOrRenderAsync(k, render.Returning(Drawing()), CancellationToken.None);

        Assert.Equal(keys.Length, render.Runs);
    }

    [AvaloniaFact]
    public async Task Concurrent_requests_share_one_render()
    {
        var cache = new MermaidDiagramCache(4);
        var render = new Render();
        render.Close();

        var first = cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None);
        var second = cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None);
        render.Open();

        Assert.Same(await first, await second);
        Assert.Equal(1, render.Runs);
    }

    [AvaloniaFact]
    public async Task Evicts_the_least_recently_used_diagram()
    {
        var cache = new MermaidDiagramCache(2);
        var render = new Render();

        await cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None);
        await cache.GetOrRenderAsync(Key("b"), render.Returning(Drawing()), CancellationToken.None);
        await cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None);   // a is now the most recent
        await cache.GetOrRenderAsync(Key("c"), render.Returning(Drawing()), CancellationToken.None);   // evicts b
        Assert.Equal(3, render.Runs);

        await cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None);
        Assert.Equal(3, render.Runs);
        await cache.GetOrRenderAsync(Key("b"), render.Returning(Drawing()), CancellationToken.None);
        Assert.Equal(4, render.Runs);
        Assert.Equal(2, cache.Count);
    }

    [AvaloniaFact]
    public async Task Does_not_cache_with_no_capacity()
    {
        var cache = new MermaidDiagramCache(0);
        var render = new Render();

        await cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None);
        await cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None);

        Assert.Equal(2, render.Runs);
        Assert.Equal(0, cache.Count);
    }

    [AvaloniaFact]
    public async Task A_failed_render_is_not_cached()
    {
        var cache = new MermaidDiagramCache(4);
        var runs = 0;
        RenderedDiagram Fail()
        {
            runs++;
            throw new InvalidOperationException("bad diagram");
        }

        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrRenderAsync(Key("a"), Fail, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrRenderAsync(Key("a"), Fail, CancellationToken.None));

        Assert.Equal("bad diagram", first.Message);
        Assert.Equal(2, runs);
        Assert.Equal(0, cache.Count);
    }

    [AvaloniaFact]
    public async Task A_render_everyone_gave_up_on_is_still_cached_when_it_ends()
    {
        var cache = new MermaidDiagramCache(4);
        var render = new Render();
        var drawing = Drawing();
        render.Close();
        using var cts = new CancellationTokenSource();

        var abandoned = cache.GetOrRenderAsync(Key("a"), render.Returning(drawing), cts.Token);
        await AsyncTestHelpers.WaitUntilAsync(() => Volatile.Read(ref render.Runs) == 1);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
        render.Open();

        await AsyncTestHelpers.WaitUntilAsync(() => cache.Count == 1);
        Assert.Same(drawing, await cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None));
        Assert.Equal(1, render.Runs);
    }

    [AvaloniaFact]
    public async Task One_request_giving_up_leaves_the_render_to_the_others()
    {
        var cache = new MermaidDiagramCache(4);
        var render = new Render();
        var drawing = Drawing();
        render.Close();
        using var cts = new CancellationTokenSource();

        var abandoned = cache.GetOrRenderAsync(Key("a"), render.Returning(drawing), cts.Token);
        var kept = cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);

        // A request made after one gave up still joins the render the others wait for.
        var joined = cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), CancellationToken.None);
        render.Open();

        Assert.Same(drawing, await kept);
        Assert.Same(drawing, await joined);
        Assert.Equal(1, render.Runs);
    }

    [AvaloniaFact]
    public async Task A_request_after_every_request_gave_up_starts_a_new_render()
    {
        var cache = new MermaidDiagramCache(4);
        var render = new Render();
        var drawing = Drawing();
        render.Close();
        using var cts = new CancellationTokenSource();

        var abandoned = cache.GetOrRenderAsync(Key("a"), render.Returning(Drawing()), cts.Token);
        await AsyncTestHelpers.WaitUntilAsync(() => Volatile.Read(ref render.Runs) == 1);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);

        // The abandoned render is still running: a new request doesn't wait for it, it renders again.
        var again = cache.GetOrRenderAsync(Key("a"), render.Returning(drawing), CancellationToken.None);
        await AsyncTestHelpers.WaitUntilAsync(() => Volatile.Read(ref render.Runs) == 2);
        render.Open();

        Assert.Same(drawing, await again);
    }

    [AvaloniaFact]
    public async Task Clear_empties_the_cache()
    {
        var cache = new MermaidDiagramCache(4);
        await cache.GetOrRenderAsync(Key("a"), Drawing, CancellationToken.None);

        cache.Clear();

        Assert.Equal(0, cache.Count);
    }

    [AvaloniaFact]
    public async Task After_Clear_the_least_recently_used_diagram_is_evicted_first()
    {
        var cache = new MermaidDiagramCache(2);
        var a = new Render();
        var b = new Render();
        await cache.GetOrRenderAsync(Key("a"), a.Returning(Drawing()), CancellationToken.None);
        cache.Clear();

        await cache.GetOrRenderAsync(Key("b"), b.Returning(Drawing()), CancellationToken.None);
        await cache.GetOrRenderAsync(Key("a"), a.Returning(Drawing()), CancellationToken.None);
        await cache.GetOrRenderAsync(Key("c"), Drawing, CancellationToken.None);

        // "b" is the least recently used: a diagram cached before Clear takes no part in the order.
        await cache.GetOrRenderAsync(Key("a"), a.Returning(Drawing()), CancellationToken.None);
        await cache.GetOrRenderAsync(Key("b"), b.Returning(Drawing()), CancellationToken.None);
        Assert.Equal(2, a.Runs);
        Assert.Equal(2, b.Runs);
    }

    // Rendering diagrams in documents.

    private static string UniqueDiagram() => $"```mermaid\ngraph TD\n  A --> B\n  %% {Guid.NewGuid():N}\n```";

    private static StackPanel RenderDocument(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        new MermaidExtension().Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    private static Image DiagramImage(StackPanel root) => Assert.IsType<Image>(Assert.IsType<Border>(Assert.Single(root.Children)).Child);

    [AvaloniaFact]
    public async Task A_diagram_rendered_before_shows_at_once_in_a_new_document()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var markdown = UniqueDiagram();
        var first = RenderDocument(markdown);
        var window = new Window { Width = 600, Height = 300, Content = first };
        try
        {
            window.Show();
            var firstImage = DiagramImage(first);
            await AsyncTestHelpers.WaitUntilAsync(() => firstImage.Source is not null);

            var second = RenderDocument(markdown);
            window.Content = second;

            // Shown as soon as it is attached, before any render could have run.
            var secondImage = DiagramImage(second);
            var drawn = Assert.IsType<SvgImage>(secondImage.Source);
            Assert.Same(((SvgImage)firstImage.Source!).Source, drawn.Source);
            // Its own picture, saved from the same SVG document.
            Assert.NotSame(firstImage.Source, drawn);
            Assert.Equal(ImageExport.GetSvg(firstImage.Source!), ImageExport.GetSvg(drawn));
            Assert.StartsWith("<svg", Encoding.UTF8.GetString(ImageExport.GetSvg(drawn)!).TrimStart());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_diagram_removed_while_rendering_ignores_the_render_and_shows_it_when_put_back()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var markdown = UniqueDiagram();
        var removed = RenderDocument(markdown);
        var image = DiagramImage(removed);
        var border = removed.Children[0];
        var other = RenderDocument(markdown);
        var otherImage = DiagramImage(other);
        var window = new Window { Width = 600, Height = 300, Content = new StackPanel { Children = { removed, other } } };
        try
        {
            window.Show();
            // Removed before its render could end.
            removed.Children.Remove(border);

            await AsyncTestHelpers.WaitUntilAsync(() => otherImage.Source is not null);
            await AsyncTestHelpers.PumpAsync();
            Assert.Null(image.Source);

            removed.Children.Add(border);

            var drawn = Assert.IsType<SvgImage>(image.Source);
            Assert.Same(((SvgImage)otherImage.Source!).Source, drawn.Source);
        }
        finally
        {
            window.Close();
        }
    }
}
