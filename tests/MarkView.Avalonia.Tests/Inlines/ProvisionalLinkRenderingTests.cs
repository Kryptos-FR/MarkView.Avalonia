// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class ProvisionalLinkRenderingTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    [AvaloniaFact]
    public void Provisional_link_keeps_its_text_and_class_but_has_no_address()
    {
        // As the healer hands it to the parser: "[text](https://example.com/partial" closed with ")".
        var link = FirstInline<MarkdownHyperlink>(Render("[text](https://example.com/partial)"));
        Assert.Null(link.NavigateUri);
        Assert.Contains("markdown-link", link.Classes);
        Assert.Equal("text", Text(link));
    }

    [AvaloniaFact]
    public void Provisional_link_keeps_its_title()
    {
        var link = FirstInline<MarkdownHyperlink>(Render("[text](u \"the title\")"));
        Assert.Null(link.NavigateUri);
        Assert.Equal("the title", link.Title);
    }

    [AvaloniaFact]
    public void Provisional_image_renders_its_alt_text_as_plain_text()
    {
        var textBlock = FirstTextBlock(Render("before ![a diagram](https://example.com/partial) after"));
        Assert.DoesNotContain(textBlock.Inlines!, i => i is InlineUIContainer);
        Assert.Equal("before a diagram after", TextOf(textBlock));
    }

    [AvaloniaFact]
    public void Provisional_image_without_alt_text_renders_nothing()
    {
        var textBlock = FirstTextBlock(Render("before ![](u) after"));
        Assert.Equal("before  after", TextOf(textBlock));
        Assert.DoesNotContain(textBlock.Inlines!, i => i is Run { Text: "" });
    }

    [AvaloniaFact]
    public void Provisional_youtube_image_renders_its_alt_text()
    {
        var textBlock = FirstTextBlock(Render("![clip](https://www.youtube.com/watch?v=dQw4w9WgXcQ)"));
        Assert.DoesNotContain(textBlock.Inlines!, i => i is InlineUIContainer);
        Assert.Equal("clip", TextOf(textBlock));
    }

    [AvaloniaFact]
    public void Link_and_image_in_a_block_before_the_tail_render_as_usual()
    {
        var renderer = new AvaloniaRenderer { IsStreaming = true };
        var document = Markdown.Parse("[t](https://example.com/) ![a](https://example.com/i.png)\n\ntail", Pipeline);
        renderer.ProvisionalStart = document[1].Span.Start;
        renderer.Render(document);
        var textBlock = Assert.IsAssignableFrom<TextBlock>(renderer.RootPanel.Children[0]);
        Assert.Equal(new Uri("https://example.com/"), textBlock.Inlines!.OfType<MarkdownHyperlink>().Single().NavigateUri);
        Assert.Single(textBlock.Inlines!.OfType<InlineUIContainer>());
    }

    [AvaloniaFact]
    public void Nothing_is_provisional_when_not_streaming()
    {
        var renderer = new AvaloniaRenderer { ProvisionalStart = 0 };
        renderer.Render(Markdown.Parse("[t](https://example.com/)", Pipeline));
        Assert.Equal(new Uri("https://example.com/"), FirstInline<MarkdownHyperlink>(renderer.RootPanel).NavigateUri);
    }

    /// <summary>Renders <paramref name="text"/> with every block provisional and returns the root panel.</summary>
    private static StackPanel Render(string text)
    {
        var renderer = new AvaloniaRenderer { IsStreaming = true, ProvisionalStart = 0 };
        renderer.Render(Markdown.Parse(text, Pipeline));
        return renderer.RootPanel;
    }

    private static TextBlock FirstTextBlock(Panel panel) => Assert.IsAssignableFrom<TextBlock>(panel.Children[0]);

    /// <summary>The first inline of type <typeparamref name="T"/> in the first text block, searched recursively.</summary>
    private static T FirstInline<T>(Panel panel) where T : Inline
        => Flatten(FirstTextBlock(panel).Inlines!).OfType<T>().First();

    private static IEnumerable<Inline> Flatten(IEnumerable<Inline> inlines)
    {
        foreach (var inline in inlines)
        {
            yield return inline;
            if (inline is Span span)
            {
                foreach (var child in Flatten(span.Inlines))
                    yield return child;
            }
        }
    }

    /// <summary>The concatenated texts of the runs of <paramref name="textBlock"/>, searched recursively.</summary>
    private static string TextOf(TextBlock textBlock) => Concat(textBlock.Inlines!);

    /// <summary>The concatenated texts of the runs of <paramref name="span"/>, searched recursively.</summary>
    private static string Text(Span span) => Concat(span.Inlines);

    private static string Concat(IEnumerable<Inline> inlines)
    {
        var sb = new StringBuilder();
        foreach (var run in Flatten(inlines).OfType<Run>())
            sb.Append(run.Text);
        return sb.ToString();
    }
}
