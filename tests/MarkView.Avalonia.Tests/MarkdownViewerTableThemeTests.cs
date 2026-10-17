// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// Table styles from <c>MarkdownTheme.axaml</c>, applied in a headless window.
/// </summary>
/// <remarks>
/// Kept apart from the renderer tests: Stryker compiles the mutated assembly without Avalonia's XAML
/// compiler, so loading the theme throws there.
/// </remarks>
public class MarkdownViewerTableThemeTests
{
    [AvaloniaFact]
    public void Header_cells_are_bold_and_body_cells_are_not()
    {
        var viewer = new MarkdownViewer();
        viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        viewer.Markdown = "| Name | Value |\n|------|-------|\n| a    | 1     |";
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        try
        {
            TextBlock CellText(bool header) => viewer.GetVisualDescendants().OfType<Border>()
                .Where(b => b.Classes.Contains("markdown-table-cell") && b.Classes.Contains("markdown-table-header") == header)
                .SelectMany(b => b.GetVisualDescendants().OfType<TextBlock>()).First();

            Assert.Equal(FontWeight.Bold, CellText(header: true).FontWeight);
            Assert.Equal(FontWeight.Normal, CellText(header: false).FontWeight);
        }
        finally
        {
            window.Close();
        }
    }
}
