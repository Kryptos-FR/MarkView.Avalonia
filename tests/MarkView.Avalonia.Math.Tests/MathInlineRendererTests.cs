// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using Markdig;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class MathInlineRendererTests
{
    private static StackPanel Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseMathematics().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer();
        renderer.ObjectRenderers.Add(new MathInlineRenderer());
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    [AvaloniaFact]
    public void Inline_math_renders_inside_InlineUIContainer()
    {
        var result = Render("Einstein said $E=mc^2$ once.");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var container = textBlock.Inlines!.OfType<InlineUIContainer>().Single();
        Assert.IsType<Image>(container.Child);
    }

    [AvaloniaFact]
    public void Inline_math_image_has_math_inline_class()
    {
        var result = Render("$x^2$");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var container = textBlock.Inlines!.OfType<InlineUIContainer>().Single();
        var image = Assert.IsType<Image>(container.Child);
        Assert.Contains("markdown-math-inline", image.Classes);
    }

    [AvaloniaFact]
    public void Inline_math_image_has_non_null_bitmap_source()
    {
        var result = Render("$x^2$");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var container = textBlock.Inlines!.OfType<InlineUIContainer>().Single();
        var image = Assert.IsType<Image>(container.Child);
        Assert.NotNull(image.Source);
    }
}
