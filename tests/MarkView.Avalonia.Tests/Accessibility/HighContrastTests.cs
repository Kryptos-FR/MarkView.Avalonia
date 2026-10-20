// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Markdig;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using MarkView.Avalonia.Tests.Shared;
using Xunit;

namespace MarkView.Avalonia.Tests.Accessibility;

public class HighContrastTests
{
    private static StyleInclude MarkdownThemeInclude() => new((Uri?)null)
    {
        Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml"),
    };

    // Only tests of the theme's colours load it: Stryker compiles the mutated assembly without
    // Avalonia's XAML compiler, so loading the theme throws there and hides what a test covers.
    private static MarkdownViewer ShowViewer(string markdown, HighContrastMode mode = HighContrastMode.System,
        bool withTheme = false)
    {
        var viewer = new MarkdownViewer { HighContrastMode = mode, Markdown = markdown };
        if (withTheme)
            viewer.Styles.Add(MarkdownThemeInclude());
        new Window { Width = 400, Height = 300, Content = viewer }.Show();
        return viewer;
    }

    private static Color LinkColor(MarkdownViewer viewer)
    {
        var link = viewer.GetVisualDescendants().OfType<TextBlock>()
            .SelectMany(t => t.Inlines!).OfType<MarkdownHyperlink>().Single();
        return Assert.IsAssignableFrom<ISolidColorBrush>(link.Foreground).Color;
    }

    [AvaloniaFact]
    public void Viewer_follows_the_system_by_default_and_headless_reports_no_preference()
    {
        var viewer = ShowViewer("Text");

        Assert.Equal(HighContrastMode.System, viewer.HighContrastMode);
        Assert.False(viewer.IsHighContrast);
        Assert.DoesNotContain(":high-contrast", viewer.Classes);
    }

    [AvaloniaFact]
    public void Enabled_and_Disabled_override_the_system_and_toggle_the_pseudo_class()
    {
        var viewer = ShowViewer("Text", HighContrastMode.Enabled);
        Assert.True(viewer.IsHighContrast);
        Assert.Contains(":high-contrast", viewer.Classes);

        viewer.HighContrastMode = HighContrastMode.Disabled;
        Assert.False(viewer.IsHighContrast);
        Assert.DoesNotContain(":high-contrast", viewer.Classes);
    }

    [AvaloniaFact]
    public void System_mode_tracks_the_platform_contrast_preference()
    {
        var viewer = ShowViewer("Text");

        viewer.OnPlatformColorValuesChanged(null, new PlatformColorValues { ContrastPreference = ColorContrastPreference.High });
        Assert.True(viewer.IsHighContrast);
        Assert.Contains(":high-contrast", viewer.Classes);

        viewer.OnPlatformColorValuesChanged(null, new PlatformColorValues { ContrastPreference = ColorContrastPreference.NoPreference });
        Assert.False(viewer.IsHighContrast);

        // An explicit mode wins over the platform until it's set back to System.
        viewer.OnPlatformColorValuesChanged(null, new PlatformColorValues { ContrastPreference = ColorContrastPreference.High });
        viewer.HighContrastMode = HighContrastMode.Disabled;
        Assert.False(viewer.IsHighContrast);
        viewer.HighContrastMode = HighContrastMode.System;
        Assert.True(viewer.IsHighContrast);
    }

    [AvaloniaTheory]
    [InlineData("Light", "SystemAccentColorDark2")]
    [InlineData("Dark", "SystemAccentColorLight2")]
    public void Link_uses_an_accent_shade_with_enough_contrast(string variant, string accentKey)
    {
        var theme = variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        using var _ = new ThemeScope(theme);
        var viewer = ShowViewer("[link](https://example.com)", withTheme: true);

        Assert.True(Application.Current!.TryFindResource(accentKey, theme, out var accent));
        Assert.Equal((Color)accent!, LinkColor(viewer));
    }

    [AvaloniaTheory]
    [InlineData("Light", "#00009F", "#FFFFFF", "#000000")]
    [InlineData("Dark", "#FFFF00", "#000000", "#FFFFFF")]
    public void High_contrast_palette_replaces_theme_colours(string variant, string link, string background, string foreground)
    {
        using var _ = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        var viewer = ShowViewer("[link](https://example.com)\n\n> [!TIP]\n> Body", HighContrastMode.Enabled, withTheme: true);

        Assert.Equal(Color.Parse(link), LinkColor(viewer));
        Assert.Equal(Color.Parse(background), ((ISolidColorBrush)viewer.Background!).Color);
        Assert.Equal(Color.Parse(foreground), ((ISolidColorBrush)viewer.Foreground!).Color);

        var alert = viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-alert"));
        Assert.Equal(Color.Parse(foreground), ((ISolidColorBrush)alert.BorderBrush!).Color);
        var title = viewer.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-alert-header"));
        Assert.Equal(Color.Parse(foreground), ((ISolidColorBrush)title.Foreground!).Color);

        viewer.HighContrastMode = HighContrastMode.Disabled;
        Assert.NotEqual(Color.Parse(link), LinkColor(viewer));
    }

    [AvaloniaTheory]
    [InlineData("Light", "#FFFFFF", "#000000")]
    [InlineData("Dark", "#000000", "#FFFFFF")]
    public void High_contrast_palette_covers_the_code_block_toolbar_and_line_numbers(string variant, string background, string foreground)
    {
        using var _ = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        var viewer = new MarkdownViewer
        {
            HighContrastMode = HighContrastMode.Enabled,
            ShowCodeBlockLineNumbers = true,
            CodeBlockMaxHeight = 100,
            Markdown = "```csharp\nvar x = 1;\nvar y = 2;\n```",
        };
        viewer.Styles.Add(MarkdownThemeInclude());
        new Window { Width = 400, Height = 300, Content = viewer }.Show();

        static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
        var header = viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-code-header"));
        Assert.Equal(Color.Parse(background), ColorOf(header.Background));
        Assert.Equal(Color.Parse(foreground), ColorOf(header.BorderBrush));
        var label = viewer.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-code-language"));
        Assert.Equal(Color.Parse(foreground), ColorOf(label.Foreground));
        var copy = viewer.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("markdown-code-copy"));
        Assert.Equal(Color.Parse(foreground), ColorOf(copy.Foreground));

        // The code and its gutter sit in a grid inside a scroll viewer, not directly in the block.
        var block = viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-code-block"));
        var texts = block.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(texts, t => t.Classes.Contains("markdown-code-line-numbers"));
        Assert.Equal(2, texts.Count);
        Assert.All(texts, t => Assert.Equal(Color.Parse(foreground), ColorOf(t.Foreground)));
    }

    [AvaloniaTheory]
    [InlineData("Light", "#FFFFFF", "#000000")]
    [InlineData("Dark", "#000000", "#FFFFFF")]
    public async Task High_contrast_palette_covers_an_image_that_could_not_be_loaded(string variant, string background, string foreground)
    {
        using var _ = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        var viewer = ShowViewer("![A diagram](unknown://diagram.png)", HighContrastMode.Enabled, withTheme: true);

        Border? Placeholder() => viewer.GetVisualDescendants().OfType<Border>().SingleOrDefault(b => b.Classes.Contains("markdown-image-failed"));
        await AsyncTestHelpers.WaitUntilAsync(() => Placeholder() is not null);

        static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
        var placeholder = Placeholder()!;
        Assert.Equal(Color.Parse(background), ColorOf(placeholder.Background));
        Assert.Equal(Color.Parse(foreground), ColorOf(placeholder.BorderBrush));
        // The alt text and the broken-image glyph are drawn in the placeholder's text colour.
        Assert.Equal(Color.Parse(foreground), ColorOf(placeholder.GetValue(TextElement.ForegroundProperty)));
        Assert.All(placeholder.GetVisualDescendants().OfType<TextBlock>(), t => Assert.Equal(Color.Parse(foreground), ColorOf(t.Foreground)));
    }

    [AvaloniaTheory]
    [InlineData("GitHubTheme", "Light", "#FFFFFF", "#0E1116", "#0349B4", "#20252C", "#744500", "#FFFFFF")]
    [InlineData("GitHubTheme", "Dark", "#0A0C10", "#F0F3F6", "#71B7FF", "#B7BDC8", "#F0B72F", "#0A0C10")]
    [InlineData("DocsTheme", "Light", "#FFFFFF", "#000000", "#00009F", "#000000", "#37006E", "#FFFFFF")]
    [InlineData("DocsTheme", "Dark", "#000000", "#FFFFFF", "#FFFF00", "#FFFFFF", "#1AEBFF", "#000000")]
    public void High_contrast_wins_over_a_preset_with_its_own_page(string preset, string variant, string background, string foreground,
        string link, string border, string highlightBackground, string highlightForeground)
    {
        using var _ = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        var viewer = new MarkdownViewer
        {
            HighContrastMode = HighContrastMode.Enabled,
            Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseAlertBlocks().Build(),
            Markdown = """
                # Title

                Text with a [link](https://example.com) and ==marked== words.[^1]

                ## Section

                ###### Level 6

                > A quote.

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
                """,
        };
        viewer.Styles.Add(MarkdownThemeInclude());
        viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri($"avares://MarkView.Avalonia/Themes/{preset}.axaml") });
        new Window { Width = 800, Height = 600, Content = viewer }.Show();

        static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
        void AssertColor(string expected, IBrush? brush) => Assert.Equal(Color.Parse(expected), ColorOf(brush));
        T Single<T>(string styleClass) where T : StyledElement =>
            viewer.GetVisualDescendants().OfType<T>().Single(c => c.Classes.Contains(styleClass));
        MarkdownSelectableTextBlock Paragraph(string text) => viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>()
            .Single(t => MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!).StartsWith(text, StringComparison.Ordinal));

        AssertColor(background, viewer.Background);
        AssertColor(foreground, viewer.Foreground);
        var links = viewer.GetVisualDescendants().OfType<TextBlock>().SelectMany(t => t.Inlines!).OfType<MarkdownHyperlink>().ToList();
        Assert.Equal(2, links.Count); // the link and the footnote reference
        Assert.All(links, l => AssertColor(link, l.Foreground));
        var marked = Paragraph("Text with").Inlines!.OfType<Span>().Single(s => s.Classes.Contains("markdown-marked"));
        AssertColor(highlightBackground, marked.Background);
        AssertColor(highlightForeground, marked.Foreground);

        // What the preset recolours: muted text, rules and grids
        AssertColor(foreground, Single<TextBlock>("markdown-h6").Foreground);
        // The rule under h2, which both presets draw
        AssertColor(border, Single<MarkdownSelectableTextBlock>("markdown-h2").BorderBrush);
        var quote = Single<Border>("markdown-blockquote");
        AssertColor(border, quote.BorderBrush);
        AssertColor(foreground, Paragraph("A quote.").Foreground);
        Assert.All(viewer.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("markdown-table-cell")),
            cell => AssertColor(border, cell.BorderBrush));
        Assert.All(viewer.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("markdown-table-row-even")),
            cell => Assert.Equal(Colors.Transparent, ColorOf(cell.Background)));
        Assert.All(viewer.GetVisualDescendants().OfType<Separator>(), rule => AssertColor(border, rule.Background));
        AssertColor(foreground, Single<TextBlock>("markdown-task-list").Foreground);
        AssertColor(foreground, Paragraph("The footnote.").Foreground);
        AssertColor(border, Single<Button>("markdown-code-copy").BorderBrush);
        AssertColor(background, Single<Border>("markdown-code-block").Background);
    }

    [AvaloniaTheory]
    [InlineData("Light", "#FFFFFF", "#000000", "#00009F", "#000000")]
    [InlineData("Dark", "#000000", "#FFFFFF", "#FFFF00", "#FFFFFF")]
    public void High_contrast_wins_over_the_Chat_preset(string variant, string background, string foreground, string link, string border)
    {
        using var _ = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        var viewer = new MarkdownViewer
        {
            HighContrastMode = HighContrastMode.Enabled,
            Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseAlertBlocks().Build(),
            Markdown = """
                Text with a [link](https://example.com).[^1]

                ###### Level 6

                > A quote.

                | Name | Value |
                |------|-------|
                | a    | 1     |

                ---

                ```cs
                var x = 1;
                ```

                [^1]: The footnote.
                """,
        };
        viewer.Styles.Add(MarkdownThemeInclude());
        viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/ChatTheme.axaml") });
        new Window { Width = 800, Height = 600, Content = viewer }.Show();

        static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
        void AssertColor(string expected, IBrush? brush) => Assert.Equal(Color.Parse(expected), ColorOf(brush));
        T Single<T>(string styleClass) where T : StyledElement =>
            viewer.GetVisualDescendants().OfType<T>().Single(c => c.Classes.Contains(styleClass));
        MarkdownSelectableTextBlock Paragraph(string text) => viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>()
            .Single(t => MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!).StartsWith(text, StringComparison.Ordinal));

        AssertColor(background, viewer.Background);
        AssertColor(foreground, viewer.Foreground);
        var links = viewer.GetVisualDescendants().OfType<TextBlock>().SelectMany(t => t.Inlines!).OfType<MarkdownHyperlink>().ToList();
        Assert.Equal(2, links.Count); // the link and the footnote reference
        Assert.All(links, l => AssertColor(link, l.Foreground));

        // What the preset recolours: muted text, the code card's outline, rules and table lines
        AssertColor(foreground, Single<TextBlock>("markdown-h6").Foreground);
        AssertColor(border, Single<Border>("markdown-blockquote").BorderBrush);
        AssertColor(foreground, Paragraph("A quote.").Foreground);
        Assert.All(viewer.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("markdown-table-cell")),
            cell => AssertColor(border, cell.BorderBrush));
        Assert.All(viewer.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("markdown-table-header")),
            cell => Assert.Equal(Colors.Transparent, ColorOf(cell.Background)));
        Assert.All(viewer.GetVisualDescendants().OfType<Separator>(), rule => AssertColor(border, rule.Background));
        AssertColor(foreground, Paragraph("The footnote.").Foreground);
        AssertColor(border, Single<Border>("markdown-code-header").BorderBrush);
        AssertColor(border, Single<Border>("markdown-code-block").BorderBrush);
        AssertColor(background, Single<Border>("markdown-code-block").Background);
    }

    private static readonly string[] Presets = ["MarkdownTheme", "GitHubTheme", "ChatTheme", "DocsTheme"];

    private static StyleInclude ThemeInclude(string theme) => new((Uri?)null)
    {
        Source = new Uri($"avares://MarkView.Avalonia/Themes/{theme}.axaml"),
    };

    [AvaloniaFact]
    public void Palette_is_only_merged_into_the_viewer_while_high_contrast_is_on()
    {
        var viewer = ShowViewer("> A quote.", withTheme: true);
        var quote = viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-blockquote"));
        Assert.Empty(viewer.Resources.MergedDictionaries);
        Assert.Equal(0.85, quote.Opacity);

        viewer.HighContrastMode = HighContrastMode.Enabled;
        var palette = Assert.IsAssignableFrom<IResourceDictionary>(Assert.Single(viewer.Resources.MergedDictionaries));
        Assert.Equal(1, quote.Opacity);
        // A string entry takes the value of the key it names; one naming a key no theme defines is left out.
        Assert.True(viewer.TryFindResource("MarkdownHighContrastBorder", viewer.ActualThemeVariant, out var border));
        Assert.Same(border, palette["MarkdownBlockquoteBorder"]);
        Assert.False(palette.ContainsKey("MarkdownAlertBorderThickness"));

        viewer.HighContrastMode = HighContrastMode.Disabled;
        Assert.Empty(viewer.Resources.MergedDictionaries);
        Assert.Equal(0.85, quote.Opacity);
    }

    [AvaloniaFact]
    public void Palette_follows_a_change_of_theme_variant()
    {
        using var _ = new ThemeScope(ThemeVariant.Light);
        var viewer = ShowViewer("[link](https://example.com)", HighContrastMode.Enabled, withTheme: true);
        Assert.Equal(Color.Parse("#00009F"), LinkColor(viewer));

        var window = (Window)TopLevel.GetTopLevel(viewer)!;
        window.RequestedThemeVariant = ThemeVariant.Dark;
        Assert.Equal(Color.Parse("#FFFF00"), LinkColor(viewer));

        window.RequestedThemeVariant = ThemeVariant.Light;
        Assert.Equal(Color.Parse("#00009F"), LinkColor(viewer));
    }

    [AvaloniaFact]
    public void Palette_follows_a_preset_added_or_removed_while_high_contrast_is_on()
    {
        using var _ = new ThemeScope(ThemeVariant.Dark);
        var viewer = ShowViewer("[link](https://example.com)", HighContrastMode.Enabled, withTheme: true);
        Assert.Equal(Color.Parse("#FFFF00"), LinkColor(viewer));

        var preset = ThemeInclude("GitHubTheme");
        viewer.Styles.Add(preset);
        // Inlines get their restyled values back at the next measure (AvaloniaUI/Avalonia#22505)
        viewer.UpdateLayout();
        Assert.Equal(Color.Parse("#71B7FF"), LinkColor(viewer));
        Assert.Equal(Color.Parse("#0A0C10"), ((ISolidColorBrush)viewer.Background!).Color);

        viewer.Styles.Remove(preset);
        viewer.UpdateLayout();
        Assert.Equal(Color.Parse("#FFFF00"), LinkColor(viewer));
        Assert.Single(viewer.Resources.MergedDictionaries);
    }

    [AvaloniaTheory]
    [InlineData(false, "0,0,0,0")]
    [InlineData(true, "1,1,1,1")]
    public void Docs_preset_outlines_alerts_in_high_contrast(bool highContrast, string thickness)
    {
        var viewer = new MarkdownViewer
        {
            HighContrastMode = highContrast ? HighContrastMode.Enabled : HighContrastMode.Disabled,
            Markdown = "> [!NOTE]\n> Body",
        };
        viewer.Styles.Add(MarkdownThemeInclude());
        viewer.Styles.Add(ThemeInclude("DocsTheme"));
        new Window { Width = 400, Height = 300, Content = viewer }.Show();

        var alert = viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-alert"));
        Assert.Equal(Thickness.Parse(thickness), alert.BorderThickness);
    }

    [AvaloniaTheory]
    [MemberData(nameof(PresetNames))]
    public void High_contrast_needs_no_style_of_its_own_in_a_preset(string theme)
    {
        // Each :high-contrast style is a style instance and an activator on every control it matches, whether or
        // not high contrast is on; the palette swap costs nothing until it is. Only the default theme keeps two,
        // for the viewer itself and for separators; its control themes match MarkdownViewer.InHighContrast instead.
        var path = Path.Combine(FindRepoRoot(), "src", "MarkView.Avalonia", "Themes", theme + ".axaml");
        var selectors = System.Xml.Linq.XDocument.Load(path).Descendants()
            .Select(e => (string?)e.Attribute("Selector"))
            .Where(s => s?.Contains(":high-contrast", StringComparison.Ordinal) == true)
            .ToList();

        if (theme == "MarkdownTheme")
            Assert.Equal(2, selectors.Count);
        else
            Assert.Empty(selectors);
    }

    public static TheoryData<string> PresetNames() => new(Presets);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }
    [AvaloniaFact]
    public void InHighContrast_is_inherited_by_the_document_while_high_contrast_is_on()
    {
        var viewer = ShowViewer("Text", HighContrastMode.Enabled);
        var paragraph = viewer.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.True(MarkdownViewer.GetInHighContrast(viewer));
        Assert.True(MarkdownViewer.GetInHighContrast(paragraph));

        viewer.HighContrastMode = HighContrastMode.Disabled;

        Assert.False(MarkdownViewer.GetInHighContrast(viewer));
        Assert.False(MarkdownViewer.GetInHighContrast(paragraph));
        Assert.False(viewer.IsSet(MarkdownViewer.InHighContrastProperty));
    }

    [AvaloniaTheory]
    [InlineData("> [!NOTE]\n> Text")]
    [InlineData("> [!NOTE]-\n> Text")]
    public void Alert_titles_take_the_high_contrast_foreground(string markdown)
    {
        using var _ = new ThemeScope(ThemeVariant.Light);
        var viewer = new MarkdownViewer().UseAlertBlocks();
        viewer.Markdown = markdown;
        viewer.Styles.Add(MarkdownThemeInclude());
        new Window { Width = 400, Height = 300, Content = viewer }.Show();
        TextBlock Header() => viewer.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-alert-header"));
        var kindColour = Assert.IsAssignableFrom<ISolidColorBrush>(Header().Foreground).Color;

        viewer.HighContrastMode = HighContrastMode.Enabled;

        Assert.Equal(Colors.Black, Assert.IsAssignableFrom<ISolidColorBrush>(Header().Foreground).Color);
        Assert.NotEqual(Colors.Black, kindColour);
    }
}
