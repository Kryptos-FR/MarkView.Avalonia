using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class SelectionLayerTests
{
    [AvaloniaFact]
    public void SelectAll_covers_all_registered_blocks()
    {
        var layer = new DocumentSelectionLayer();
        layer.Register(new DocumentBlock(MakeBlock("Hello"), "Hello"));
        layer.Register(new DocumentBlock(MakeBlock("World"), "World"));

        layer.SelectAll();

        Assert.Equal("Hello\nWorld", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void ClearSelection_returns_empty_text()
    {
        var layer = new DocumentSelectionLayer();
        layer.Register(new DocumentBlock(MakeBlock("Hello"), "Hello"));
        layer.SelectAll();
        layer.ClearSelection();

        Assert.Equal(string.Empty, layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void GetSelectedText_single_block_partial()
    {
        var layer = new DocumentSelectionLayer();
        layer.Register(new DocumentBlock(MakeBlock("Hello World"), "Hello World"));

        layer.SetSelectionForTest(blockIdx: 0, startOffset: 0, endOffset: 5);

        Assert.Equal("Hello", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void GetSelectedText_cross_block()
    {
        var layer = new DocumentSelectionLayer();
        layer.Register(new DocumentBlock(MakeBlock("Hello"), "Hello"));
        layer.Register(new DocumentBlock(MakeBlock("World"), "World"));

        layer.SetSelectionForTest(startBlock: 0, startOffset: 3, endBlock: 1, endOffset: 3);

        Assert.Equal("lo\nWor", layer.GetSelectedText());
    }

    private static TextBlock MakeBlock(string text) => new() { Text = text };
}
