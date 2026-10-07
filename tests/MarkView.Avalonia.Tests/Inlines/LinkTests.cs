// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class LinkTests : RenderTestBase
{
    [AvaloniaFact]
    public void Link_renders_as_MarkdownHyperlink_span()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Equal(new Uri("https://example.com"), hyperlink.NavigateUri);
    }

    [AvaloniaFact]
    public void Link_text_is_a_Run_inside_hyperlink()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        var run = Assert.IsType<Run>(Assert.Single(hyperlink.Inlines));
        Assert.Equal("click me", run.Text);
    }

    [AvaloniaFact]
    public void Link_has_markdown_link_css_class()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Contains("markdown-link", hyperlink.Classes);
    }

    [AvaloniaFact]
    public void Link_with_title_stores_Title_on_hyperlink()
    {
        var result = Render("[click me](https://example.com \"My Title\")");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Equal("My Title", hyperlink.Title);
    }

    [AvaloniaFact]
    public void Link_without_title_has_null_Title()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Null(hyperlink.Title);
    }

    [AvaloniaFact]
    public void Link_with_empty_title_has_null_Title()
    {
        var result = Render("[click me](https://example.com \"\")");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Null(hyperlink.Title);
    }

    [AvaloniaFact]
    public void Relative_link_is_resolved_against_BaseUri()
    {
        var markdown = "[docs](path/to/doc)";
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var document = Markdig.Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer
        {
            BaseUri = new Uri("https://doc.stride3d.net/4.2/")
        };
        pipeline.Setup(renderer);
        renderer.Render(document);
        var result = renderer.RootPanel;

        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Equal(new Uri("https://doc.stride3d.net/4.2/path/to/doc"), hyperlink.NavigateUri);
    }

    private sealed class ControlledLoader(string prefix) : IImageLoader
    {
        public List<string> Requests { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public TaskCompletionSource<IImage?> Result { get; } = new();
        public bool CanLoad(string url) => url.StartsWith(prefix, StringComparison.Ordinal);
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Requests.Add(url);
            Tokens.Add(cancellationToken);
            return Result.Task;
        }
    }

    private sealed class FixedImage : IImage
    {
        public Size Size => new(1, 1);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
    }

    private static (StackPanel Root, Image Image) RenderImage(string markdown, params IImageLoader[] loaders)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        for (var i = loaders.Length - 1; i >= 0; i--)
            renderer.ImageLoaders.Insert(0, loaders[i]);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        var text = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(renderer.RootPanel.Children));
        var image = Assert.IsType<Image>(text.Inlines!.OfType<InlineUIContainer>().Single().Child);
        return (renderer.RootPanel, image);
    }

    [AvaloniaFact]
    public void Image_is_a_single_inline_with_image_class_and_alt_tooltip()
    {
        var (root, image) = RenderImage("![Alt text](test://a.png)");

        var text = (MarkdownSelectableTextBlock)root.Children[0];
        Assert.Single(text.Inlines!);
        Assert.DoesNotContain(text.Inlines!, i => i is MarkdownHyperlink);
        Assert.Contains("markdown-image", image.Classes);
        Assert.Equal("Alt text", ToolTip.GetTip(image));
    }

    [AvaloniaFact]
    public async Task Reattaching_while_a_load_is_in_flight_does_not_start_a_second_load()
    {
        var loader = new ControlledLoader("test://");
        var (root, _) = RenderImage("![a](test://a.png)", loader);
        var window = new Window { Content = root };
        try
        {
            window.Show();
            window.Content = null;
            window.Content = root;
            await AsyncTestHelpers.PumpAsync();

            Assert.Single(loader.Requests);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_the_image_cancels_its_in_flight_load()
    {
        var loader = new ControlledLoader("test://");
        var (root, _) = RenderImage("![a](test://a.png)", loader);
        var window = new Window { Content = root };
        try
        {
            window.Show();

            window.Content = null;

            Assert.True(Assert.Single(loader.Tokens).IsCancellationRequested);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task First_loader_that_returns_an_image_wins_and_declining_loaders_are_skipped()
    {
        var declining = new ControlledLoader("other://");
        var nullResult = new ControlledLoader("test://");
        var winner = new ControlledLoader("test://");
        var shadowed = new ControlledLoader("test://");
        nullResult.Result.SetResult(null);
        var expected = new FixedImage();
        winner.Result.SetResult(expected);
        shadowed.Result.SetResult(new FixedImage());
        var (root, image) = RenderImage("![a](test://a.png)", declining, nullResult, winner, shadowed);
        var window = new Window { Content = root };
        try
        {
            window.Show();

            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);

            Assert.Same(expected, image.Source);
            Assert.Empty(declining.Requests);
            Assert.Empty(shadowed.Requests);
        }
        finally
        {
            window.Close();
        }
    }

    private static StackPanel RenderWithoutUrl(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(markdown, pipeline);
        foreach (var link in document.Descendants<LinkInline>())
            link.Url = null;
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    [AvaloniaFact]
    public void Link_without_url_resolves_to_an_empty_target()
    {
        var text = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(RenderWithoutUrl("[a](x)").Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(text.Inlines!));

        Assert.Equal(string.Empty, hyperlink.NavigateUri?.OriginalString ?? string.Empty);
    }

    [AvaloniaFact]
    public void Image_without_url_has_an_empty_source_url()
    {
        var text = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(RenderWithoutUrl("![a](x)").Children));
        var image = Assert.IsType<Image>(text.Inlines!.OfType<InlineUIContainer>().Single().Child);

        Assert.Equal(string.Empty, image.Tag);
    }
}
