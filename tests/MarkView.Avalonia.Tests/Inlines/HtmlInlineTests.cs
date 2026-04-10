// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class HtmlInlineTests : RenderTestBase
{
    [AvaloniaTheory]
    [InlineData("before<br>after")]
    [InlineData("before<br/>after")]
    [InlineData("before<br />after")]
    [InlineData("before<BR>after")]
    public void Br_tag_renders_as_line_break(string markdown)
    {
        var result = Render(markdown);

        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var inlines = textBlock.Inlines!.ToList();
        // Should be: Run("before") + LineBreak + Run("after")
        Assert.Equal(3, inlines.Count);
        Assert.IsType<Run>(inlines[0]);
        Assert.IsType<LineBreak>(inlines[1]);
        Assert.IsType<Run>(inlines[2]);
    }

    [AvaloniaFact]
    public void Unknown_html_tag_is_silently_ignored()
    {
        var result = Render("text<span>more</span>end");

        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        // The <span> and </span> tags should be dropped, leaving just text runs
        var inlines = textBlock.Inlines!.ToList();
        Assert.All(inlines, i => Assert.True(i is Run));
    }
}
