using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class LinkTests : RenderTestBase
{
    [AvaloniaFact]
    public void Link_renders_as_MarkdownHyperlink_span()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Equal(new Uri("https://example.com"), hyperlink.NavigateUri);
    }

    [AvaloniaFact]
    public void Link_text_is_a_Run_inside_hyperlink()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        var run = Assert.IsType<Run>(Assert.Single(hyperlink.Inlines));
        Assert.Equal("click me", run.Text);
    }

    [AvaloniaFact]
    public void Link_has_markdown_link_css_class()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Contains("markdown-link", hyperlink.Classes);
    }

    [AvaloniaFact]
    public void Link_with_title_stores_Title_on_hyperlink()
    {
        var result = Render("[click me](https://example.com \"My Title\")");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Equal("My Title", hyperlink.Title);
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

        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Equal(new Uri("https://doc.stride3d.net/4.2/path/to/doc"), hyperlink.NavigateUri);
    }
}
