// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Markdig;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using MarkView.Avalonia.Tests.Shared;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// <c>GitHubTheme.axaml</c> loaded on top of <c>MarkdownTheme.axaml</c> in a headless window.
/// </summary>
/// <remarks>
/// Only these tests load the theme: Stryker compiles the mutated assembly without Avalonia's XAML
/// compiler, so loading a theme throws there and would hide what a test covers.
/// </remarks>
public class GitHubThemeTests
{
    private const string Document = """
        # Title

        Text with a [link](https://example.com), `code` and ==marked== words.[^1]

        ## Section `with code`

        ### Level 3

        ###### Level 6

        > A quote.

        > [!NOTE]
        > A note.

        | Name | Value |
        |------|-------|
        | a    | 1     |
        | b    | 2     |

        ---

        ```cs
        var x = 1;
        ```

        - [x] Done

        [^1]: The footnote.
        """;

    private static StyleInclude Theme(string name) => new((Uri?)null)
    {
        Source = new Uri($"avares://MarkView.Avalonia/Themes/{name}.axaml"),
    };

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, StyleInclude GitHub) : IDisposable
    {
        public IEnumerable<T> All<T>() where T : Visual => Viewer.GetVisualDescendants().OfType<T>();

        public T Single<T>(string styleClass) where T : StyledElement =>
            Viewer.GetVisualDescendants().OfType<T>().Single(c => c.Classes.Contains(styleClass));

        public MarkdownSelectableTextBlock Paragraph(string text) =>
            All<MarkdownSelectableTextBlock>().Single(tb => MarkdownSelectableTextBlock.ExtractPlainText(tb.Inlines!).StartsWith(text, StringComparison.Ordinal));

        public Border Cell(int row, int column) =>
            All<Border>().Single(b => b.Classes.Contains("markdown-table-cell") && Grid.GetRow(b) == row && Grid.GetColumn(b) == column);

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown = Document)
    {
        var viewer = new MarkdownViewer
        {
            Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseAlertBlocks().Build(),
        };
        viewer.Styles.Add(Theme("MarkdownTheme"));
        var gitHub = Theme("GitHubTheme");
        viewer.Styles.Add(gitHub);
        viewer.Markdown = markdown;
        var window = new Window { Width = 800, Height = 600, Content = viewer };
        window.Show();
        return new Fixture(window, viewer, gitHub);
    }

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static void AssertColor(string expected, IBrush? brush) => Assert.Equal(Color.Parse(expected), ColorOf(brush));

    [AvaloniaTheory]
    [InlineData("Light", "#FFFFFF", "#1F2328")]
    [InlineData("Dark", "#0D1117", "#F0F6FC")]
    public void Document_takes_GitHubs_background_text_colour_and_font_size(string variant, string background, string foreground)
    {
        using var theme = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        using var f = Show();

        AssertColor(background, f.Viewer.Background);
        AssertColor(foreground, f.Viewer.Foreground);
        Assert.Equal(16, f.Viewer.FontSize);
        Assert.Contains("Segoe UI", f.Viewer.FontFamily.FamilyNames);
        // Paragraphs inherit them
        var paragraph = f.Paragraph("Text with");
        AssertColor(foreground, paragraph.Foreground);
        Assert.Equal(16, paragraph.FontSize);
    }

    [AvaloniaFact]
    public void Switching_the_theme_variant_recolours_the_document_live()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();
        var code = f.Single<Border>("markdown-code-block");
        var link = f.Paragraph("Text with").Inlines!.OfType<MarkdownHyperlink>().Single(l => l.Classes.Contains("markdown-link"));
        AssertColor("#F6F8FA", code.Background);
        AssertColor("#0969DA", link.Foreground);

        theme.Switch(ThemeVariant.Dark);

        AssertColor("#151B23", code.Background);
        AssertColor("#4493F8", link.Foreground);
        AssertColor("#0D1117", f.Viewer.Background);
    }

    [AvaloniaFact]
    public void Headings_use_GitHubs_sizes_and_weight()
    {
        using var f = Show("# 1\n\n## 2\n\n### 3\n\n#### 4\n\n##### 5\n\n###### 6");

        double[] sizes = [32, 24, 20, 16, 14, 13.6];
        for (var level = 1; level <= 6; level++)
        {
            var heading = f.Single<TextBlock>($"markdown-h{level}");
            Assert.Equal(sizes[level - 1], heading.FontSize);
            Assert.Equal(FontWeight.SemiBold, heading.FontWeight);
        }
    }

    [AvaloniaFact]
    public void Level_1_and_2_headings_are_ruled_off_and_the_others_are_not()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        foreach (var level in new[] { 1, 2 })
        {
            var heading = f.Single<MarkdownSelectableTextBlock>($"markdown-h{level}");
            AssertColor("#B3D1D9E0", heading.BorderBrush);
            Assert.Equal(new Thickness(0, 0, 0, 1), heading.BorderThickness);
            Assert.True(heading.Padding.Bottom > heading.BorderThickness.Bottom);
        }
        var h3 = f.Single<MarkdownSelectableTextBlock>("markdown-h3");
        Assert.Null(h3.BorderBrush);
        Assert.Equal(default, h3.BorderThickness);
    }

    [AvaloniaFact]
    public void A_heading_has_space_above_it_unless_it_starts_its_container()
    {
        using var f = Show();

        Assert.Equal(new Thickness(0, 0, 0, 8), f.Single<TextBlock>("markdown-h1").Margin);
        Assert.Equal(new Thickness(0, 16, 0, 8), f.Single<TextBlock>("markdown-h2").Margin);
    }

    [AvaloniaFact]
    public void Level_6_headings_and_quotes_use_the_muted_text_colour()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        AssertColor("#59636E", f.Single<TextBlock>("markdown-h6").Foreground);
        var quote = f.Single<Border>("markdown-blockquote");
        AssertColor("#818B98", quote.BorderBrush);
        Assert.Equal(new Thickness(4, 0, 0, 0), quote.BorderThickness);
        Assert.Equal(1, quote.Opacity);
        AssertColor("#59636E", f.Paragraph("A quote.").Foreground);
        Assert.Equal(new Thickness(0), f.Paragraph("A quote.").Margin);
    }

    [AvaloniaFact]
    public void Inline_code_is_smaller_than_paragraph_text_but_not_than_heading_text()
    {
        using var f = Show();

        var inParagraph = f.Paragraph("Text with").Inlines!.OfType<Run>().Single(r => r.Classes.Contains("markdown-code-inline"));
        var inHeading = f.Single<TextBlock>("markdown-h2").Inlines!.OfType<Run>().Single(r => r.Classes.Contains("markdown-code-inline"));

        Assert.Equal(13.6, inParagraph.FontSize);
        Assert.Equal(24, inHeading.FontSize);
        Assert.Contains("Consolas", inParagraph.FontFamily.FamilyNames);
    }

    [AvaloniaFact]
    public void Code_blocks_get_GitHubs_corners_padding_and_font()
    {
        using var f = Show();

        var container = f.Single<StackPanel>("markdown-code-container");
        var header = f.Single<Border>("markdown-code-header");
        var block = f.Single<Border>("markdown-code-block");
        Assert.Same(container, header.Parent);
        Assert.Equal(new CornerRadius(6, 6, 0, 0), header.CornerRadius);
        Assert.Equal(new CornerRadius(0, 0, 6, 6), block.CornerRadius);
        Assert.Equal(new Thickness(16, 0, 16, 16), block.Padding);
        var code = Assert.IsAssignableFrom<TextBlock>(block.Child);
        Assert.Equal(13.6, code.FontSize);
        Assert.Contains("Menlo", code.FontFamily.FamilyNames);
        Assert.Equal(1, f.Single<Button>("markdown-code-copy").BorderThickness.Left);
    }

    [AvaloniaFact]
    public void Code_blocks_without_a_toolbar_keep_padding_on_every_side()
    {
        using var f = Show("```\nplain\n```");
        f.Viewer.ShowCodeBlockCopyButton = false;

        var block = f.Single<Border>("markdown-code-block");
        Assert.Equal(new Thickness(16), block.Padding);
        Assert.Equal(new CornerRadius(6), block.CornerRadius);
    }

    [AvaloniaFact]
    public void Tables_get_a_grid_on_every_side_and_striped_body_rows()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        Assert.Equal(new Thickness(1), f.Cell(0, 0).BorderThickness);
        Assert.Equal(new Thickness(0, 1, 1, 1), f.Cell(0, 1).BorderThickness);
        Assert.Equal(new Thickness(1, 0, 1, 1), f.Cell(1, 0).BorderThickness);
        Assert.Equal(new Thickness(0, 0, 1, 1), f.Cell(1, 1).BorderThickness);
        AssertColor("#818B98", f.Cell(1, 1).BorderBrush);

        Assert.Equal(Brushes.Transparent, f.Cell(0, 0).Background);
        Assert.Null(f.Cell(1, 0).Background);
        AssertColor("#F6F8FA", f.Cell(2, 0).Background);
        var headerText = f.Cell(0, 0).GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.Equal(FontWeight.SemiBold, headerText.FontWeight);
        Assert.Equal(new Thickness(0), f.Paragraph("a").Margin);
    }

    [AvaloniaTheory]
    [InlineData("Light", "#0969DA")]
    [InlineData("Dark", "#4493F8")]
    public void Alerts_have_a_coloured_bar_and_title_and_no_background(string variant, string accent)
    {
        using var theme = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        using var f = Show();

        var alert = f.Single<Border>("markdown-alert-note");
        AssertColor(accent, alert.BorderBrush);
        Assert.Equal(0, ColorOf(alert.Background).A);
        Assert.Equal(new Thickness(4, 0, 0, 0), alert.BorderThickness);
        var title = f.Single<TextBlock>("markdown-alert-header");
        AssertColor(accent, title.Foreground);
        Assert.Equal(16, title.FontSize);
        Assert.Equal(new Thickness(0), f.Paragraph("A note.").Margin);
        Assert.Equal(new Thickness(0, 4), f.Paragraph("Text with").Margin);
    }

    [AvaloniaFact]
    public void Rules_marked_text_and_task_boxes_take_GitHubs_colours()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        var rule = f.All<Separator>().Single(s => !s.Classes.Contains("markdown-footnote-separator"));
        AssertColor("#818B98", rule.Background);
        Assert.Equal(4, rule.Height);
        var marked = f.Paragraph("Text with").Inlines!.OfType<Span>().Single(s => s.Classes.Contains("markdown-marked"));
        AssertColor("#FFF8C5", marked.Background);
        AssertColor("#59636E", f.Single<TextBlock>("markdown-task-list").Foreground);
    }

    [AvaloniaFact]
    public void Footnotes_sit_below_a_thin_line_in_small_muted_text()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        var separator = f.Single<Separator>("markdown-footnote-separator");
        Assert.Equal(1, separator.Height);
        AssertColor("#D1D9E0", separator.Background);
        var footnote = f.Paragraph("The footnote.");
        Assert.Equal(12, footnote.FontSize);
        AssertColor("#59636E", footnote.Foreground);
        Assert.Equal(1, f.Single<StackPanel>("markdown-footnote-group").Opacity);
    }

    [AvaloniaFact]
    public void Resources_defined_closer_to_the_viewer_override_the_preset()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        f.Viewer.Resources.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary
        {
            ["MarkdownCodeBlockBackground"] = new SolidColorBrush(Colors.Red),
        };

        AssertColor("#FF0000", f.Single<Border>("markdown-code-block").Background);
    }

    [AvaloniaFact]
    public void Removing_the_theme_brings_the_default_look_back()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();
        var code = f.Single<Border>("markdown-code-block");
        var h1 = f.Single<MarkdownSelectableTextBlock>("markdown-h1");

        f.Viewer.Styles.Remove(f.GitHub);

        AssertColor("#F3F3F3", code.Background);
        Assert.Equal(28, h1.FontSize);
        Assert.Null(h1.BorderBrush);
        Assert.Equal(Brushes.Transparent, f.Viewer.Background);
    }
}
