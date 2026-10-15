// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Markdig;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class FooterTests : RenderTestBase
{
    [AvaloniaFact]
    public void Footer_renders_its_blocks_in_a_footer_panel()
    {
        var result = Render("""
            Body

            ^^ Footer text
            ^^ continues
            """, new MarkdownPipelineBuilder().UseFooters().Build());

        Assert.Equal(2, result.Children.Count);
        var footer = Assert.IsType<StackPanel>(result.Children[1]);
        Assert.Contains("markdown-footer", footer.Classes);
        var paragraph = Assert.IsAssignableFrom<TextBlock>(Assert.Single(footer.Children));
        Assert.Contains("markdown-paragraph", paragraph.Classes);
    }
}
