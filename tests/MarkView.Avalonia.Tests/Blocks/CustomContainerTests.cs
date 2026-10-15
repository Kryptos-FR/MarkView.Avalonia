// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class CustomContainerTests : RenderTestBase
{
    private static MarkdownPipeline ContainerPipeline() =>
        new MarkdownPipelineBuilder().UseCustomContainers().Build();

    private static (Border Border, StackPanel Content) RenderContainer(string markdown)
    {
        var border = Assert.IsType<Border>(Assert.Single(Render(markdown, ContainerPipeline()).Children));
        Assert.Contains("markdown-custom-container", border.Classes);
        return (border, Assert.IsType<StackPanel>(border.Child));
    }

    [AvaloniaFact]
    public void Container_renders_its_blocks_inside_a_border()
    {
        var (_, content) = RenderContainer("""
            :::
            First

            Second
            :::
            """);

        Assert.Equal(2, content.Children.Count);
        Assert.All(content.Children, c => Assert.Contains("markdown-paragraph", c.Classes));
    }

    [AvaloniaFact]
    public void Container_name_becomes_a_class()
    {
        var (border, _) = RenderContainer("""
            :::warning
            Careful
            :::
            """);

        Assert.Equal(["markdown-custom-container", "warning"], border.Classes.Where(c => !c.StartsWith(':')));
    }

    [AvaloniaFact]
    public void Container_without_name_gets_only_the_container_class()
    {
        var (border, _) = RenderContainer("""
            :::
            Plain
            :::
            """);

        Assert.Equal(["markdown-custom-container"], border.Classes.Where(c => !c.StartsWith(':')));
    }

    [AvaloniaFact]
    public void Text_after_the_name_becomes_a_title()
    {
        var (_, content) = RenderContainer("""
            :::tip  Read this first
            Body
            :::
            """);

        Assert.Equal(2, content.Children.Count);
        var title = Assert.IsType<MarkdownSelectableTextBlock>(content.Children[0]);
        Assert.Contains("markdown-custom-container-title", title.Classes);
        Assert.Equal("Read this first", title.Text);
    }

    [AvaloniaFact]
    public void Container_without_title_text_has_no_title()
    {
        var (_, content) = RenderContainer("""
            :::tip
            Body
            :::
            """);

        var paragraph = Assert.Single(content.Children);
        Assert.DoesNotContain("markdown-custom-container-title", paragraph.Classes);
    }

    [AvaloniaFact]
    public void Inline_container_renders_as_span_with_class()
    {
        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(
            Assert.Single(Render("Some ::marked:: text", ContainerPipeline()).Children));

        var span = Assert.IsType<Span>(paragraph.Inlines![1]);
        Assert.Contains("markdown-custom-inline", span.Classes);
        Assert.Equal("marked", Assert.IsType<Run>(Assert.Single(span.Inlines)).Text);
    }
}
