// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// <see cref="MarkdownViewer.UrlTransform"/>, and which links the viewer opens with the platform's launcher
/// (<see cref="MarkdownViewerDefaults.LaunchableLinkSchemes"/>).
/// </summary>
public class MarkdownViewerUrlPolicyTests
{
    private sealed class CountingLoader : IImageLoader
    {
        public List<string> Urls { get; } = [];
        public bool CanLoad(string url) => true;

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Urls.Add(url);
            return Task.FromResult<IImage?>(null);
        }
    }

    private sealed class LoaderExtension(IImageLoader loader) : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, loader);
    }

    private static InlineCollection Paragraph(MarkdownViewer viewer)
    {
        var grid = Assert.IsType<Grid>(viewer.Content);
        var block = Assert.IsType<MarkdownSelectableTextBlock>(Assert.IsType<StackPanel>(grid.Children[0]).Children[0]);
        return block.Inlines!;
    }

    private static MarkdownHyperlink Link(MarkdownViewer viewer) => Paragraph(viewer).OfType<MarkdownHyperlink>().Single();

    /// <summary>Records each URL and kind given to it, and returns what <paramref name="map"/> makes of the URL.</summary>
    private static MarkdownUrlTransform Recording(List<(string Url, MarkdownUrlKind Kind)> calls, Func<string, string?>? map = null) =>
        (url, kind) =>
        {
            calls.Add((url, kind));
            return map is null ? url : map(url);
        };

    private static (Window Window, MarkdownViewer Viewer) Show(MarkdownViewer viewer)
    {
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        return (window, viewer);
    }

    // ── UrlTransform: links ───────────────────────────────────────────────────

    [AvaloniaFact]
    public void A_link_gets_the_url_the_transform_returns()
    {
        var viewer = new MarkdownViewer
        {
            UrlTransform = (url, _) => url.Replace("http://", "https://"),
            Markdown = "[site](http://example.com/a)",
        };

        Assert.Equal(new Uri("https://example.com/a"), Link(viewer).NavigateUri);
    }

    [AvaloniaFact]
    public void The_transform_is_given_the_url_resolved_against_the_base_uri()
    {
        var calls = new List<(string, MarkdownUrlKind)>();

        _ = new MarkdownViewer
        {
            BaseUri = new Uri("https://example.com/docs/"),
            UrlTransform = Recording(calls),
            Markdown = "[next](next.md#intro)",
        };

        Assert.Equal([("https://example.com/docs/next.md#intro", MarkdownUrlKind.Link)], calls);
    }

    [AvaloniaFact]
    public void A_dropped_link_renders_as_its_text_alone()
    {
        var viewer = new MarkdownViewer { UrlTransform = (_, _) => null, Markdown = "Go [**there**](file:///etc/passwd) now" };

        var inlines = Paragraph(viewer);
        Assert.Empty(inlines.OfType<MarkdownHyperlink>());
        Assert.Equal("Go there now", inlines.Text);
        Assert.Single(inlines.OfType<Bold>());
    }

    [AvaloniaFact]
    public void A_same_document_anchor_is_not_given_to_the_transform()
    {
        var calls = new List<(string, MarkdownUrlKind)>();

        var viewer = new MarkdownViewer { UrlTransform = Recording(calls, _ => null), Markdown = "[top](#top)" };

        Assert.Empty(calls);
        Assert.Equal("#top", Link(viewer).NavigateUri!.OriginalString);
    }

    [AvaloniaFact]
    public void An_email_autolink_is_given_to_the_transform_as_mailto()
    {
        var calls = new List<(string, MarkdownUrlKind)>();

        var viewer = new MarkdownViewer { UrlTransform = Recording(calls), Markdown = "<me@example.com>" };

        Assert.Equal([("mailto:me@example.com", MarkdownUrlKind.Link)], calls);
        Assert.Equal(new Uri("mailto:me@example.com"), Link(viewer).NavigateUri);
    }

    [AvaloniaTheory]
    [InlineData("<me@example.com>", "me@example.com")]
    [InlineData("<https://example.com/a>", "https://example.com/a")]
    public void A_dropped_autolink_renders_as_its_text(string markdown, string text)
    {
        var viewer = new MarkdownViewer { UrlTransform = (_, _) => null, Markdown = markdown };

        var inlines = Paragraph(viewer);
        Assert.Empty(inlines.OfType<MarkdownHyperlink>());
        Assert.Equal(text, inlines.Text);
    }

    [AvaloniaFact]
    public void An_html_link_goes_through_the_transform()
    {
        var calls = new List<(string, MarkdownUrlKind)>();

        _ = new MarkdownViewer { UrlTransform = Recording(calls), Markdown = """<a href="https://example.com/a">site</a>""" };

        Assert.Equal([("https://example.com/a", MarkdownUrlKind.Link)], calls);
    }

    [AvaloniaFact]
    public async Task The_url_of_a_link_still_being_streamed_is_not_given_to_the_transform()
    {
        var calls = new List<(string, MarkdownUrlKind)>();
        var viewer = new MarkdownViewer { UrlTransform = Recording(calls) };

        var session = viewer.BeginStreaming();
        session.Append("See [site](https://exam");
        session.Append("ple.com/)");

        Assert.Empty(calls);
        session.Complete();
        await Tests.Shared.AsyncTestHelpers.WaitUntilAsync(() => calls.Count > 0);
        Assert.Equal([("https://example.com/", MarkdownUrlKind.Link)], calls);
    }

    // ── UrlTransform: images ──────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task An_image_loads_the_url_the_transform_returns()
    {
        var loader = new CountingLoader();
        var viewer = new MarkdownViewer { UrlTransform = (url, _) => url + "?size=small" };
        viewer.Extensions.Add(new LoaderExtension(loader));
        viewer.Markdown = "![pic](https://example.com/a.png)";
        var (window, _) = Show(viewer);

        await Tests.Shared.AsyncTestHelpers.WaitUntilAsync(() => loader.Urls.Count > 0);

        Assert.Equal(["https://example.com/a.png?size=small"], loader.Urls);
        window.Close();
    }

    [AvaloniaFact]
    public void An_image_url_is_given_to_the_transform_as_an_image()
    {
        var calls = new List<(string, MarkdownUrlKind)>();

        _ = new MarkdownViewer { UrlTransform = Recording(calls), Markdown = "![pic](https://example.com/a.png) <img src=\"https://example.com/b.png\">" };

        Assert.Equal([("https://example.com/a.png", MarkdownUrlKind.Image), ("https://example.com/b.png", MarkdownUrlKind.Image)], calls);
    }

    [AvaloniaFact]
    public void A_dropped_image_shows_its_placeholder_without_being_loaded()
    {
        var loader = new CountingLoader();
        var viewer = new MarkdownViewer { UrlTransform = (_, _) => null };
        viewer.Extensions.Add(new LoaderExtension(loader));
        viewer.Markdown = "![a tracking pixel](https://evil.example/?secret=1)";
        var (window, _) = Show(viewer);

        var placeholder = viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-image-failed"));
        Assert.Equal("a tracking pixel", MarkdownSelection.GetCopyText(placeholder));
        Assert.Empty(viewer.GetVisualDescendants().OfType<Image>());
        Assert.Empty(loader.Urls);
        window.Close();
    }

    [AvaloniaFact]
    public void A_video_whose_link_is_dropped_shows_the_image_placeholder()
    {
        var viewer = new MarkdownViewer
        {
            Pipeline = new MarkdownPipelineBuilder().UseMediaLinks().Build(),
            UrlTransform = (url, kind) => kind == MarkdownUrlKind.Link ? null : url,
            Markdown = "![video](https://www.youtube.com/watch?v=dQw4w9WgXcQ)",
        };
        var (window, _) = Show(viewer);

        Assert.Empty(viewer.GetVisualDescendants().OfType<Button>());
        Assert.Single(viewer.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("markdown-image-failed"));
        window.Close();
    }

    [AvaloniaFact]
    public void A_video_thumbnail_is_given_to_the_transform_as_an_image()
    {
        var calls = new List<(string, MarkdownUrlKind)>();
        var loader = new CountingLoader();
        var viewer = new MarkdownViewer
        {
            Pipeline = new MarkdownPipelineBuilder().UseMediaLinks().Build(),
            UrlTransform = Recording(calls, url => url.Contains("img.youtube.com") ? null : url),
        };
        viewer.Extensions.Add(new LoaderExtension(loader));
        viewer.Markdown = "![video](https://www.youtube.com/watch?v=dQw4w9WgXcQ)";
        var (window, _) = Show(viewer);

        Assert.Equal(
            [("https://www.youtube.com/watch?v=dQw4w9WgXcQ", MarkdownUrlKind.Link), ("https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg", MarkdownUrlKind.Image)],
            calls);
        Assert.Single(viewer.GetVisualDescendants().OfType<Button>());
        Assert.Empty(loader.Urls);
        window.Close();
    }

    // ── UrlTransform: where it comes from ─────────────────────────────────────

    [AvaloniaFact]
    public void The_application_wide_transform_is_used_by_a_viewer_without_its_own()
    {
        using var _ = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.UrlTransform = (_, _) => "https://app.example/";

        var viewer = new MarkdownViewer { Markdown = "[a](https://example.com/)" };

        Assert.Equal(new Uri("https://app.example/"), Link(viewer).NavigateUri);
    }

    [AvaloniaFact]
    public void A_viewer_transform_takes_precedence_over_the_application_wide_one()
    {
        using var _ = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.UrlTransform = (_, _) => "https://app.example/";

        var viewer = new MarkdownViewer { UrlTransform = (_, _) => "https://viewer.example/", Markdown = "[a](https://example.com/)" };

        Assert.Equal(new Uri("https://viewer.example/"), Link(viewer).NavigateUri);
    }

    [AvaloniaFact]
    public void Setting_the_transform_renders_the_document_again()
    {
        var viewer = new MarkdownViewer { Markdown = "[a](https://example.com/)" };

        viewer.UrlTransform = (_, _) => null;

        Assert.Empty(Paragraph(viewer).OfType<MarkdownHyperlink>());
    }

    // ── Launching ─────────────────────────────────────────────────────────────

    private static (MarkdownViewer Viewer, List<Uri> Launched, List<string> Clicked) Clickable(string markdown)
    {
        var launched = new List<Uri>();
        var clicked = new List<string>();
        var viewer = new MarkdownViewer { Markdown = markdown };
        viewer.LaunchUri = uri =>
        {
            launched.Add(uri);
            return Task.CompletedTask;
        };
        viewer.LinkClicked += (_, e) => clicked.Add(e.Url);
        return (viewer, launched, clicked);
    }

    [AvaloniaTheory]
    [InlineData("https://example.com/")]
    [InlineData("http://example.com/")]
    [InlineData("mailto:me@example.com")]
    [InlineData("HTTPS://EXAMPLE.COM/")]
    public void A_web_or_mail_link_is_opened_with_the_launcher(string url)
    {
        var (viewer, launched, clicked) = Clickable($"[a]({url})");

        viewer.ActivateLink(Link(viewer));

        Assert.Equal([new Uri(url)], launched);
        Assert.Single(clicked);
    }

    [AvaloniaTheory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-appinstaller:?source=https://evil.example/app.msix")]
    [InlineData("myapp://settings")]
    public void A_link_of_another_scheme_raises_LinkClicked_but_is_not_launched(string url)
    {
        var (viewer, launched, clicked) = Clickable($"[a]({url})");

        viewer.ActivateLink(Link(viewer));

        Assert.Empty(launched);
        Assert.Equal([new Uri(url).ToString()], clicked);
    }

    [AvaloniaFact]
    public void A_scheme_added_by_the_application_is_launched()
    {
        using var _ = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.LaunchableLinkSchemes.Add("myapp");
        var (viewer, launched, _) = Clickable("[a](myapp://settings)");

        viewer.ActivateLink(Link(viewer));

        Assert.Equal([new Uri("myapp://settings")], launched);
    }

    [AvaloniaFact]
    public void A_handled_link_is_not_launched()
    {
        var (viewer, launched, _) = Clickable("[a](https://example.com/)");
        viewer.LinkClicked += (_, e) => e.Handled = true;

        viewer.ActivateLink(Link(viewer));

        Assert.Empty(launched);
    }

    [AvaloniaFact]
    public void A_relative_link_is_not_launched()
    {
        var (viewer, launched, clicked) = Clickable("[a](docs/next.md)");

        viewer.ActivateLink(Link(viewer));

        Assert.Empty(launched);
        Assert.Equal(["docs/next.md"], clicked);
    }
}
