// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

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
}
