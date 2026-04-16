// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using Markdig;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class EmphasisTests : RenderTestBase
{
    [AvaloniaFact]
    public void Bold_text_renders_as_Bold_inline()
    {
        var result = Render("**bold**");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var bold = Assert.IsType<Bold>(Assert.Single(textBlock.Inlines!));
        var run = Assert.IsType<Run>(Assert.Single(bold.Inlines));
        Assert.Equal("bold", run.Text);
    }

    [AvaloniaFact]
    public void Italic_text_renders_as_Italic_inline()
    {
        var result = Render("*italic*");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var italic = Assert.IsType<Italic>(Assert.Single(textBlock.Inlines!));
        var run = Assert.IsType<Run>(Assert.Single(italic.Inlines));
        Assert.Equal("italic", run.Text);
    }

    [AvaloniaFact]
    public void Mixed_emphasis_in_paragraph()
    {
        var result = Render("normal **bold** and *italic*");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var inlines = textBlock.Inlines!.ToList();
        Assert.Equal(4, inlines.Count); // "normal " + Bold + " and " + Italic
    }

    [AvaloniaFact]
    public void Strikethrough_text_renders_with_strikethrough_decoration()
    {
        var pipeline = new MarkdownPipelineBuilder().UseEmphasisExtras().Build();
        var result = Render("~~deleted~~", pipeline);

        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var span = Assert.IsType<Span>(Assert.Single(textBlock.Inlines!));
        Assert.Equal(TextDecorations.Strikethrough, span.TextDecorations);
    }

    [AvaloniaFact]
    public void Subscript_text_renders_with_subscript_baseline_alignment()
    {
        var pipeline = new MarkdownPipelineBuilder().UseEmphasisExtras().Build();
        var result = Render("H~2~O", pipeline);

        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var span = textBlock.Inlines!.OfType<Span>().Single();
        Assert.Equal(BaselineAlignment.Subscript, span.BaselineAlignment);
    }

    [AvaloniaFact]
    public void Superscript_text_renders_with_superscript_baseline_alignment()
    {
        var pipeline = new MarkdownPipelineBuilder().UseEmphasisExtras().Build();
        var result = Render("E=mc^2^", pipeline);

        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var span = textBlock.Inlines!.OfType<Span>().Single();
        Assert.Equal(BaselineAlignment.Superscript, span.BaselineAlignment);
    }

    [AvaloniaFact]
    public void Inserted_text_renders_with_underline_decoration()
    {
        var pipeline = new MarkdownPipelineBuilder().UseEmphasisExtras().Build();
        var result = Render("++inserted++", pipeline);

        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var span = Assert.IsType<Underline>(Assert.Single(textBlock.Inlines!));
        Assert.Equal(TextDecorations.Underline, span.TextDecorations);
    }

    [AvaloniaFact]
    public void Marked_text_renders_with_markdown_marked_class()
    {
        var pipeline = new MarkdownPipelineBuilder().UseEmphasisExtras().Build();
        var result = Render("==highlighted==", pipeline);

        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var span = Assert.IsType<Span>(Assert.Single(textBlock.Inlines!));
        Assert.Contains("markdown-marked", span.Classes);
    }

    [AvaloniaFact]
    public void Inserted_single_plus_falls_through_to_plain_span()
    {
        var span = RenderEmphasisInlineDirect('+', delimiterCount: 1);
        Assert.IsNotType<Underline>(span);
        Assert.Null(span.TextDecorations);
    }

    [AvaloniaFact]
    public void Marked_single_equals_falls_through_to_plain_span()
    {
        var span = RenderEmphasisInlineDirect('=', delimiterCount: 1);
        Assert.DoesNotContain("markdown-marked", span.Classes);
    }

    private static Span RenderEmphasisInlineDirect(char delimiterChar, int delimiterCount)
    {
        var renderer = new AvaloniaRenderer();
        var pipeline = new MarkdownPipelineBuilder().Build();
        pipeline.Setup(renderer);

        var container = new Span();
        renderer.Push(container.Inlines);

        var inline = new EmphasisInline { DelimiterChar = delimiterChar, DelimiterCount = delimiterCount };
        inline.AppendChild(new LiteralInline("text"));
        renderer.Write(inline);

        renderer.Pop();
        return Assert.IsType<Span>(Assert.Single(container.Inlines));
    }
}
