// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

// Tests non-rendering behaviour directly on AvaloniaRenderer; RenderTestBase not needed.
public class AvaloniaRendererTests
{
    [AvaloniaFact]
    public void ResolveUrl_relative_with_BaseUri_returns_absolute_url()
    {
        var renderer = new AvaloniaRenderer { BaseUri = new Uri("https://example.com/docs/") };
        var result = renderer.ResolveUrl("images/pic.png");
        Assert.Equal("https://example.com/docs/images/pic.png", result);
    }

    [AvaloniaFact]
    public void ResolveUrl_absolute_url_is_returned_unchanged()
    {
        var renderer = new AvaloniaRenderer { BaseUri = new Uri("https://example.com/docs/") };
        var result = renderer.ResolveUrl("https://other.com/file.png");
        Assert.Equal("https://other.com/file.png", result);
    }

    [AvaloniaFact]
    public void ResolveUrl_relative_url_without_BaseUri_is_returned_unchanged()
    {
        var renderer = new AvaloniaRenderer();
        var result = renderer.ResolveUrl("images/pic.png");
        Assert.Equal("images/pic.png", result);
    }

    [AvaloniaFact]
    public void ResolveUrl_pure_fragment_with_BaseUri_is_returned_unchanged()
    {
        var renderer = new AvaloniaRenderer { BaseUri = new Uri("https://example.com/docs/page.md") };
        var result = renderer.ResolveUrl("#heading-1");
        Assert.Equal("#heading-1", result);
    }

    [AvaloniaFact]
    public void ImageResizeMode_defaults_to_ScaleDownToFit()
    {
        var renderer = new AvaloniaRenderer();
        Assert.Equal(ImageResizeMode.ScaleDownToFit, renderer.ImageResizeMode);
    }

    [AvaloniaFact]
    public void ImageResizeMode_can_be_set()
    {
        var renderer = new AvaloniaRenderer { ImageResizeMode = ImageResizeMode.Fill };
        Assert.Equal(ImageResizeMode.Fill, renderer.ImageResizeMode);
    }

    [AvaloniaFact]
    public void HeadingEntries_records_level_text_and_slug_in_document_order()
    {
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var document = Markdig.Markdown.Parse("# Title\n\n## Sub Heading", pipeline);
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(document);

        Assert.Equal(
            new List<(int Level, string Text, string Slug)>
            {
                (1, "Title", "title"),
                (2, "Sub Heading", "sub-heading"),
            },
            renderer.HeadingEntries);
    }

    [AvaloniaFact]
    public void HeadingEntries_cleared_on_next_render()
    {
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(Markdig.Markdown.Parse("# First", pipeline));
        renderer.Render(Markdig.Markdown.Parse("Just a paragraph", pipeline));

        Assert.Empty(renderer.HeadingEntries);
    }
}
