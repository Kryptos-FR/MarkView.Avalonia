// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;

using Xunit;

namespace MarkView.Avalonia.Tests.Shared;

internal static class CodeBlockAssert
{
    /// <summary>
    /// Returns the <c>markdown-code-block</c> border of a rendered code block, whether it was written
    /// on its own or inside a <c>markdown-code-container</c> below its toolbar.
    /// </summary>
    public static Border CodeBorder(Control block) =>
        block is StackPanel container && container.Classes.Contains("markdown-code-container")
            ? Assert.IsType<Border>(container.Children[^1])
            : Assert.IsType<Border>(block);

    /// <summary>
    /// Takes apart the content of a <c>markdown-code-block</c> border: the vertical scroll viewer
    /// a maximum height puts around the code, the line-number gutter next to it, and the code's
    /// text block. The first two are <c>null</c> when the block was rendered without them.
    /// </summary>
    public static (ScrollViewer? Scroll, TextBlock? Numbers, TextBlock Code) Layout(Border border)
    {
        var content = border.Child;
        ScrollViewer? scroll = null;
        if (content is ScrollViewer scrollViewer)
        {
            Assert.Contains("markdown-code-scroll", scrollViewer.Classes);
            Assert.Equal(ScrollBarVisibility.Disabled, scrollViewer.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
            scroll = scrollViewer;
            content = (Control?)scrollViewer.Content;
        }

        if (content is not Grid lines)
            return (scroll, null, Assert.IsType<TextBlock>(content));

        Assert.Equal(2, lines.Children.Count);
        var numbers = Assert.IsType<TextBlock>(lines.Children[0]);
        Assert.Contains("markdown-code-line-numbers", numbers.Classes);
        Assert.Equal(0, Grid.GetColumn(numbers));
        var code = Assert.IsType<TextBlock>(lines.Children[1]);
        Assert.Equal(1, Grid.GetColumn(code));
        return (scroll, numbers, code);
    }
}
