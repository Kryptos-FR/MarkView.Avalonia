// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.VisualTree;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>Documents and images read through <see cref="MarkdownViewer.ResourceResolver"/>.</summary>
public class MarkdownViewerResourceResolverTests
{
    /// <summary>Serves resources from memory, synchronously unless <see cref="Gate"/> is set.</summary>
    private sealed class MemoryResolver(Dictionary<string, byte[]> resources) : IResourceResolver
    {
        public List<Uri> Requests { get; } = [];
        public Task? Gate { get; set; }

        public MemoryResolver(Dictionary<string, string> texts)
            : this(texts.ToDictionary(p => p.Key, p => System.Text.Encoding.UTF8.GetBytes(p.Value))) { }

        public Task<Stream?> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            Requests.Add(uri);
            var stream = resources.TryGetValue(uri.AbsoluteUri, out var bytes) ? new MemoryStream(bytes) : null;
            return Gate is null ? Task.FromResult<Stream?>(stream) : Opened(Gate, stream);

            static async Task<Stream?> Opened(Task gate, Stream? stream)
            {
                await gate;
                return stream;
            }
        }
    }

    private sealed class FailingResolver : IResourceResolver
    {
        public Task<Stream?> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromException<Stream?>(new UnauthorizedAccessException());
    }

    private sealed class ThrowingResolver : IResourceResolver
    {
        public Task<Stream?> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Thrown instead of failing the task.");
    }

    private static byte[] Pixel()
    {
        using var stream = AssetLoader.Open(new Uri("avares://MarkView.Avalonia.Tests/TestAssets/pixel.png"));
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static MarkdownSelectableTextBlock FirstBlock(MarkdownViewer viewer)
    {
        var grid = Assert.IsType<Grid>(viewer.Content);
        return Assert.IsType<MarkdownSelectableTextBlock>(Assert.IsType<StackPanel>(grid.Children[0]).Children[0]);
    }

    private static string FirstText(MarkdownViewer viewer) => FirstBlock(viewer).Inlines!.Text!;

    private static (Window Window, MarkdownViewer Viewer) Show(MarkdownViewer viewer)
    {
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        return (window, viewer);
    }

    private static Image[] Images(MarkdownViewer viewer) => viewer.GetVisualDescendants().OfType<Image>().ToArray();

    // ── Source ────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void A_resolver_that_answers_at_once_renders_the_document_before_the_setter_returns()
    {
        var resolver = new MemoryResolver(new Dictionary<string, string> { ["zip://manual/index.md"] = "From the archive" });

        var viewer = new MarkdownViewer { ResourceResolver = resolver, Source = new Uri("zip://manual/index.md") };

        Assert.Equal("From the archive", FirstText(viewer));
    }

    [AvaloniaFact]
    public async Task A_resolver_that_answers_later_renders_the_document_once_it_has()
    {
        var gate = new TaskCompletionSource();
        var resolver = new MemoryResolver(new Dictionary<string, string> { ["zip://manual/index.md"] = "From the archive" }) { Gate = gate.Task };
        var viewer = new MarkdownViewer { ResourceResolver = resolver, Source = new Uri("zip://manual/index.md") };
        Assert.Null(viewer.Content);

        gate.SetResult();
        await AsyncTestHelpers.WaitUntilAsync(() => viewer.Content is not null);

        Assert.Equal("From the archive", FirstText(viewer));
    }

    [AvaloniaFact]
    public void The_resolver_is_given_the_source_without_its_fragment()
    {
        var resolver = new MemoryResolver(new Dictionary<string, string> { ["zip://manual/index.md"] = "# Title" });

        _ = new MarkdownViewer { ResourceResolver = resolver, Source = new Uri("zip://manual/index.md#title") };

        Assert.Equal([new Uri("zip://manual/index.md")], resolver.Requests);
    }

    [AvaloniaFact]
    public void A_source_the_resolver_leaves_is_loaded_by_the_viewer()
    {
        var resolver = new MemoryResolver(new Dictionary<string, string>());

        var viewer = new MarkdownViewer { ResourceResolver = resolver, Source = new Uri("avares://MarkView.Avalonia.Tests/TestAssets/test.md") };

        Assert.Contains("markdown-h1", FirstBlock(viewer).Classes);
        Assert.Single(resolver.Requests);
    }

    [AvaloniaFact]
    public async Task A_source_that_fails_to_load_keeps_the_current_document()
    {
        var viewer = new MarkdownViewer { Markdown = "Current" };

        viewer.ResourceResolver = new FailingResolver();
        viewer.Source = new Uri("zip://manual/index.md");
        await Task.Yield();

        Assert.Equal("Current", FirstText(viewer));
    }

    [AvaloniaFact]
    public void A_resolver_that_throws_keeps_the_current_document()
    {
        var viewer = new MarkdownViewer { Markdown = "Current" };

        viewer.ResourceResolver = new ThrowingResolver();
        viewer.Source = new Uri("zip://manual/index.md");

        Assert.Equal("Current", FirstText(viewer));
    }

    [AvaloniaFact]
    public void A_source_of_a_scheme_nobody_opens_keeps_the_current_document()
    {
        var viewer = new MarkdownViewer { Markdown = "Current" };

        viewer.Source = new Uri("zip://manual/index.md");

        Assert.Equal("Current", FirstText(viewer));
    }

    [AvaloniaFact]
    public void A_relative_source_is_ignored()
    {
        var viewer = new MarkdownViewer { Markdown = "Current" };

        viewer.Source = new Uri("docs/index.md#intro", UriKind.Relative);

        Assert.Equal("Current", FirstText(viewer));
    }

    [AvaloniaFact]
    public void Setting_the_resolver_loads_the_source_again()
    {
        var viewer = new MarkdownViewer { Source = new Uri("zip://manual/index.md") };
        Assert.Null(viewer.Content);

        viewer.ResourceResolver = new MemoryResolver(new Dictionary<string, string> { ["zip://manual/index.md"] = "Now readable" });

        Assert.Equal("Now readable", FirstText(viewer));
    }

    [AvaloniaFact]
    public void The_application_wide_resolver_is_used_by_a_viewer_without_its_own()
    {
        using var _ = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.ResourceResolver = new MemoryResolver(new Dictionary<string, string> { ["zip://manual/index.md"] = "App-wide" });

        var viewer = new MarkdownViewer { Source = new Uri("zip://manual/index.md") };

        Assert.Equal("App-wide", FirstText(viewer));
    }

    [AvaloniaFact]
    public void A_viewer_resolver_takes_precedence_over_the_application_wide_one()
    {
        using var _ = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.ResourceResolver = new MemoryResolver(new Dictionary<string, string> { ["zip://manual/index.md"] = "App-wide" });

        var viewer = new MarkdownViewer
        {
            ResourceResolver = new MemoryResolver(new Dictionary<string, string> { ["zip://manual/index.md"] = "Viewer" }),
            Source = new Uri("zip://manual/index.md"),
        };

        Assert.Equal("Viewer", FirstText(viewer));
    }

    // ── Images ────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Images_of_a_document_from_a_custom_scheme_are_read_relative_to_it()
    {
        var root = $"zip://{Guid.NewGuid():N}/";
        var resolver = new MemoryResolver(new Dictionary<string, byte[]>
        {
            [root + "guide/index.md"] = "![pixel](../img/pixel.png)"u8.ToArray(),
            [root + "img/pixel.png"] = Pixel(),
        });
        var (window, viewer) = Show(new MarkdownViewer { ResourceResolver = resolver, Source = new Uri(root + "guide/index.md") });

        var image = Images(viewer).Single();
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);

        Assert.Contains(new Uri(root + "img/pixel.png"), resolver.Requests);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Images_of_a_markdown_text_are_read_through_the_resolver()
    {
        var url = $"app://images/{Guid.NewGuid():N}.png";
        var resolver = new MemoryResolver(new Dictionary<string, byte[]> { [url] = Pixel() });
        var (window, viewer) = Show(new MarkdownViewer { ResourceResolver = resolver, Markdown = $"![pixel]({url})" });

        var image = Images(viewer).Single();
        await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
        window.Close();
    }

    [AvaloniaFact]
    public async Task An_image_the_resolver_cannot_read_shows_its_placeholder()
    {
        var url = $"app://images/{Guid.NewGuid():N}.png";
        var (window, viewer) = Show(new MarkdownViewer { ResourceResolver = new FailingResolver(), Markdown = $"![pixel]({url})" });

        await AsyncTestHelpers.WaitUntilAsync(() => viewer.GetVisualDescendants().OfType<Border>().Any(b => b.Classes.Contains("markdown-image-failed")));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Images_next_to_a_file_source_are_loaded()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(folder.FullName, "pixel.png"), Pixel(), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(folder.FullName, "doc.md"), "![pixel](pixel.png)", TestContext.Current.CancellationToken);
            var (window, viewer) = Show(new MarkdownViewer { Source = new Uri(Path.Combine(folder.FullName, "doc.md")) });
            await AsyncTestHelpers.WaitUntilAsync(() => viewer.Content is not null);

            var image = Images(viewer).Single();
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
            window.Close();
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task The_same_image_url_read_through_different_resolvers_is_cached_apart()
    {
        var url = $"app://images/{Guid.NewGuid():N}.png";
        var first = new MemoryResolver(new Dictionary<string, byte[]> { [url] = Pixel() });
        var second = new MemoryResolver(new Dictionary<string, byte[]> { [url] = Pixel() });
        var (window, viewer) = Show(new MarkdownViewer { ResourceResolver = first, Markdown = $"![pixel]({url})" });
        await AsyncTestHelpers.WaitUntilAsync(() => Images(viewer).Single().Source is not null);
        var loaded = Images(viewer).Single().Source;

        // The same resolver again: from the cache.
        viewer.Markdown = $"![pixel]({url})\n\nEdited";
        window.UpdateLayout();
        Assert.Same(loaded, Images(viewer).Single().Source);
        Assert.Single(first.Requests);

        viewer.ResourceResolver = second;
        window.UpdateLayout();
        await AsyncTestHelpers.WaitUntilAsync(() => Images(viewer).Single().Source is not null);

        Assert.NotSame(loaded, Images(viewer).Single().Source);
        Assert.Single(second.Requests);
        window.Close();
    }
}
