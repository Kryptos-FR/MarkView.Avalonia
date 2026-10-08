// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Markdig;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class HeadingTests : RenderTestBase
{
    [AvaloniaTheory]
    [InlineData("# H1", 1)]
    [InlineData("## H2", 2)]
    [InlineData("### H3", 3)]
    [InlineData("#### H4", 4)]
    [InlineData("##### H5", 5)]
    [InlineData("###### H6", 6)]
    public void Heading_renders_with_level_style_class(string markdown, int level)
    {
        var result = Render(markdown);
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        Assert.Contains($"markdown-h{level}", textBlock.Classes);
    }

    [AvaloniaFact]
    public void Heading_renders_text_content()
    {
        var result = Render("# Hello");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var run = Assert.IsType<Run>(Assert.Single(textBlock.Inlines!));
        Assert.Equal("Hello", run.Text);
    }

    [AvaloniaFact]
    public void Heading_slug_and_entry_text_include_inline_code_and_nested_emphasis_text()
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse("## Use `Foo` and **bold _nested_** now", pipeline));

        var entry = Assert.Single(renderer.HeadingEntries);
        Assert.Equal((2, "Use Foo and bold nested now", "use-foo-and-bold-nested-now"), entry);
        var block = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(renderer.RootPanel.Children));
        Assert.Equal("use-foo-and-bold-nested-now", block.Tag);
    }

    [AvaloniaFact]
    public void Heading_has_heading_classes_wraps_and_is_bound_to_the_renderer()
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse("### Title", pipeline));

        var block = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(renderer.RootPanel.Children));
        Assert.Equal(["markdown-heading", "markdown-h3"], block.Classes.Where(c => !c.StartsWith(':')).ToArray());
        Assert.Equal(TextWrapping.Wrap, block.TextWrapping);
        Assert.Same(renderer, block.Renderer);
    }
}
