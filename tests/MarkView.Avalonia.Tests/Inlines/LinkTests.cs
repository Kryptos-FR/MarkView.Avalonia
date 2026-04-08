using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class LinkTests : RenderTestBase
{
    [AvaloniaFact]
    public void Link_renders_as_HyperlinkButton_in_InlineUIContainer()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var container = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(container.Child);
        Assert.Equal(new Uri("https://example.com"), button.NavigateUri);
    }

    [AvaloniaFact]
    public void Link_text_is_rendered_as_button_content()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var container = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(container.Child);
        var content = Assert.IsType<TextBlock>(button.Content);
        var run = Assert.IsType<Run>(Assert.Single(content.Inlines!));
        Assert.Equal("click me", run.Text);
    }

    [AvaloniaFact]
    public void Relative_link_is_resolved_against_BaseUri()
    {
        var markdown = "[docs](path/to/doc)";
        var pipeline = new Markdig.MarkdownPipelineBuilder().Build();
        var document = Markdig.Markdown.Parse(markdown, pipeline);
        var renderer = new MarkView.Avalonia.Rendering.AvaloniaRenderer
        {
            BaseUri = new Uri("https://doc.stride3d.net/4.2/")
        };
        pipeline.Setup(renderer);
        renderer.Render(document);
        var result = renderer.RootPanel;

        var textBlock = Assert.IsType<TextBlock>(Assert.Single(result.Children));
        var container = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(container.Child);
        Assert.Equal(new Uri("https://doc.stride3d.net/4.2/path/to/doc"), button.NavigateUri);
    }
}
