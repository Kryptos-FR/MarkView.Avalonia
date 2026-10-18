// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;

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
        foreach (var asset in new[] { "chat.md", "readme.md", "theming.md" })
            for (var seed = 1; seed <= 3; seed++)
                data.Add(asset, seed, false);
        data.Add("chat.md", 1, true);
        data.Add("readme.md", 2, true);
        return data;
    }

    private static string Load(string asset, bool crlf)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://MarkView.Avalonia.Tests/TestAssets/Streaming/{asset}"));
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        return crlf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
    }

    private static MarkdownViewer Viewer() =>
        new MarkdownViewer { Template = ScrollingViewerTemplate.Create() }.UseAlertBlocks();

    private static StackPanel Root(MarkdownViewer viewer) =>
        Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);

    private static IEnumerable<(int Level, string Text, string Slug)> Flatten(IEnumerable<TocEntry> entries) =>
        entries.SelectMany(e => Flatten(e.Children).Prepend((e.Level, e.Text, e.Slug)));

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
    }
}
