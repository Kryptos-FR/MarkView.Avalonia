// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using Markdig;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

/// <summary>How the HTML blocks of the HTML subset render.</summary>
public class HtmlSubsetBlockTests : RenderTestBase
{
    private static MarkdownPipeline Pipeline => new MarkdownPipelineBuilder().UseHtmlSubset().Build();

    [AvaloniaTheory]
    [InlineData("left", TextAlignment.Left)]
    [InlineData("center", TextAlignment.Center)]
    [InlineData("right", TextAlignment.Right)]
    [InlineData("justify", TextAlignment.Justify)]
    public void An_aligned_element_aligns_the_text_inside_it(string value, TextAlignment alignment)
    {
        var result = Render($"<p align=\"{value}\">x</p>", Pipeline);

        var panel = Assert.IsType<StackPanel>(Assert.Single(result.Children));
        Assert.Equal(["markdown-html-align", $"markdown-html-align-{value}"], panel.Classes);
        Assert.Equal(8, panel.Spacing);
        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(panel.Children));
        Assert.Contains("markdown-paragraph", paragraph.Classes);
        Assert.Equal(alignment, paragraph.TextAlignment);
    }

    [AvaloniaFact]
    public void A_centered_div_centers_the_markdown_inside_it()
    {
        var result = Render("<div align=\"center\">\n\n# Title\n\n- item\n\n</div>\n\nAfter", Pipeline);

        var panel = Assert.IsType<StackPanel>(result.Children[0]);
        var heading = Assert.IsType<MarkdownSelectableTextBlock>(panel.Children[0]);
        Assert.Contains("markdown-h1", heading.Classes);
        Assert.Equal(TextAlignment.Center, heading.TextAlignment);
        var list = Assert.IsType<StackPanel>(panel.Children[1]);
        Assert.Contains("markdown-list", list.Classes);
        var after = Assert.IsType<MarkdownSelectableTextBlock>(result.Children[1]);
        Assert.NotEqual(TextAlignment.Center, after.TextAlignment);
    }

    [AvaloniaFact]
    public void An_html_heading_renders_like_a_markdown_heading()
    {
        var result = Render("<h2 align=\"center\">Hello <sup>beta</sup></h2>", Pipeline);

        var heading = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Assert.IsType<StackPanel>(Assert.Single(result.Children)).Children));
        Assert.Equal(["markdown-heading", "markdown-h2"], heading.Classes);
        Assert.Equal("hello-beta", heading.Tag);
        Assert.Equal("Hello beta", MarkdownSelectableTextBlock.ExtractPlainText(heading.Inlines!));
    }

    [AvaloniaFact]
    public void An_hr_renders_a_thematic_break()
    {
        var result = Render("<p>a</p>\n<hr>\n<p>b</p>", Pipeline);

        Assert.Equal(3, result.Children.Count);
        Assert.Contains("markdown-thematic-break", Assert.IsType<Separator>(result.Children[1]).Classes);
    }

    [AvaloniaFact]
    public void A_readme_header_renders_centered_badges()
    {
        var result = Render("""
            <p align="center">
              <a href="https://example.com/ci"><img src="https://example.com/ci.svg" alt="CI"></a>
              <a href="https://example.com/nuget"><img src="https://example.com/nuget.svg" alt="NuGet"></a>
            </p>
            """, Pipeline);

        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Assert.IsType<StackPanel>(Assert.Single(result.Children)).Children));
        Assert.Equal(TextAlignment.Center, paragraph.TextAlignment);
        Assert.Equal("￼ ￼", MarkdownSelectableTextBlock.ExtractPlainText(paragraph.Inlines!, out var embedded));
        Assert.Equal(["CI", "NuGet"], embedded!.Values);
        Assert.All(paragraph.Inlines!.OfType<Span>(), link => Assert.Contains("markdown-link", link.Classes));
    }

    [AvaloniaFact]
    public void Unknown_html_in_a_block_shows_its_text()
    {
        var result = Render("<div>\nsome <span>html</span>\n</div>", Pipeline);

        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        Assert.Equal("some html", MarkdownSelectableTextBlock.ExtractPlainText(paragraph.Inlines!));
    }

    [AvaloniaFact]
    public void A_details_summary_shows_its_inline_html()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();
        var result = Render("<details>\n<summary><b>Bold</b> title</summary>\n\nBody\n\n</details>", pipeline);

        var summary = ((Panel)((Border)Assert.Single(result.Children)).Child!).Children.OfType<TextBlock>().Single();
        Assert.Contains("markdown-details-summary", summary.Classes);
        var inlines = summary.Inlines!.ToList();
        Assert.IsType<InlineUIContainer>(inlines[0]);
        Assert.Equal(" ", Assert.IsType<Run>(inlines[1]).Text);
        Assert.Equal("Bold", MarkdownSelectableTextBlock.ExtractPlainText(Assert.IsType<Bold>(inlines[2]).Inlines));
        Assert.Equal(" title", Assert.IsType<Run>(inlines[3]).Text);
    }
}
