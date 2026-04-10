// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class QuoteBlockTests : RenderTestBase
{
    [AvaloniaFact]
    public void Blockquote_renders_as_Border_with_StackPanel()
    {
        var result = Render("> This is a quote");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-blockquote", border.Classes);
        var panel = Assert.IsType<StackPanel>(border.Child);
        Assert.NotEmpty(panel.Children);
    }

    [AvaloniaFact]
    public void Nested_blockquote_renders_recursively()
    {
        var result = Render("> Outer\n> > Inner");
        var outerBorder = Assert.IsType<Border>(Assert.Single(result.Children));
        var outerPanel = Assert.IsType<StackPanel>(outerBorder.Child);
        Assert.True(outerPanel.Children.Count >= 1);
    }
}
