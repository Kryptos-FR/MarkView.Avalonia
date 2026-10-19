// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

internal static class MathBlockAssert
{
    /// <summary>
    /// Returns the border of a rendered display formula (<c>markdown-math-block</c>, or
    /// <c>markdown-math-fallback</c> when it failed to render), whether it was written on its own or
    /// inside a <c>markdown-math-container</c> under its copy button.
    /// </summary>
    public static Border MathBorder(Control? block)
    {
        if (block is not Grid container || !container.Classes.Contains("markdown-math-container"))
            return Assert.IsType<Border>(block);

        Assert.Equal(2, container.Children.Count);
        Assert.Contains("markdown-math-copy", Assert.IsType<Button>(container.Children[1]).Classes);
        return Assert.IsType<Border>(container.Children[0]);
    }
}
