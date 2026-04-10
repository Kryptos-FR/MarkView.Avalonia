// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class AutolinkTests : RenderTestBase
{
    [AvaloniaFact]
    public void Url_autolink_renders_as_HyperlinkButton_with_correct_uri()
    {
        var result = Render("<https://example.com>");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var uiContainer = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(uiContainer.Child);
        Assert.Equal(new Uri("https://example.com"), button.NavigateUri);
    }

    [AvaloniaFact]
    public void Url_autolink_has_markdown_link_css_class()
    {
        var result = Render("<https://example.com>");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var uiContainer = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(uiContainer.Child);
        Assert.Contains("markdown-link", button.Classes);
    }

    [AvaloniaFact]
    public void Email_autolink_prepends_mailto_scheme()
    {
        var result = Render("<user@example.com>");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var uiContainer = Assert.IsType<InlineUIContainer>(Assert.Single(textBlock.Inlines!));
        var button = Assert.IsType<HyperlinkButton>(uiContainer.Child);
        Assert.Equal(new Uri("mailto:user@example.com"), button.NavigateUri);
    }
}
