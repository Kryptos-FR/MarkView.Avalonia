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
/// <c>ChatTheme.axaml</c> loaded on top of <c>MarkdownTheme.axaml</c> in a headless window.
/// </summary>
/// <remarks>
/// Only these tests load the theme: Stryker compiles the mutated assembly without Avalonia's XAML
/// compiler, so loading a theme throws there and would hide what a test covers.
/// </remarks>
public class ChatThemeTests
{
    private const string Document = """
        # Title

        Text with a [link](https://example.com), `code` and ==marked== words.[^1]

        ## Section

        ###### Level 6

        > A quote.

        > [!NOTE]
        > A note.

        | Name | Value |
        |------|-------|
        | a    | 1     |

        ---

        ```cs
        var x = 1;
        ```

        [^1]: The footnote.
        """;

    private static StyleInclude Theme(string name) => new((Uri?)null)
    {
        Source = new Uri($"avares://MarkView.Avalonia/Themes/{name}.axaml"),
    };

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, StyleInclude Chat) : IDisposable
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
        var chat = Theme("ChatTheme");
        viewer.Styles.Add(chat);
        viewer.Markdown = markdown;
        // A chat bubble: the host sets the font size, text colour and background
        var window = new Window
        {
            Width = 600,
            Height = 600,
            FontSize = 15,
            Foreground = Brushes.DarkSlateGray,
            Content = new Border { Background = Brushes.Lavender, Child = viewer },
        };
        window.Show();
        return new Fixture(window, viewer, chat);
    }

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static void AssertColor(string expected, IBrush? brush) => Assert.Equal(Color.Parse(expected), ColorOf(brush));

    [AvaloniaFact]
    public void Document_keeps_the_hosts_background_font_size_and_text_colour()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        Assert.Equal(Brushes.Transparent, f.Viewer.Background);
        var paragraph = f.Paragraph("Text with");
        Assert.Equal(15, paragraph.FontSize);
        Assert.Equal(Brushes.DarkSlateGray, paragraph.Foreground);
        Assert.Equal(f.Window.FontFamily, paragraph.FontFamily);
    }

    [AvaloniaFact]
    public void Headings_are_small_and_semibold()
    {
        using var f = Show("# 1\n\n## 2\n\n### 3\n\n#### 4\n\n##### 5\n\n###### 6");

        double[] sizes = [20, 18, 16, 15, 14, 14];
        for (var level = 1; level <= 6; level++)
        {
            var heading = f.Single<TextBlock>($"markdown-h{level}");
            Assert.Equal(sizes[level - 1], heading.FontSize);
            Assert.Equal(FontWeight.SemiBold, heading.FontWeight);
        }
    }

    [AvaloniaFact]
    public void Blocks_are_only_spaced_by_the_document_except_above_headings()
    {
        using var f = Show();

        Assert.Equal(new Thickness(0), f.Single<TextBlock>("markdown-h1").Margin);
        Assert.Equal(new Thickness(0, 8, 0, 0), f.Single<TextBlock>("markdown-h2").Margin);
        Assert.Equal(new Thickness(0), f.Paragraph("Text with").Margin);
        Assert.Equal(new Thickness(0), f.Single<StackPanel>("markdown-code-container").Margin);
        Assert.Equal(new Thickness(0), f.Single<Border>("markdown-blockquote").Margin);
        Assert.Equal(new Thickness(0), f.Single<Border>("markdown-alert").Margin);
        Assert.Equal(new Thickness(0), f.Single<Grid>("markdown-table").Margin);
    }

    [AvaloniaFact]
    public void Code_blocks_are_outlined_cards_with_the_toolbar_inside()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        var header = f.Single<Border>("markdown-code-header");
        var block = f.Single<Border>("markdown-code-block");
        Assert.Equal(new CornerRadius(8, 8, 0, 0), header.CornerRadius);
        Assert.Equal(new Thickness(1, 1, 1, 0), header.BorderThickness);
        Assert.Equal(new CornerRadius(0, 0, 8, 8), block.CornerRadius);
        Assert.Equal(new Thickness(1, 0, 1, 1), block.BorderThickness);
        AssertColor("#14000000", header.BorderBrush);
        AssertColor("#14000000", block.BorderBrush);
        AssertColor("#0A000000", header.Background);
        AssertColor("#0A000000", block.Background);
        var code = Assert.IsAssignableFrom<TextBlock>(block.Child);
        Assert.Equal(13, code.FontSize);
        Assert.Contains("Cascadia Code", code.FontFamily.FamilyNames);
    }

    [AvaloniaFact]
    public void Code_blocks_without_a_toolbar_are_outlined_on_every_side()
    {
        using var f = Show("```\nplain\n```");
        f.Viewer.ShowCodeBlockCopyButton = false;

        var block = f.Single<Border>("markdown-code-block");
        Assert.Equal(new Thickness(1), block.BorderThickness);
        Assert.Equal(new CornerRadius(8), block.CornerRadius);
        Assert.Equal(new Thickness(12, 10), block.Padding);
    }

    [AvaloniaFact]
    public void Inline_code_is_a_little_smaller_than_the_text()
    {
        using var f = Show();

        var run = f.Paragraph("Text with").Inlines!.OfType<Run>().Single(r => r.Classes.Contains("markdown-code-inline"));
        Assert.Equal(13, run.FontSize);
        Assert.Contains("Cascadia Code", run.FontFamily.FamilyNames);
    }

    [AvaloniaFact]
    public void Tables_have_lines_between_rows_and_a_tinted_header()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        Assert.Equal(new Thickness(0, 0, 0, 1), f.Cell(0, 0).BorderThickness);
        Assert.Equal(new Thickness(0, 0, 0, 1), f.Cell(1, 1).BorderThickness);
        AssertColor("#7A7A83", f.Cell(1, 1).BorderBrush);
        AssertColor("#0A000000", f.Cell(0, 0).Background);
        Assert.Null(f.Cell(1, 0).Background);
        Assert.Equal(FontWeight.SemiBold, f.Cell(0, 0).GetVisualDescendants().OfType<TextBlock>().Single().FontWeight);
    }

    [AvaloniaTheory]
    [InlineData("Light", "#1D4ED8", "#0F1D4ED8")]
    [InlineData("Dark", "#6EADFB", "#146EADFB")]
    public void Alerts_have_a_thin_bar_on_a_faint_tint(string variant, string accent, string tint)
    {
        using var theme = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        using var f = Show();

        var alert = f.Single<Border>("markdown-alert-note");
        AssertColor(accent, alert.BorderBrush);
        AssertColor(tint, alert.Background);
        Assert.Equal(new Thickness(3, 0, 0, 0), alert.BorderThickness);
        Assert.Equal(new CornerRadius(0, 8, 8, 0), alert.CornerRadius);
        var title = f.Single<TextBlock>("markdown-alert-header");
        AssertColor(accent, title.Foreground);
        Assert.Equal(14, title.FontSize);
    }

    [AvaloniaFact]
    public void Quotes_level_6_headings_and_footnotes_use_the_muted_text_colour()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();

        AssertColor("#52525B", f.Single<TextBlock>("markdown-h6").Foreground);
        var quote = f.Single<Border>("markdown-blockquote");
        Assert.Equal(new Thickness(3, 0, 0, 0), quote.BorderThickness);
        Assert.Equal(1, quote.Opacity);
        AssertColor("#52525B", f.Paragraph("A quote.").Foreground);
        var footnote = f.Paragraph("The footnote.");
        AssertColor("#52525B", footnote.Foreground);
        Assert.Equal(12, footnote.FontSize);
        var rule = f.All<Separator>().Single(s => !s.Classes.Contains("markdown-footnote-separator"));
        Assert.Equal(1, rule.Height);
        AssertColor("#7A7A83", rule.Background);
    }

    [AvaloniaFact]
    public void Switching_the_theme_variant_recolours_the_document_live()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();
        var code = f.Single<Border>("markdown-code-block");
        var link = f.Paragraph("Text with").Inlines!.OfType<MarkdownHyperlink>().Single(l => l.Classes.Contains("markdown-link"));
        AssertColor("#0A000000", code.Background);
        AssertColor("#1D4ED8", link.Foreground);

        theme.Switch(ThemeVariant.Dark);

        AssertColor("#0FFFFFFF", code.Background);
        AssertColor("#6EADFB", link.Foreground);
        Assert.Equal(Brushes.Transparent, f.Viewer.Background);
    }

    [AvaloniaFact]
    public void Removing_the_theme_brings_the_default_look_back()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        using var f = Show();
        var code = f.Single<Border>("markdown-code-block");
        var h1 = f.Single<TextBlock>("markdown-h1");

        f.Viewer.Styles.Remove(f.Chat);

        AssertColor("#F3F3F3", code.Background);
        Assert.Equal(28, h1.FontSize);
        Assert.Equal(new Thickness(0, 2), f.Paragraph("Text with").Margin);
    }
}
