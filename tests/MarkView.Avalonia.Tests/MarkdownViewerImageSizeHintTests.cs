// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;

using Markdig;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>The rewrite of "![alt](url =WxH)" size hints before parsing.</summary>
public class MarkdownViewerImageSizeHintTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();

    private static string Preprocess(string markdown) => MarkdownViewer.PreprocessImageSizes(markdown, Pipeline, out _);

    [AvaloniaTheory]
    [InlineData("![a](x.png =10x20)", "![a](x.png \"=10x20\")")]
    [InlineData("Text ![a](x.png =10x20) and ![b](y.png  =3x4).", "Text ![a](x.png \"=10x20\") and ![b](y.png \"=3x4\").")]
    [InlineData("- item\n\n    ![a](x.png =10x20)", "- item\n\n    ![a](x.png \"=10x20\")")]
    [InlineData("``a`b`` ![a](x.png =10x20)", "``a`b`` ![a](x.png \"=10x20\")")]
    [InlineData("`code` ![a](x.png =10x20) `code`", "`code` ![a](x.png \"=10x20\") `code`")]
    public void Rewrites_size_hints_outside_code(string markdown, string expected) =>
        Assert.Equal(expected, Preprocess(markdown));

    [AvaloniaTheory]
    [InlineData("`![a](x.png =10x20)`")]
    [InlineData("``a`![a](x.png =10x20)``")]
    [InlineData("```\n![a](x.png =10x20)\n```")]
    [InlineData("~~~\n![a](x.png =10x20)\n~~~")]
    [InlineData("~~~md\n![a](x.png =10x20)\n```\nstill code\n~~~")]
    [InlineData("Paragraph\n\n    ![a](x.png =10x20)")]
    [InlineData("> ~~~\n> ![a](x.png =10x20)\n> ~~~")]
    [InlineData("```\nunclosed fence\n![a](x.png =10x20)")]
    public void Leaves_size_hints_in_code_as_written(string markdown) =>
        Assert.Equal(markdown, Preprocess(markdown));

    [AvaloniaFact]
    public void Rewrites_the_hints_around_code_and_leaves_the_one_inside()
    {
        const string markdown = "![a](x.png =1x1)\n\n~~~\n![b](y.png =2x2)\n~~~\n\n![c](z.png =3x3)";

        Assert.Equal("![a](x.png \"=1x1\")\n\n~~~\n![b](y.png =2x2)\n~~~\n\n![c](z.png \"=3x3\")", Preprocess(markdown));
    }

    [AvaloniaFact]
    public void Text_without_a_size_hint_is_returned_as_is()
    {
        const string markdown = "# Title\n\n`code` and ![a](x.png)";

        Assert.Same(markdown, MarkdownViewer.PreprocessImageSizes(markdown, Pipeline, out var shifts));
        Assert.Empty(shifts);
    }
}
