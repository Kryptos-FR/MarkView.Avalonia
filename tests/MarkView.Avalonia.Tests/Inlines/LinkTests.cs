// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class LinkTests : RenderTestBase
{
    [AvaloniaFact]
    public void Link_renders_as_HyperlinkButton_with_correct_uri()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var uiContainer = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(uiContainer.Child);
        Assert.Equal(new Uri("https://example.com"), button.NavigateUri);
    }

    [AvaloniaFact]
    public void Link_text_is_a_Run_inside_the_button_content()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var uiContainer = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(uiContainer.Child);
        var content = Assert.IsType<TextBlock>(button.Content);
        var run = Assert.IsType<Run>(Assert.Single(content.Inlines!));
        Assert.Equal("click me", run.Text);
    }

    [AvaloniaFact]
    public void Link_has_markdown_link_css_class()
    {
        var result = Render("[click me](https://example.com)");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var uiContainer = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(uiContainer.Child);
        Assert.Contains("markdown-link", button.Classes);
    }

    [AvaloniaFact]
    public void Link_with_title_sets_tooltip_on_button()
    {
        var result = Render("[click me](https://example.com \"My Title\")");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var uiContainer = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(uiContainer.Child);
        Assert.Equal("My Title", ToolTip.GetTip(button));
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
        var uiContainer = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(uiContainer.Child);
        Assert.Equal(new Uri("https://doc.stride3d.net/4.2/path/to/doc"), button.NavigateUri);
    }
}
