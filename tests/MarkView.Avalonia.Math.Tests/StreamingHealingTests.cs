// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Math.Parsing;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class StreamingHealingTests
{
    /// <summary>
    /// The images of the inline formulas the viewer's document renders, in document order; none while
    /// the first update has not installed the document yet.
    /// </summary>
    private static List<Image> FormulaImages(MarkdownViewer viewer)
    {
        if (viewer.Content is not Grid grid)
            return [];

        var panel = Assert.IsType<StackPanel>(grid.Children[0]);
        return panel.Children
            .OfType<MarkdownSelectableTextBlock>()
            .SelectMany(textBlock => textBlock.Inlines!.OfType<InlineUIContainer>())
            .Select(container => container.Child)
            .OfType<Image>()
            .Where(image => image.Classes.Contains("markdown-math-inline"))
            .ToList();
    }

    [AvaloniaFact]
    public async Task Open_bracket_formula_renders_as_a_formula_while_streaming()
    {
        var viewer = new MarkdownViewer().UseMath();
        viewer.Markdown = "";
        using var session = viewer.BeginStreaming(TimeSpan.FromMilliseconds(1));
        session.Append(@"The area is \(\pi r^2");

        await AsyncTestHelpers.WaitUntilAsync(() => FormulaImages(viewer).Count == 1);
        Assert.Equal(@"\pi r^2", AutomationProperties.GetName(FormulaImages(viewer).Single()));
    }

    /// <summary>
    /// <c>MarkdownHealer.OptionsFor</c> in the core package recognises the bracket math extension by this
    /// full name, since the core does not reference this package: renaming or moving the type would
    /// silently stop the healing of <c>\(</c> and <c>\[</c>.
    /// </summary>
    [AvaloniaFact]
    public void Bracket_extension_keeps_the_name_the_core_healer_looks_for() =>
        Assert.Equal("MarkView.Avalonia.Math.Parsing.BracketMathExtension", typeof(BracketMathExtension).FullName);
}
