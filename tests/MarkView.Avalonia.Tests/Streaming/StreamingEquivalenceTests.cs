// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Platform;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Streaming;

/// <summary>
/// A document streamed in random chunks, each followed by an update, ends up rendered exactly as when
/// the whole text is set to <see cref="MarkdownViewer.Markdown"/> at once.
/// </summary>
public class StreamingEquivalenceTests
{
    public static TheoryData<string, int, bool> Documents()
    {
        var data = new TheoryData<string, int, bool>();
        foreach (var asset in new[] { "chat.md", "readme.md", "theming.md", "references.md", "abbreviations.md", "healing.md", "thinking.md" })
            for (var seed = 1; seed <= 3; seed++)
                data.Add(asset, seed, false);
        data.Add("chat.md", 1, true);
        data.Add("readme.md", 2, true);
        data.Add("references.md", 3, true);
        data.Add("healing.md", 1, true);
        data.Add("thinking.md", 2, true);
        return data;
    }

    private static string Load(string asset, bool crlf)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://MarkView.Avalonia.Tests/TestAssets/Streaming/{asset}"));
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        return crlf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
    }

    // The alert extras of UseAlertBlocks, plus abbreviations, which Markdig applies to every paragraph
    // once their definition, typically at the end, is parsed, and thinking blocks, expanded once complete so
    // that selection compares their content too.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseGitHubExtensions()
        .UseExtendedAlertBlocks()
        .UseAbbreviations()
        .UseThinkingBlocks(new ThinkingBlockOptions { ExpandWhenDone = true })
        .Build();

    private static MarkdownViewer Viewer() =>
        new() { Template = ScrollingViewerTemplate.Create(), Pipeline = Pipeline };

    private static StackPanel Root(MarkdownViewer viewer) =>
        Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);

    private static IEnumerable<(int Level, string Text, string Slug)> Flatten(IEnumerable<TocEntry> entries) =>
        entries.SelectMany(e => Flatten(e.Children).Prepend((e.Level, e.Text, e.Slug)));

    /// <summary>
    /// What each root child renders that its text does not show, in document order: every control's
    /// type and classes, every link's address and title, every image's source and copy text, and
    /// every tooltip.
    /// </summary>
    private static List<string> Structure(MarkdownViewer viewer) =>
        Root(viewer).Children.Select(child =>
        {
            var dump = new StringBuilder();
            DumpControl(child, dump);
            return dump.ToString();
        }).ToList();

    private static void DumpControl(Control control, StringBuilder dump)
    {
        dump.Append('<').Append(control.GetType().Name);
        foreach (var name in control.Classes)
            dump.Append(" .").Append(name);
        if (control is Image image)
            dump.Append(" src=").Append(image.Tag).Append(" copy=").Append(MarkdownSelection.GetCopyText(image));
        if (ToolTip.GetTip(control) is string tip)
            dump.Append(" tip=").Append(tip);
        dump.Append('>');
        if (control is TextBlock { Inlines: { } inlines })
            DumpInlines(inlines, dump);
        else
        {
            foreach (var child in control.GetLogicalChildren().OfType<Control>())
                DumpControl(child, dump);
        }
        dump.Append("</>");
    }

    private static void DumpInlines(InlineCollection inlines, StringBuilder dump)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case MarkdownHyperlink link:
                    dump.Append("<a href=").Append(link.NavigateUri?.OriginalString).Append(" title=").Append(link.Title).Append('>');
                    DumpInlines(link.Inlines, dump);
                    dump.Append("</a>");
                    break;
                case Span span:
                    dump.Append('<').Append(span.GetType().Name);
                    foreach (var name in span.Classes)
                        dump.Append(" .").Append(name);
                    dump.Append('>');
                    DumpInlines(span.Inlines, dump);
                    dump.Append("</>");
                    break;
                case InlineUIContainer { Child: { } child }:
                    DumpControl(child, dump);
                    break;
            }
        }
    }

    private static string AllText(MarkdownViewer viewer)
    {
        viewer.SelectAll();
        return viewer.GetSelectedText();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Documents))]
    public void Streamed_document_renders_like_a_direct_set(string asset, int seed, bool crlf)
    {
        var text = Load(asset, crlf);
        var expected = Viewer();
        expected.Markdown = text;

        var actual = Viewer();
        var session = actual.BeginStreaming();
        var random = new Random(seed);
        var updates = 0;
        for (var position = 0; position < text.Length;)
        {
            var length = Math.Min(random.Next(20, 201), text.Length - position);
            session.Append(text.AsSpan(position, length));
            session.ApplyPendingUpdate();
            position += length;
            updates++;
        }
        session.Complete();

        Assert.True(updates > 1, "the document must take several updates");
        Assert.Equal(text, actual.Markdown);
        Assert.Equal(AllText(expected), AllText(actual));
        Assert.Equal(expected.AnchorIds.Order(StringComparer.Ordinal), actual.AnchorIds.Order(StringComparer.Ordinal));
        Assert.Equal(Flatten(expected.TableOfContents), Flatten(actual.TableOfContents));
        Assert.Equal(Root(expected).Children.Count, Root(actual).Children.Count);
        Assert.Equal(Root(expected).Children.Select(c => c.GetType()), Root(actual).Children.Select(c => c.GetType()));
        Assert.Equal(Structure(expected), Structure(actual));
    }
}
