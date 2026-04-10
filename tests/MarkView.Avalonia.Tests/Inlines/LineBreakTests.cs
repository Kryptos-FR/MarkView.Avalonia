// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class LineBreakTests : RenderTestBase
{
    [AvaloniaFact]
    public void Hard_break_renders_as_LineBreak_inline()
    {
        // Backslash at end of line forces a hard line break
        var result = Render("line one\\\nline two");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var inlines = textBlock.Inlines!.ToList();
        // Run("line one") + LineBreak + Run("line two")
        Assert.Equal(3, inlines.Count);
        Assert.IsType<LineBreak>(inlines[1]);
    }

    [AvaloniaFact]
    public void Soft_break_renders_as_space_Run()
    {
        // A single newline inside a paragraph is a soft break
        var result = Render("line one\nline two");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var inlines = textBlock.Inlines!.ToList();
        // Run("line one") + Run(" ") + Run("line two")
        Assert.Equal(3, inlines.Count);
        var space = Assert.IsType<Run>(inlines[1]);
        Assert.Equal(" ", space.Text);
    }
}
