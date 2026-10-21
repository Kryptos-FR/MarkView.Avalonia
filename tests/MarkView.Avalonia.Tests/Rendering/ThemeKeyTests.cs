// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Markdig;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// Which control theme each renderer gives its controls. Every key resolves to an empty theme of its own,
/// so the tests need no theme file (which mutation testing can't load).
/// </summary>
public class ThemeKeyTests
{
    /// <summary>An empty theme per resource key, and the key of the theme a control was given.</summary>
    internal sealed class TaggedThemes
    {
        private readonly Dictionary<string, ControlTheme> _themes = new(StringComparer.Ordinal);
        private readonly Dictionary<ControlTheme, string> _keys = new(ReferenceEqualityComparer.Instance);

        public ThemeLookup Lookup => new(Find);

        public ControlTheme Find(string key)
        {
            if (!_themes.TryGetValue(key, out var theme))
            {
                theme = new ControlTheme(typeof(StyledElement));
                _themes[key] = theme;
                _keys[theme] = key;
            }
            return theme;
        }

        public string? KeyOf(StyledElement element) =>
            element.Theme is { } theme && _keys.TryGetValue(theme, out var key) ? key : null;

        /// <summary>Defines the themes of <paramref name="keys"/> in <paramref name="resources"/>, for a viewer.</summary>
        public void AddTo(IResourceDictionary resources, params string[] keys)
        {
            foreach (var key in keys)
                resources[key] = Find(key);
        }
    }

    private static (StackPanel Root, TaggedThemes Themes) Render(string markdown, MarkdownPipeline? pipeline = null,
        Action<AvaloniaRenderer>? configure = null)
    {
        pipeline ??= new MarkdownPipelineBuilder().UseGitHubExtensions().Build();
        var themes = new TaggedThemes();
        var renderer = new AvaloniaRenderer { Themes = themes.Lookup };
        configure?.Invoke(renderer);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return (renderer.RootPanel, themes);
    }

    private static IEnumerable<T> All<T>(ILogical root, string className) where T : StyledElement =>
        root.GetLogicalDescendants().OfType<T>().Where(e => e.Classes.Contains(className));

    private static MarkdownSelectableTextBlock Paragraph(ILogical root, string text) =>
        root.GetLogicalDescendants().OfType<MarkdownSelectableTextBlock>()
            .Single(t => t.Classes.Contains("markdown-paragraph") && MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!) == text);

    [AvaloniaFact]
    public void Headings_take_the_theme_of_their_level()
    {
        var (root, themes) = Render("# 1\n## 2\n### 3\n#### 4\n##### 5\n###### 6");

        var headings = root.Children.OfType<TextBlock>().Select(themes.KeyOf);

        Assert.Equal(Enumerable.Range(1, 6).Select(level => $"MarkdownHeading{level}Theme"), headings);
    }

    [AvaloniaFact]
    public void A_paragraph_takes_the_theme_of_its_alert_table_quote_or_list_in_that_order()
    {
        var (root, themes) = Render("""
            Top

            - In list

              > In quote in list

            > In quote
            >
            > - In list in quote

            | Head |
            |------|
            | In table |

            > [!NOTE]
            > In alert
            >
            > - In list in alert
            >
            > > In quote in alert
            """);

        Assert.Equal("MarkdownParagraphTheme", themes.KeyOf(Paragraph(root, "Top")));
        Assert.Equal("MarkdownListParagraphTheme", themes.KeyOf(Paragraph(root, "In list")));
        Assert.Equal("MarkdownQuoteParagraphTheme", themes.KeyOf(Paragraph(root, "In quote in list")));
        Assert.Equal("MarkdownQuoteParagraphTheme", themes.KeyOf(Paragraph(root, "In quote")));
        Assert.Equal("MarkdownQuoteParagraphTheme", themes.KeyOf(Paragraph(root, "In list in quote")));
        Assert.Equal("MarkdownTableParagraphTheme", themes.KeyOf(Paragraph(root, "In table")));
        Assert.Equal("MarkdownAlertParagraphTheme", themes.KeyOf(Paragraph(root, "In alert")));
        Assert.Equal("MarkdownAlertParagraphTheme", themes.KeyOf(Paragraph(root, "In list in alert")));
        Assert.Equal("MarkdownAlertParagraphTheme", themes.KeyOf(Paragraph(root, "In quote in alert")));
    }

    [AvaloniaFact]
    public void A_table_in_a_quote_is_a_table_and_a_list_in_a_table_cell_cannot_exist()
    {
        var (root, themes) = Render("""
            > | Head |
            > |------|
            > | In table in quote |
            """);

        Assert.Equal("MarkdownTableParagraphTheme", themes.KeyOf(Paragraph(root, "In table in quote")));
    }

    [AvaloniaFact]
    public void The_custom_title_of_an_alert_keeps_the_alert_paragraph_theme()
    {
        var pipeline = new MarkdownPipelineBuilder().UseExtendedAlertBlocks().Build();
        var (root, themes) = Render("> [!TIP] Title\n> Body", pipeline);

        Assert.Equal("MarkdownAlertParagraphTheme", themes.KeyOf(Paragraph(root, "Body")));
    }

    [AvaloniaFact]
    public void Inline_code_after_the_custom_title_of_an_alert_is_in_a_paragraph()
    {
        var pipeline = new MarkdownPipelineBuilder().UseExtendedAlertBlocks().Build();
        var (root, themes) = Render("> [!TIP] Title\n> Body `code`", pipeline);

        var code = root.GetLogicalDescendants().OfType<TextBlock>().SelectMany(t => t.Inlines!).OfType<Run>()
            .Single(r => r.Classes.Contains("markdown-code-inline"));
        Assert.Equal("MarkdownParagraphCodeInlineTheme", themes.KeyOf(code));
    }

    [AvaloniaFact]
    public void Inline_code_takes_the_paragraph_theme_only_in_a_paragraph()
    {
        var (root, themes) = Render("# Heading `a`\n\nText `b` and more `c`\n\n# After `d`");

        var runs = root.GetLogicalDescendants().OfType<TextBlock>().SelectMany(t => t.Inlines!)
            .OfType<Run>().Where(r => r.Classes.Contains("markdown-code-inline")).ToList();

        Assert.Equal(["a", "b", "c", "d"], runs.Select(r => r.Text));
        Assert.Equal(
            ["MarkdownCodeInlineTheme", "MarkdownParagraphCodeInlineTheme", "MarkdownParagraphCodeInlineTheme", "MarkdownCodeInlineTheme"],
            runs.Select(themes.KeyOf));
    }

    [AvaloniaTheory]
    [InlineData("NOTE", "Note")]
    [InlineData("TIP", "Tip")]
    [InlineData("WARNING", "Warning")]
    [InlineData("IMPORTANT", "Important")]
    [InlineData("CAUTION", "Caution")]
    [InlineData("TODO", "")]
    public void Alerts_take_the_themes_of_their_kind(string kind, string theme)
    {
        var pipeline = new MarkdownPipelineBuilder().UseExtendedAlertBlocks().Build();
        var (root, themes) = Render($"> [!{kind}]\n> Body", pipeline);

        Assert.Equal($"MarkdownAlert{theme}Theme", themes.KeyOf(All<Border>(root, "markdown-alert").Single()));
        Assert.Equal($"MarkdownAlert{theme}HeaderTheme", themes.KeyOf(All<TextBlock>(root, "markdown-alert-header").Single()));
        Assert.Equal("MarkdownAlertContentTheme", themes.KeyOf(All<StackPanel>(root, "markdown-alert-content").Single()));
    }

    [AvaloniaFact]
    public void Quotes_lists_and_tasks_take_their_themes()
    {
        var (root, themes) = Render("> Quote\n\n- [x] Task\n  - Nested");

        Assert.Equal("MarkdownBlockquoteTheme", themes.KeyOf(All<Border>(root, "markdown-blockquote").Single()));
        var lists = All<StackPanel>(root, "markdown-list").ToList();
        Assert.Equal(["MarkdownListTheme", "MarkdownNestedListTheme"], lists.Select(themes.KeyOf));
        Assert.Equal("MarkdownTaskMarkerTheme", themes.KeyOf(All<StyledElement>(root, "markdown-task-list").Single()));
    }

    [AvaloniaFact]
    public void Table_cells_take_the_theme_of_their_position()
    {
        var (root, themes) = Render("""
            | A | B |
            |---|---|
            | C | D |
            """);

        Assert.Equal("MarkdownTableTheme", themes.KeyOf(All<Grid>(root, "markdown-table").Single()));
        var cells = All<Border>(root, "markdown-table-cell")
            .ToDictionary(c => MarkdownSelectableTextBlock.ExtractPlainText(c.GetLogicalDescendants().OfType<TextBlock>().First().Inlines!), themes.KeyOf);
        Assert.Equal("MarkdownTableCornerCellTheme", cells["A"]);
        Assert.Equal("MarkdownTableFirstRowCellTheme", cells["B"]);
        Assert.Equal("MarkdownTableFirstColumnCellTheme", cells["C"]);
        Assert.Equal("MarkdownTableCellTheme", cells["D"]);
        var headerContent = All<Border>(root, "markdown-table-header").Select(c => (StyledElement)c.Child!).Select(themes.KeyOf);
        Assert.All(headerContent, key => Assert.Equal("MarkdownTableHeaderContentTheme", key));
        Assert.Equal(2, headerContent.Count());
        Assert.All(All<Border>(root, "markdown-table-cell").Where(c => !c.Classes.Contains("markdown-table-header")),
            c => Assert.NotEqual("MarkdownTableHeaderContentTheme", themes.KeyOf((StyledElement)c.Child!)));
    }

    [AvaloniaFact]
    public void Footnotes_and_details_take_their_themes()
    {
        var (root, themes) = Render("Text[^1]\n\n<details><summary>More</summary>\n\nHidden\n\n</details>\n\n[^1]: Note");

        Assert.Equal("MarkdownFootnoteGroupTheme", themes.KeyOf(All<StackPanel>(root, "markdown-footnote-group").Single()));
        Assert.Equal("MarkdownFootnoteItemTheme", themes.KeyOf(All<Grid>(root, "markdown-footnote-item").Single()));
        Assert.Equal("MarkdownDetailsTheme", themes.KeyOf(All<Border>(root, "markdown-details").Single()));
        Assert.Equal("MarkdownDetailsSummaryTheme", themes.KeyOf(All<TextBlock>(root, "markdown-details-summary").Single()));
    }

    [AvaloniaFact]
    public void Html_elements_take_the_themes_of_their_markdown_counterparts()
    {
        var (root, themes) = Render("Press <kbd>Ctrl</kbd> on <mark>this</mark>");

        var key = All<Border>(root, "markdown-html-kbd").Single();
        Assert.Equal("MarkdownKbdTheme", themes.KeyOf(key));
        Assert.Equal("MarkdownKbdTextTheme", themes.KeyOf((StyledElement)key.Child!));
        var marked = root.GetLogicalDescendants().OfType<TextBlock>().SelectMany(t => t.Inlines!).OfType<Span>()
            .Single(s => s.Classes.Contains("markdown-marked"));
        Assert.Equal("MarkdownMarkedTheme", themes.KeyOf(marked));
    }

    [AvaloniaFact]
    public async Task An_image_that_fails_to_load_shows_a_themed_placeholder()
    {
        var (root, themes) = Render("![A diagram](unknown://diagram.png)");
        var window = new Window { Content = root };
        window.Show();
        var slot = Assert.IsType<InlineUIContainer>(Assert.IsAssignableFrom<TextBlock>(root.Children[0]).Inlines![0]);

        await AsyncTestHelpers.WaitUntilAsync(() => slot.Child is Border);

        Assert.Equal("MarkdownImageFailedTheme", themes.KeyOf(slot.Child!));
        window.Close();
    }

    [AvaloniaFact]
    public void Advanced_extensions_take_their_themes()
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var (root, themes) = Render("""
            ==Marked== and ""cited"" HTML

            *[HTML]: Hyper Text

            ^^^
            ![](image.png)
            ^^^ Caption

            Term
            :   Definition

            ::: note
            Contained
            :::

            ^^ Footer
            """, pipeline);

        var spans = root.GetLogicalDescendants().OfType<TextBlock>().SelectMany(t => t.Inlines!).OfType<Span>().ToList();
        Assert.Equal("MarkdownMarkedTheme", themes.KeyOf(spans.Single(s => s.Classes.Contains("markdown-marked"))));
        Assert.Equal("MarkdownCitationTheme", themes.KeyOf(spans.Single(s => s.Classes.Contains("markdown-citation"))));
        Assert.Equal("MarkdownAbbreviationTheme", themes.KeyOf(All<TextBlock>(root, "markdown-abbr").Single()));
        Assert.Equal("MarkdownFigureTheme", themes.KeyOf(All<Border>(root, "markdown-figure").Single()));
        Assert.Equal("MarkdownFigureCaptionTheme", themes.KeyOf(All<TextBlock>(root, "markdown-figure-caption").Single()));
        Assert.Equal("MarkdownDefinitionTermTheme", themes.KeyOf(All<TextBlock>(root, "markdown-definition-term").Single()));
        Assert.Equal("MarkdownDefinitionTheme", themes.KeyOf(All<StyledElement>(root, "markdown-definition").Single()));
        Assert.Equal("MarkdownCustomContainerTheme", themes.KeyOf(All<Border>(root, "markdown-custom-container").Single()));
        Assert.Equal("MarkdownFooterTheme", themes.KeyOf(All<StyledElement>(root, "markdown-footer").Single()));
    }

    [AvaloniaFact]
    public void A_titled_custom_container_gives_its_title_a_theme()
    {
        var pipeline = new MarkdownPipelineBuilder().UseCustomContainers().Build();
        var (root, themes) = Render("::: warning Watch out\nBody\n:::", pipeline);

        Assert.Equal("MarkdownCustomContainerTitleTheme", themes.KeyOf(All<TextBlock>(root, "markdown-custom-container-title").Single()));
    }

    [AvaloniaFact]
    public void Thinking_blocks_take_their_themes()
    {
        var pipeline = new MarkdownPipelineBuilder().UseThinkingBlocks().Build();
        var (root, themes) = Render("<think>\nReasoning\n</think>", pipeline);

        Assert.Equal("MarkdownThinkingTheme", themes.KeyOf(All<Border>(root, "markdown-thinking").Single()));
        Assert.Equal("MarkdownThinkingHeaderTheme", themes.KeyOf(All<TextBlock>(root, "markdown-thinking-header").Single()));
        Assert.Equal("MarkdownThinkingContentTheme", themes.KeyOf(All<StyledElement>(root, "markdown-thinking-content").Single()));
    }

    [AvaloniaFact]
    public void A_youtube_link_takes_its_overlay_themes()
    {
        var pipeline = new MarkdownPipelineBuilder().UseMediaLinks().Build();
        var (root, themes) = Render("![clip](https://www.youtube.com/watch?v=dQw4w9WgXcQ)", pipeline);

        Assert.Equal("MarkdownYoutubeOverlayTheme", themes.KeyOf(All<Grid>(root, "markdown-youtube-overlay").Single()));
        Assert.Equal("MarkdownYoutubePlayTheme", themes.KeyOf(All<TextBlock>(root, "markdown-youtube-play").Single()));
    }

    [AvaloniaTheory]
    [InlineData(false, false, false, "MarkdownCodeBlockTheme")]
    [InlineData(true, false, false, "MarkdownToolbarCodeBlockTheme")]
    [InlineData(false, true, false, "MarkdownToolbarCodeBlockTheme")]
    [InlineData(false, false, true, "MarkdownCodeBlockTheme")]
    public void A_code_block_takes_the_toolbar_theme_only_with_a_toolbar(bool language, bool copy, bool lineNumbers, string blockTheme)
    {
        var viewer = new MarkdownViewer
        {
            ShowCodeBlockLanguage = language,
            ShowCodeBlockCopyButton = copy,
            ShowCodeBlockLineNumbers = lineNumbers,
        };
        var themes = new TaggedThemes();
        themes.AddTo(viewer.Resources, "MarkdownCodeBlockTheme", "MarkdownToolbarCodeBlockTheme", "MarkdownCodeTheme",
            "MarkdownCodeLineNumbersTheme", "MarkdownCodeLanguageTheme", "MarkdownCodeHeaderTheme", "MarkdownCodeContainerTheme");
        viewer.Markdown = "```cs\nvar x = 1;\n```";

        var block = All<Border>(viewer, "markdown-code-block").Single();
        Assert.Equal(blockTheme, themes.KeyOf(block));
        Assert.Equal("MarkdownCodeTheme", themes.KeyOf(block.GetLogicalDescendants().OfType<TextBlock>()
            .Single(t => !t.Classes.Contains("markdown-code-line-numbers"))));
        Assert.Equal(lineNumbers ? "MarkdownCodeLineNumbersTheme" : null,
            All<TextBlock>(viewer, "markdown-code-line-numbers").Select(themes.KeyOf).SingleOrDefault());
        Assert.Equal(language ? "MarkdownCodeLanguageTheme" : null,
            All<TextBlock>(viewer, "markdown-code-language").Select(themes.KeyOf).SingleOrDefault());
        var toolbar = language || copy;
        Assert.Equal(toolbar ? "MarkdownCodeHeaderTheme" : null,
            All<Border>(viewer, "markdown-code-header").Select(themes.KeyOf).SingleOrDefault());
        Assert.Equal(toolbar ? "MarkdownCodeContainerTheme" : null,
            All<StackPanel>(viewer, "markdown-code-container").Select(themes.KeyOf).SingleOrDefault());
    }

    [AvaloniaFact]
    public void The_document_and_a_fence_renderer_control_take_their_themes_from_the_viewer()
    {
        var viewer = new MarkdownViewer().UseFenceRenderer("chart", (code, _) => new TextBlock { Text = code });
        var themes = new TaggedThemes();
        themes.AddTo(viewer.Resources, "MarkdownDocumentTheme", "MarkdownCustomFenceTheme");
        viewer.Markdown = "```chart\ndata\n```";

        Assert.Equal("MarkdownDocumentTheme", themes.KeyOf(Assert.IsType<Grid>(viewer.Content)));
        Assert.Equal("MarkdownCustomFenceTheme", themes.KeyOf(All<Border>(viewer, "markdown-custom-fence").Single()));
    }

    [AvaloniaFact]
    public void A_viewer_swaps_in_a_theme_defined_or_removed_while_it_shows_the_document()
    {
        var viewer = new MarkdownViewer { Markdown = "Text" };
        var window = new Window { Content = viewer };
        window.Show();
        var paragraph = viewer.GetLogicalDescendants().OfType<TextBlock>().Single();
        var themes = new TaggedThemes();
        var preset = new Styles();
        themes.AddTo(preset.Resources, "MarkdownParagraphTheme");

        viewer.Styles.Add(preset);
        Assert.Equal("MarkdownParagraphTheme", themes.KeyOf(paragraph));

        viewer.Styles.Remove(preset);
        Assert.Null(themes.KeyOf(paragraph));
        window.Close();
    }

    [AvaloniaFact]
    public void A_viewer_looks_its_themes_up_again_when_attached_to_a_tree_that_defines_them()
    {
        var viewer = new MarkdownViewer { Markdown = "Text" };
        var paragraph = viewer.GetLogicalDescendants().OfType<TextBlock>().Single();
        var themes = new TaggedThemes();
        var window = new Window();
        themes.AddTo(window.Resources, "MarkdownParagraphTheme");
        Assert.Null(themes.KeyOf(paragraph));

        window.Content = viewer;
        window.Show();

        Assert.Equal("MarkdownParagraphTheme", themes.KeyOf(paragraph));
        window.Close();
    }
}
