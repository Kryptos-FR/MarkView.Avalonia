// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

/// <summary>How the inline elements of the HTML subset render.</summary>
public class HtmlSubsetInlineTests : RenderTestBase
{
    private static MarkdownPipeline Pipeline => new MarkdownPipelineBuilder().UseHtmlSubset().Build();

    private static List<Inline> Inlines(string markdown) =>
        Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Render(markdown, Pipeline).Children)).Inlines!.ToList();

    private static T Single<T>(string markdown) where T : Inline =>
        Assert.IsType<T>(Assert.Single(Inlines(markdown)), exactMatch: true);

    private static string Text(Span span) => MarkdownSelectableTextBlock.ExtractPlainText(span.Inlines);

    [AvaloniaTheory]
    [InlineData("<b>x</b>")]
    [InlineData("<strong>x</strong>")]
    public void Bold_tags_render_bold(string markdown)
    {
        Assert.Equal("x", Text(Single<Bold>(markdown)));
    }

    [AvaloniaTheory]
    [InlineData("<i>x</i>")]
    [InlineData("<em>x</em>")]
    public void Italic_tags_render_italic(string markdown)
    {
        Assert.Equal("x", Text(Single<Italic>(markdown)));
    }

    [AvaloniaTheory]
    [InlineData("<ins>x</ins>")]
    [InlineData("<u>x</u>")]
    public void Underline_tags_render_underlined(string markdown)
    {
        Assert.Equal("x", Text(Single<Underline>(markdown)));
    }

    [AvaloniaTheory]
    [InlineData("<del>x</del>")]
    [InlineData("<s>x</s>")]
    [InlineData("<strike>x</strike>")]
    public void Strikethrough_tags_render_struck_through(string markdown)
    {
        var span = Single<Span>(markdown);

        Assert.Equal(TextDecorations.Strikethrough, span.TextDecorations);
        Assert.Equal("x", Text(span));
        Assert.Empty(span.Classes);
    }

    [AvaloniaFact]
    public void Mark_renders_like_marked_text()
    {
        var span = Single<Span>("<mark>x</mark>");

        Assert.Equal(["markdown-marked"], span.Classes);
        Assert.Equal("x", Text(span));
    }

    [AvaloniaTheory]
    [InlineData("sub", BaselineAlignment.Subscript, "subs", "markdown-subscript")]
    [InlineData("sup", BaselineAlignment.Superscript, "sups", "markdown-superscript")]
    public void Sub_and_sup_render_like_their_markdown_emphasis(string name, BaselineAlignment baseline, string feature, string className)
    {
        var inlines = Inlines($"x<{name}>2</{name}>");

        var span = Assert.IsType<Span>(inlines[1], exactMatch: true);
        Assert.Equal(baseline, span.BaselineAlignment);
        Assert.Equal(feature, Assert.Single(span.FontFeatures!).Tag);
        Assert.Equal([className], span.Classes);
        Assert.Equal("2", Text(span));
    }

    [AvaloniaFact]
    public void Formatting_elements_nest()
    {
        var bold = Single<Bold>("<b>a<i>b</i></b>");

        Assert.Equal("b", Text(Assert.IsType<Italic>(bold.Inlines[1])));
    }

    [AvaloniaFact]
    public void Code_renders_like_inline_code()
    {
        var run = Single<Run>("<code>a &lt; b</code>");

        Assert.Equal("a < b", run.Text);
        Assert.Contains("markdown-code-inline", run.Classes);
    }

    [AvaloniaFact]
    public void Kbd_renders_as_a_key_that_copies_its_text()
    {
        var inlines = Inlines("Press <kbd>Ctrl</kbd>");

        var key = Assert.IsType<HtmlKeyboardKey>(Assert.IsType<InlineUIContainer>(inlines[1]).Child);
        Assert.Equal(["markdown-html-kbd"], key.Classes);
        var text = Assert.IsType<TextBlock>(key.Child);
        Assert.Equal("Ctrl", MarkdownSelectableTextBlock.ExtractPlainText(text.Inlines!));
        Assert.Equal("Ctrl", MarkdownSelection.GetCopyText(key));
    }

    [AvaloniaFact]
    public void Kbd_keeps_the_formatting_inside_it()
    {
        var key = (HtmlKeyboardKey)Single<InlineUIContainer>("<kbd><b>C</b>trl</kbd>").Child!;

        var text = (TextBlock)key.Child!;
        Assert.IsType<Bold>(text.Inlines![0]);
        Assert.Equal("Ctrl", MarkdownSelection.GetCopyText(key));
    }

    [AvaloniaFact]
    public void Screen_readers_read_a_key_as_its_text_once()
    {
        var key = Render("<kbd><b>C</b>trl</kbd>", Pipeline).GetLogicalDescendants().OfType<HtmlKeyboardKey>().Single();
        var peer = ControlAutomationPeer.CreatePeerForElement(key);

        Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
        Assert.Equal("Ctrl", peer.GetName());
        Assert.False(ControlAutomationPeer.CreatePeerForElement((Control)key.Child!).IsControlElement());
    }

    [AvaloniaFact]
    public void A_key_is_styled_as_a_border()
    {
        var root = Render("<kbd>K</kbd>", Pipeline, themed: true);
        var key = root.GetLogicalDescendants().OfType<HtmlKeyboardKey>().Single();
        var window = new Window { Content = root };
        window.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        window.Show();
        try
        {
            Assert.Equal(new Thickness(1, 1, 1, 2), key.BorderThickness);
            Assert.Equal(new Thickness(4, 0), key.Padding);
            Assert.NotNull(key.Background);
            Assert.Equal(12, ((TextBlock)key.Child!).FontSize);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_text_of_a_key_sits_on_the_baseline_of_the_line()
    {
        var key = new HtmlKeyboardKey
        {
            Margin = new Thickness(1, 2, 0, 0),
            BorderThickness = new Thickness(3),
            Padding = new Thickness(0, 4, 0, 0),
            Child = new TextBlock { Text = "K" },
        };
        var line = new TextBlock { FontSize = 20, Inlines = [new Run("a"), new InlineUIContainer(key)] };
        var window = new Window { Content = line };
        window.Show();
        try
        {
            var text = (TextBlock)key.Child;
            Assert.Equal(2 + 3 + 4 + text.TextLayout.Baseline, TextBlock.GetBaselineOffset(key), 6);
            var keyBaseline = text.TranslatePoint(new Point(0, text.TextLayout.Baseline), line)!.Value.Y;
            // Within layout rounding.
            Assert.Equal(line.TextLayout.TextLines[0].Baseline, keyBaseline, tolerance: 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_key_around_another_control_keeps_the_default_baseline()
    {
        var key = new HtmlKeyboardKey { Child = new Border { Width = 10, Height = 10 } };

        key.Measure(Size.Infinity);

        Assert.Equal(0, TextBlock.GetBaselineOffset(key));
    }

    [AvaloniaFact]
    public void A_link_renders_like_a_markdown_link()
    {
        var link = Single<MarkdownHyperlink>("<a href=\"https://example.com/\" title=\"Example\">x</a>");

        Assert.Equal(new Uri("https://example.com/"), link.NavigateUri);
        Assert.Equal("Example", link.Title);
        Assert.Contains("markdown-link", link.Classes);
        Assert.Equal("x", Text(link));
    }

    [AvaloniaFact]
    public void A_relative_link_is_resolved_against_the_base_uri()
    {
        var link = (MarkdownHyperlink)Assert.Single(RenderInlines("<a href=\"docs/a.md#part\">x</a>", new Uri("https://example.com/repo/")));

        Assert.Equal(new Uri("https://example.com/repo/docs/a.md#part"), link.NavigateUri);
    }

    [AvaloniaFact]
    public void An_image_renders_like_a_markdown_image()
    {
        var container = Assert.IsType<InlineUIContainer>(Assert.Single(RenderInlines("<img src=\"img/logo.png\" alt=\"Logo\">", new Uri("https://example.com/repo/"))));

        var image = Assert.IsType<Image>(container.Child);
        Assert.Equal("https://example.com/repo/img/logo.png", image.Tag);
        Assert.Contains("markdown-image", image.Classes);
        Assert.Equal("Logo", ToolTip.GetTip(image));
        Assert.Equal("Logo", MarkdownSelection.GetCopyText(image));
        Assert.Equal(double.PositiveInfinity, image.MaxWidth);
        Assert.Equal(double.PositiveInfinity, image.MaxHeight);
    }

    [AvaloniaTheory]
    [InlineData("width=\"200\" height=\"100\"", 200.0, 100.0)]
    [InlineData("width=\"200\"", 200.0, double.PositiveInfinity)]
    [InlineData("height=\"100\"", double.PositiveInfinity, 100.0)]
    public void Image_width_and_height_cap_its_size_like_a_size_hint(string attributes, double maxWidth, double maxHeight)
    {
        var image = (Image)Single<InlineUIContainer>($"<img src=\"logo.png\" {attributes}>").Child!;

        Assert.Equal(maxWidth, image.MaxWidth);
        Assert.Equal(maxHeight, image.MaxHeight);
        Assert.Equal(Stretch.Uniform, image.Stretch);
        Assert.Equal(StretchDirection.DownOnly, image.StretchDirection);
    }

    [AvaloniaFact]
    public void Image_size_attributes_win_over_a_size_title()
    {
        var image = (Image)Single<InlineUIContainer>("<img src=\"logo.png\" title=\"=10x10\" width=\"200\">").Child!;

        Assert.Equal(200.0, image.MaxWidth);
        Assert.Equal(double.PositiveInfinity, image.MaxHeight);
    }

    [AvaloniaFact]
    public void An_image_is_loaded_through_the_image_loaders()
    {
        var loader = new RecordingImageLoader();
        var pipeline = Pipeline;
        var document = Markdown.Parse("<p align=\"center\"><img src=\"https://example.com/logo.png\"></p>", pipeline);
        var renderer = new AvaloniaRenderer();
        renderer.ImageLoaders.Insert(0, loader);
        pipeline.Setup(renderer);
        renderer.Render(document);

        var window = new Window { Content = renderer.RootPanel };
        window.Show();
        try
        {
            Assert.Equal(["https://example.com/logo.png"], loader.Urls);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_linked_image_renders_inside_the_link()
    {
        var link = Single<MarkdownHyperlink>("<a href=\"https://example.com/\"><img src=\"badge.svg\" alt=\"build\"></a>");

        var image = Assert.IsType<Image>(Assert.IsType<InlineUIContainer>(Assert.Single(link.Inlines)).Child);
        Assert.Equal("build", MarkdownSelection.GetCopyText(image));
    }

    [AvaloniaFact]
    public void A_line_break_tag_renders_a_line_break()
    {
        var inlines = Inlines("a<br>b");

        Assert.IsType<LineBreak>(inlines[1]);
        Assert.Equal(3, inlines.Count);
    }

    [AvaloniaTheory]
    [InlineData("a<span>b</span>c")]
    [InlineData("a<b>b</i>c")]
    [InlineData("a<iframe src=\"https://example.com\">b</iframe>c")]
    [InlineData("a<b onclick=\"alert(1)\" style=\"color:red\">b</b>c")]
    public void Unknown_and_unmatched_tags_are_dropped_and_their_text_shows(string markdown)
    {
        var text = MarkdownSelectableTextBlock.ExtractPlainText(
            Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Render(markdown, Pipeline).Children)).Inlines!);

        Assert.Equal("abc", text);
    }

    [AvaloniaFact]
    public void Without_the_subset_inline_tags_are_dropped()
    {
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Render("a<b>b</b>c").Children));

        Assert.All(textBlock.Inlines!, inline => Assert.IsType<Run>(inline));
    }

    private static InlineCollection RenderInlines(string markdown, Uri baseUri)
    {
        var pipeline = Pipeline;
        var renderer = new AvaloniaRenderer { BaseUri = baseUri };
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(renderer.RootPanel.Children)).Inlines!;
    }

    private sealed class RecordingImageLoader : IImageLoader
    {
        public List<string> Urls { get; } = [];

        public bool CanLoad(string url)
        {
            Urls.Add(url);
            return false;
        }

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult<IImage?>(null);
    }
}
