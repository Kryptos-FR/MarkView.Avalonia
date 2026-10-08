// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class MathSelectionTests
{
    private static string SelectAllText(string markdown)
    {
        var viewer = new MarkdownViewer().UseMath();
        viewer.Markdown = markdown;
        viewer.SelectAll();
        return viewer.GetSelectedText();
    }

    [AvaloniaFact]
    public void Inline_math_is_copied_as_its_dollar_delimited_source()
    {
        Assert.Equal("Einstein said $E=mc^2$ once.", SelectAllText("Einstein said $E=mc^2$ once."));
    }

    [AvaloniaFact]
    public void Display_math_is_copied_as_its_double_dollar_delimited_source()
    {
        const string markdown = """
            before

            $$
            x^2 + y^2
            $$

            after
            """;

        Assert.Equal("before\n$$\nx^2 + y^2\n$$\nafter", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Empty_display_math_is_copied_as_empty_delimiters()
    {
        const string markdown = """
            $$
            $$
            """;

        Assert.Equal("$$\n\n$$", SelectAllText(markdown));
    }

    [AvaloniaFact]
    public void Display_math_that_fails_to_render_is_still_copied_as_its_source()
    {
        var nested = new string('{', 60) + "x" + new string('}', 60); // exceeds the brace-nesting limit

        Assert.Equal($"$$\n{nested}\n$$", SelectAllText($"$$\n{nested}\n$$"));
    }
}
