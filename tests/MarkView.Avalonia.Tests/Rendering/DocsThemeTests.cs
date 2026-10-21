// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
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
/// <c>DocsTheme.axaml</c> loaded on top of <c>MarkdownTheme.axaml</c> in a headless window.
/// </summary>
/// <remarks>
/// Only these tests load the theme: Stryker compiles the mutated assembly without Avalonia's XAML
/// compiler, so loading a theme throws there and would hide what a test covers.
/// </remarks>
public class DocsThemeTests
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

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, StyleInclude Docs) : IDisposable
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
        var docs = Theme("DocsTheme");
        viewer.Styles.Add(docs);
        viewer.Markdown = markdown;
        var window = new Window { Width = 1000, Height = 600, Content = viewer };
        window.Show();
        return new Fixture(window, viewer, docs);
    }

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static void AssertColor(string expected, IBrush? brush) => Assert.Equal(Color.Parse(expected), ColorOf(brush));

    [AvaloniaTheory]
    [InlineData("Light", "#FFFFFF", "#3C3C43")]
    [InlineData("Dark", "#1B1B1F", "#DFDFD6")]
    public void Document_takes_the_sites_background_text_colour_and_font(string variant, string background, string foreground)
    {
        using var theme = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        using var f = Show();

        AssertColor(background, f.Viewer.Background);
        AssertColor(foreground, f.Viewer.Foreground);
        Assert.Equal(16, f.Viewer.FontSize);
        // The Inter the application embeds (WithInterFont) first, then an installed one
        Assert.Equal("Inter", f.Viewer.FontFamily.FamilyNames[0]);
        Assert.Contains("fonts:Inter#Inter, Inter, Segoe UI", f.Viewer.FontFamily.ToString());
        var paragraph = f.Paragraph("Text with");
        AssertColor(foreground, paragraph.Foreground);
        Assert.Equal(6, paragraph.LineSpacing);
        Assert.Equal(new Thickness(0, 4), paragraph.Margin);
    }

    [AvaloniaFact]
    public void Text_is_kept_to_a_centred_column_768px_wide()
    {
        using var f = Show();

        var document = Assert.IsType<Grid>(f.Viewer.Content);
        Assert.Equal(768, document.Bounds.Width);
        var scroller = f.Viewer.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "PART_ScrollViewer" && s.TemplatedParent == f.Viewer);
        var left = document.TranslatePoint(default, scroller)!.Value.X;
        Assert.Equal(scroller.Viewport.Width - 768, 2 * left, 1);
    }

    [AvaloniaFact]
    public void A_short_document_still_takes_the_whole_column()
    {
        using var f = Show("Short.");

        Assert.Equal(768, Assert.IsType<Grid>(f.Viewer.Content).Bounds.Width);
    }

    [AvaloniaFact]
    public void A_narrow_viewer_uses_its_whole_width()
    {
        using var f = Show();
        f.Viewer.Width = 500;
        f.Window.UpdateLayout();

        var scroller = f.Viewer.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "PART_ScrollViewer" && s.TemplatedParent == f.Viewer);
        Assert.Equal(scroller.Viewport.Width, Assert.IsType<Grid>(f.Viewer.Content).Bounds.Width, 1);
    }

    [AvaloniaFact]
    public void Headings_use_the_sites_sizes_and_weight()
    {
        using var f = Show("# 1\n\n## 2\n\n### 3\n\n#### 4\n\n##### 5\n\n###### 6");

        double[] sizes = [32, 24, 20, 18, 16, 16];
        for (var level = 1; level <= 6; level++)
        {
            var heading = f.Single<TextBlock>($"markdown-h{level}");
            Assert.Equal(sizes[level - 1], heading.FontSize);
            Assert.Equal(FontWeight.SemiBold, heading.FontWeight);
        }
    }

    [AvaloniaFact]
    public void Level_2_headings_have_a_divider_above_them_unless_they_start_the_document()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        var h2 = f.Single<MarkdownSelectableTextBlock>("markdown-h2");
        AssertColor("#E2E2E3", h2.BorderBrush);
        Assert.Equal(new Thickness(0, 1, 0, 0), h2.BorderThickness);
        Assert.Equal(new Thickness(0, 24, 0, 0), h2.Padding);
        Assert.Equal(new Thickness(0, 32, 0, 0), h2.Margin);
        var h1 = f.Single<MarkdownSelectableTextBlock>("markdown-h1");
        Assert.Equal(new Thickness(0), h1.Margin);
        Assert.Equal(default, h1.BorderThickness);

        using var first = Show("## First\n\nText");
        var leading = first.Single<MarkdownSelectableTextBlock>("markdown-h2");
        Assert.Equal(default, leading.BorderThickness);
        Assert.Equal(new Thickness(0), leading.Padding);
        Assert.Equal(new Thickness(0), leading.Margin);
    }

    [AvaloniaFact]
    public void Links_are_medium_weight_and_inline_code_takes_the_link_colour()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        var paragraph = f.Paragraph("Text with");
        var link = paragraph.Inlines!.OfType<MarkdownHyperlink>().Single(l => l.Classes.Contains("markdown-link"));
        AssertColor("#3451B2", link.Foreground);
        Assert.Equal(FontWeight.Medium, link.FontWeight);
        var code = paragraph.Inlines!.OfType<Run>().Single(r => r.Classes.Contains("markdown-code-inline"));
        AssertColor("#3451B2", code.Foreground);
        AssertColor("#248E96AA", code.Background);
        Assert.Equal(14, code.FontSize);
    }

    [AvaloniaFact]
    public void Code_blocks_get_the_sites_corners_padding_and_font()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        var header = f.Single<Border>("markdown-code-header");
        var block = f.Single<Border>("markdown-code-block");
        Assert.Equal(new CornerRadius(8, 8, 0, 0), header.CornerRadius);
        Assert.Equal(new CornerRadius(0, 0, 8, 8), block.CornerRadius);
        Assert.Equal(new Thickness(24, 4, 24, 20), block.Padding);
        AssertColor("#F6F6F7", block.Background);
        AssertColor("#F6F6F7", header.Background);
        var code = Assert.IsAssignableFrom<TextBlock>(block.Child);
        Assert.Equal(14, code.FontSize);
        Assert.Equal(24, code.LineHeight);
        Assert.Contains("Menlo", code.FontFamily.FamilyNames);
        Assert.Equal(1, f.Single<Button>("markdown-code-copy").BorderThickness.Left);
    }

    [AvaloniaFact]
    public void Code_blocks_without_a_toolbar_keep_padding_on_every_side()
    {
        using var f = Show("```\nplain\n```");
        f.Viewer.ShowCodeBlockCopyButton = false;

        var block = f.Single<Border>("markdown-code-block");
        Assert.Equal(new Thickness(24, 20), block.Padding);
        Assert.Equal(new CornerRadius(8), block.CornerRadius);
    }

    [AvaloniaFact]
    public void Tables_get_a_grid_a_soft_header_and_striped_rows_in_smaller_text()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        Assert.Equal(new Thickness(1), f.Cell(0, 0).BorderThickness);
        Assert.Equal(new Thickness(0, 0, 1, 1), f.Cell(1, 1).BorderThickness);
        AssertColor("#87878C", f.Cell(1, 1).BorderBrush);
        AssertColor("#F6F6F7", f.Cell(0, 0).Background);
        Assert.Null(f.Cell(1, 0).Background);
        AssertColor("#F6F6F7", f.Cell(2, 0).Background);
        var headerText = f.Cell(0, 0).GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.Equal(FontWeight.SemiBold, headerText.FontWeight);
        AssertColor("#67676C", headerText.Foreground);
        Assert.Equal(14, f.Paragraph("a").FontSize);
    }

    [AvaloniaTheory]
    [InlineData("Light", "#3451B2", "#24646CFF")]
    [InlineData("Dark", "#A8B1FF", "#29646CFF")]
    public void Alerts_are_tinted_boxes_without_a_bar(string variant, string accent, string tint)
    {
        using var theme = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        using var f = Show();

        var alert = f.Single<Border>("markdown-alert-note");
        AssertColor(tint, alert.Background);
        Assert.Equal(default, alert.BorderThickness);
        Assert.Equal(new CornerRadius(8), alert.CornerRadius);
        var title = f.Single<TextBlock>("markdown-alert-header");
        AssertColor(accent, title.Foreground);
        Assert.Equal(14, title.FontSize);
        Assert.Equal(FontWeight.SemiBold, title.FontWeight);
        Assert.Equal(14, f.Paragraph("A note.").FontSize);
    }

    [AvaloniaFact]
    public void Quotes_rules_and_footnotes_take_the_sites_greys()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        var quote = f.Single<Border>("markdown-blockquote");
        AssertColor("#87878C", quote.BorderBrush);
        Assert.Equal(new Thickness(2, 0, 0, 0), quote.BorderThickness);
        Assert.Equal(1, quote.Opacity);
        AssertColor("#67676C", f.Paragraph("A quote.").Foreground);
        AssertColor("#67676C", f.Single<TextBlock>("markdown-h6").Foreground);
        var rule = f.All<Separator>().Single(s => !s.Classes.Contains("markdown-footnote-separator"));
        AssertColor("#87878C", rule.Background);
        Assert.Equal(1, rule.Height);
        var separator = f.Single<Separator>("markdown-footnote-separator");
        AssertColor("#E2E2E3", separator.Background);
        var footnote = f.Paragraph("The footnote.");
        Assert.Equal(14, footnote.FontSize);
        AssertColor("#67676C", footnote.Foreground);
    }

    [AvaloniaFact]
    public void In_high_contrast_alerts_are_outlined_and_table_headers_lose_their_tint()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();
        f.Viewer.HighContrastMode = HighContrastMode.Enabled;

        var alert = f.Single<Border>("markdown-alert-note");
        Assert.Equal(new Thickness(1), alert.BorderThickness);
        AssertColor("#000000", alert.BorderBrush);
        var header = f.Cell(0, 0);
        Assert.Equal(Colors.Transparent, ColorOf(header.Background));
        AssertColor("#000000", header.GetVisualDescendants().OfType<TextBlock>().Single().Foreground);
        AssertColor("#000000", f.Single<MarkdownSelectableTextBlock>("markdown-h2").BorderBrush);
    }

    [AvaloniaFact]
    public void Switching_the_theme_variant_recolours_the_document_live()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();
        var code = f.Single<Border>("markdown-code-block");
        AssertColor("#F6F6F7", code.Background);

        theme.Switch(ThemeVariant.Dark);

        AssertColor("#161618", code.Background);
        AssertColor("#1B1B1F", f.Viewer.Background);
    }

    [AvaloniaFact]
    public void Removing_the_theme_brings_the_default_look_back()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();
        var document = Assert.IsType<Grid>(f.Viewer.Content);

        f.Viewer.Styles.Remove(f.Docs);
        f.Window.UpdateLayout();

        AssertColor("#F3F3F3", f.Single<Border>("markdown-code-block").Background);
        Assert.Equal(28, f.Single<TextBlock>("markdown-h1").FontSize);
        Assert.Equal(Brushes.Transparent, f.Viewer.Background);
        Assert.True(double.IsPositiveInfinity(document.MaxWidth));
    }
}
