// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerTableOfContentsTests
{
    [AvaloniaFact]
    public void TableOfContents_reflects_heading_structure()
    {
        var viewer = new MarkdownViewer { Markdown = "# Title\n\n## Sub" };

        var root = Assert.Single(viewer.TableOfContents);
        Assert.Equal("Title", root.Text);
        Assert.Equal("title", root.Slug);
        var child = Assert.Single(root.Children);
        Assert.Equal("Sub", child.Text);
    }

    [AvaloniaFact]
    public void TableOfContents_empty_for_document_without_headings()
    {
        var viewer = new MarkdownViewer { Markdown = "Just a paragraph" };
        Assert.Empty(viewer.TableOfContents);
    }

    [AvaloniaFact]
    public void Null_Markdown_clears_TableOfContents()
    {
        var viewer = new MarkdownViewer { Markdown = "# Title" };
        viewer.Markdown = null;
        Assert.Empty(viewer.TableOfContents);
    }

    [AvaloniaFact]
    public void TableOfContentsMaxDepth_defaults_to_six()
    {
        var viewer = new MarkdownViewer();
        Assert.Equal(6, viewer.TableOfContentsMaxDepth);
    }

    [AvaloniaFact]
    public void TableOfContentsMaxDepth_excludes_deeper_headings()
    {
        var viewer = new MarkdownViewer
        {
            Markdown = "# H1\n\n## H2",
            TableOfContentsMaxDepth = 1,
        };
        var root = Assert.Single(viewer.TableOfContents);
        Assert.Empty(root.Children);
    }

    [AvaloniaFact]
    public void Changing_TableOfContentsMaxDepth_updates_TableOfContents_without_full_rerender()
    {
        var viewer = new MarkdownViewer { Markdown = "# H1\n\n## H2" };
        viewer.TableOfContentsMaxDepth = 1;

        var root = Assert.Single(viewer.TableOfContents);
        Assert.Empty(root.Children);
    }

    [AvaloniaFact]
    public void TableOfContents_slug_uses_github_style_format()
    {
        var viewer = new MarkdownViewer { Markdown = "# My Heading" };
        var entry = Assert.Single(viewer.TableOfContents);
        Assert.Equal("my-heading", entry.Slug);
    }

    [AvaloniaFact]
    public void Duplicate_heading_text_produces_unique_slugs_matching_anchors()
    {
        var viewer = new MarkdownViewer { Markdown = "# Hello\n\n# Hello" };

        Assert.Collection(viewer.TableOfContents,
            first => Assert.Equal("hello", first.Slug),
            second => Assert.Equal("hello-1", second.Slug));
    }
}
