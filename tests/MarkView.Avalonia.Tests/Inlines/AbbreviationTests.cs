// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class AbbreviationTests : RenderTestBase
{
    private static MarkdownPipeline AbbrPipeline() =>
        new MarkdownPipelineBuilder().UseAbbreviations().Build();

    [AvaloniaFact]
    public void Abbreviation_renders_with_class_and_tooltip()
    {
        var result = Render("HTML is great\n\n*[HTML]: HyperText Markup Language", AbbrPipeline());
        var para = Assert.IsType<MarkdownSelectableTextBlock>(result.Children[0]);
        var abbr = FindAbbrTextBlock(para.Inlines!);
        Assert.NotNull(abbr);
        Assert.Equal("HTML", abbr!.Text);
        Assert.Equal("HyperText Markup Language", ToolTip.GetTip(abbr) as string);
    }

    [AvaloniaFact]
    public void Abbreviation_has_markdown_abbr_class()
    {
        var result = Render("HTML is great\n\n*[HTML]: HyperText Markup Language", AbbrPipeline());
        var para = Assert.IsType<MarkdownSelectableTextBlock>(result.Children[0]);
        var abbr = FindAbbrTextBlock(para.Inlines!);
        Assert.NotNull(abbr);
        Assert.Contains("markdown-abbr", abbr!.Classes);
    }

    [AvaloniaFact]
    public void Non_abbreviated_word_has_no_abbr_inline()
    {
        var result = Render("CSS is great\n\n*[HTML]: HyperText Markup Language", AbbrPipeline());
        var para = Assert.IsType<MarkdownSelectableTextBlock>(result.Children[0]);
        Assert.Null(FindAbbrTextBlock(para.Inlines!));
    }

    private static TextBlock? FindAbbrTextBlock(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is InlineUIContainer c && c.Child is TextBlock tb && tb.Classes.Contains("markdown-abbr"))
                return tb;
            if (inline is Span s && s.Inlines != null)
            {
                var found = FindAbbrTextBlock(s.Inlines);
                if (found is not null) return found;
            }
        }
        return null;
    }
}
