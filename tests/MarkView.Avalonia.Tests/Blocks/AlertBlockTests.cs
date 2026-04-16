// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Markdig;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class AlertBlockTests : RenderTestBase
{
    private static MarkdownPipeline AlertPipeline() =>
        new MarkdownPipelineBuilder().UseAlertBlocks().Build();

    [AvaloniaTheory]
    [InlineData("> [!NOTE]\n> Content", "note")]
    [InlineData("> [!WARNING]\n> Content", "warning")]
    [InlineData("> [!TIP]\n> Content", "tip")]
    [InlineData("> [!IMPORTANT]\n> Content", "important")]
    [InlineData("> [!CAUTION]\n> Content", "caution")]
    public void Alert_renders_Border_with_variant_class(string markdown, string kind)
    {
        var result = Render(markdown, AlertPipeline());
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-alert", border.Classes);
        Assert.Contains($"markdown-alert-{kind}", border.Classes);
    }

    [AvaloniaFact]
    public void Alert_renders_header_and_content()
    {
        var result = Render("> [!NOTE]\n> Hello world", AlertPipeline());
        var border = Assert.IsType<Border>(result.Children[0]);
        var outer = Assert.IsType<StackPanel>(border.Child);
        // outer has header TextBlock + content StackPanel
        Assert.True(outer.Children.Count >= 2);
        var header = Assert.IsType<TextBlock>(outer.Children[0]);
        Assert.Equal("NOTE", header.Text);
    }

    [AvaloniaFact]
    public void Alert_content_panel_has_children()
    {
        var result = Render("> [!WARNING]\n> Watch out", AlertPipeline());
        var border = Assert.IsType<Border>(result.Children[0]);
        var outer = Assert.IsType<StackPanel>(border.Child);
        var content = Assert.IsType<StackPanel>(outer.Children[1]);
        Assert.Contains("markdown-alert-content", content.Classes);
        Assert.NotEmpty(content.Children);
    }
}
