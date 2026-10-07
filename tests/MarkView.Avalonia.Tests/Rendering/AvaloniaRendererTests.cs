// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
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

    [AvaloniaFact]
    public void ResolveUrl_keeps_fragment_unencoded_when_resolving_against_base()
    {
        var renderer = new AvaloniaRenderer { BaseUri = new Uri("https://example.com/docs/") };

        Assert.Equal("https://example.com/docs/guide/page.md#intro", renderer.ResolveUrl("guide/page.md#intro"));
    }

    [AvaloniaFact]
    public void ResolveUrl_keeps_percent_encoded_fragment_verbatim()
    {
        var renderer = new AvaloniaRenderer { BaseUri = new Uri("https://example.com/docs/") };

        Assert.Equal("https://example.com/docs/page.md#a%20b", renderer.ResolveUrl("page.md#a%20b"));
    }

    private static StackPanel RenderMarkdown(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    [AvaloniaFact]
    public void Indented_code_lines_are_separated_by_line_breaks()
    {
        var root = RenderMarkdown("""
                one
                two
            """);
        var text = Assert.IsType<TextBlock>(Assert.IsType<Border>(Assert.Single(root.Children)).Child);

        var inlines = text.Inlines!.ToList();
        Assert.Equal(3, inlines.Count);
        Assert.Equal("one", Assert.IsType<Run>(inlines[0]).Text);
        Assert.IsType<LineBreak>(inlines[1]);
        Assert.Equal("two", Assert.IsType<Run>(inlines[2]).Text);
    }

    [AvaloniaFact]
    public void Empty_fenced_code_block_has_no_inlines()
    {
        var root = RenderMarkdown("""
            ```
            ```
            """);
        var text = Assert.IsType<TextBlock>(Assert.IsType<Border>(Assert.Single(root.Children)).Child);

        Assert.Empty(text.Inlines!);
    }
}
