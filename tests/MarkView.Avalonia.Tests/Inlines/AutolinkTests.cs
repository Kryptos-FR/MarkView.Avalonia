// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class AutolinkTests : RenderTestBase
{
    [AvaloniaFact]
    public void Url_autolink_renders_as_MarkdownHyperlink_span_with_correct_uri()
    {
        var result = Render("<https://example.com>");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Equal(new Uri("https://example.com"), hyperlink.NavigateUri);
    }

    [AvaloniaFact]
    public void Url_autolink_has_markdown_link_css_class()
    {
        var result = Render("<https://example.com>");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Contains("markdown-link", hyperlink.Classes);
    }

    [AvaloniaFact]
    public void Email_autolink_prepends_mailto_scheme()
    {
        var result = Render("<user@example.com>");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        Assert.Equal(new Uri("mailto:user@example.com"), hyperlink.NavigateUri);
    }

    [AvaloniaFact]
    public void Email_autolink_displays_raw_address_not_mailto_uri()
    {
        var result = Render("<user@example.com>");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var hyperlink = Assert.IsType<MarkdownHyperlink>(Assert.Single(textBlock.Inlines!));
        var run = Assert.IsType<Run>(Assert.Single(hyperlink.Inlines));
        Assert.Equal("user@example.com", run.Text);
    }
}
