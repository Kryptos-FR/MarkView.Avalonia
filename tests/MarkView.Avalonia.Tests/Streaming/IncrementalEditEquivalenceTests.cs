// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;

using MarkView.Avalonia.Rendering;

using Xunit;

using static MarkView.Avalonia.Tests.Streaming.StreamingEquivalenceTests;

namespace MarkView.Avalonia.Tests.Streaming;

/// <summary>
/// A document edited at random places, each edit rendered incrementally, is rendered after every edit
/// exactly as when the edited text is set to a viewer that renders it from scratch.
/// </summary>
public class IncrementalEditEquivalenceTests
{
    public static TheoryData<string, int> Documents()
    {
        var data = new TheoryData<string, int>();
        foreach (var asset in new[] { "chat.md", "readme.md", "themes.md", "style-classes.md", "references.md", "abbreviations.md", "healing.md" })
            for (var seed = 1; seed <= 3; seed++)
                data.Add(asset, seed);
        return data;
    }

    // Insertions that start, end, split or merge blocks, add headings with existing texts (their
    // slugs then change after them), references, footnotes and tasks.
    private static readonly string[] Snippets =
    [
        "x", " ", "\n", "\n\n", "**", "`", "> ", "- ", "1. ", "# ", "\n\n## Usage\n\n", "\n\n# Introduction\n\n",
        "\n\n- [ ] task\n- [x] done\n\n", "[link](https://example.com/a)", "[ref]", "\n\n[ref]: https://example.com/r\n\n",
        "[^1]", "\n\n[^1]: A footnote.\n\n", "\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n", "\n\n```\ncode\n```\n\n",
        "\n\n> [!NOTE]\n> Note\n\n", "===", "\n---\n", "*[HTML]: Hyper Text\n", "    ", "\n\n    indented code\n\n",
        "\n\n<details>\n<summary>More</summary>\n\nHidden\n\n</details>\n\n", "<sub>2</sub>", "<!-- note -->", "```",
    ];

    private static string Edit(string text, Random random)
    {
        var position = random.Next(text.Length + 1);
        switch (random.Next(4))
        {
            case 0:
                return text.Insert(position, Snippets[random.Next(Snippets.Length)]);
            case 1:
                var length = Math.Min(random.Next(1, 40), text.Length - position);
                return text.Remove(position, length);
            case 2:
                // Duplicates a line, so that a heading's text appears twice.
                var start = text.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;
                var end = text.IndexOf('\n', position);
                var line = text[start..(end < 0 ? text.Length : end)];
                return text.Insert(start, line + "\n\n");
            default:
                return position < text.Length ? text.Remove(position, 1).Insert(position, "y") : text + "y";
        }
    }

    private static MarkdownViewer Viewer(bool incremental) =>
        new()
        {
            Template = ScrollingViewerTemplate.Create(),
            Pipeline = Pipeline,
            AreTaskListsInteractive = true,
            AreHeadingsFoldable = true,
            IsIncrementalRenderingEnabled = incremental,
        };

    private static string TextOf(Control control) => control is TextBlock { Inlines: { } inlines }
        ? MarkdownSelectableTextBlock.ExtractPlainText(inlines)
        : control.GetType().Name;

    /// <summary>Each anchor, with the text of what it scrolls to, which must be in the rendered document.</summary>
    private static List<string> Anchors(MarkdownViewer viewer)
    {
        var root = Root(viewer);
        return viewer.AnchorControls
            .Select(a =>
            {
                Assert.Contains(root, a.Value.GetSelfAndLogicalAncestors());
                return $"{a.Key} → {TextOf(a.Value)}";
            })
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The link layer's keyboard stand-ins, in order, with the address of the link each covers.</summary>
    private static List<string> LinkTargets(MarkdownViewer viewer) =>
        Assert.IsType<LinkFocusLayer>(Assert.IsType<Grid>(viewer.Content).Children[2]).Children
            .Cast<LinkFocusTarget>()
            .Select(t => $"{t.Link.NavigateUri} in {TextOf(t.TextBlock)}")
            .ToList();

    [AvaloniaTheory]
    [MemberData(nameof(Documents))]
    public void Edited_document_renders_like_a_new_one(string asset, int seed)
    {
        var text = Load(asset, crlf: false);
        var actual = Viewer(incremental: true);
        actual.Markdown = text;
        var grid = actual.Content;
        var random = new Random(seed);

        for (var i = 0; i < 60; i++)
        {
            text = Edit(text, random);
            if (text.Length == 0)
                text = "x";
            actual.Markdown = text;
            var expected = Viewer(incremental: false);
            expected.Markdown = text;

            Assert.Same(grid, actual.Content);
            Assert.Equal(AllText(expected), AllText(actual));
            Assert.Equal(Anchors(expected), Anchors(actual));
            Assert.Equal(Flatten(expected.TableOfContents), Flatten(actual.TableOfContents));
            Assert.Equal(Structure(expected), Structure(actual));
            Assert.Equal(LinkTargets(expected), LinkTargets(actual));
            // Copying as Markdown reads each block's source position, which a block kept after the edit moved.
            actual.SelectAll();
            expected.SelectAll();
            Assert.Equal(expected.GetSelectedMarkdown(), actual.GetSelectedMarkdown());
        }
    }
}
