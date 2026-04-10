// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class HtmlBlockTests : RenderTestBase
{
    [AvaloniaFact]
    public void Html_block_is_silently_ignored()
    {
        var result = Render("<div>\nsome html\n</div>");

        // HTML blocks produce no output in a non-HTML renderer
        Assert.Empty(result.Children);
    }

    [AvaloniaFact]
    public void Html_block_between_paragraphs_does_not_break_rendering()
    {
        var result = Render("Before\n\n<div>html</div>\n\nAfter");

        // Should have at least the two paragraphs
        Assert.True(result.Children.Count >= 2);
    }
}
