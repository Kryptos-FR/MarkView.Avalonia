using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class MarkdownSelectableTextBlockTests
{
    [Fact]
    public void Is_not_a_SelectableTextBlock()
    {
        Assert.False(typeof(MarkdownSelectableTextBlock).IsSubclassOf(typeof(SelectableTextBlock)));
    }

    [Fact]
    public void Is_a_TextBlock()
    {
        Assert.True(typeof(MarkdownSelectableTextBlock).IsSubclassOf(typeof(TextBlock)));
    }

    [Fact]
    public void ExtractPlainText_extracts_runs()
    {
        var inlines = new InlineCollection();
        inlines.Add(new Run("Hello "));
        inlines.Add(new Run("World"));
        var result = MarkdownSelectableTextBlock.ExtractPlainText(inlines);
        Assert.Equal("Hello World", result);
    }
}

