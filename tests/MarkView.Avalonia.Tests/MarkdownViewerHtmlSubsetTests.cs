// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// The HTML subset inside a full <see cref="MarkdownViewer"/> with its default pipeline: selection and copy,
/// links, and the table of contents.
/// </summary>
public class MarkdownViewerHtmlSubsetTests
{
    [AvaloniaFact]
    public void Default_pipeline_renders_the_html_subset()
    {
        using var _ = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.Pipeline = null;

        var viewer = new MarkdownViewer { Markdown = "Press <kbd>Ctrl</kbd>" };

        Assert.Single(viewer.GetLogicalDescendants().OfType<HtmlKeyboardKey>());
    }

    [AvaloniaFact]
    public void Selection_copies_the_text_of_the_new_elements()
    {
        var viewer = new MarkdownViewer
        {
            Markdown = """
                <p align="center"><img src="logo.png" alt="Logo"><br><b>Fast</b> viewer</p>

                Press <kbd>Ctrl</kbd>+<kbd>C</kbd> to <ins>copy</ins> H<sub>2</sub>O, x<sup>2</sup>, <code>a &lt; b</code> and <mark>this</mark>.
                """,
        };

        viewer.SelectAll();

        Assert.Equal("Logo\nFast viewer\nPress Ctrl+C to copy H2O, x2, a < b and this.", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Selection_copies_the_markdown_inside_a_centered_div()
    {
        var viewer = new MarkdownViewer { Markdown = "<div align=\"center\">\n\n# Title\n\nCentered\n\n</div>\n\nAfter" };

        viewer.SelectAll();

        Assert.Equal("Title\nCentered\nAfter", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Html_headings_are_listed_in_the_table_of_contents()
    {
        var viewer = new MarkdownViewer { Markdown = "<h2 align=\"center\">Project</h2>\n\n## Usage" };

        Assert.Equal(["Project", "Usage"], viewer.TableOfContents.Select(e => e.Text));
    }

    [AvaloniaFact]
    public void Clicking_an_html_link_raises_LinkClicked_like_a_markdown_link()
    {
        var viewer = new MarkdownViewer { Markdown = "Intro\n\n<a href=\"https://example.com/\">link</a> tail" };
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        // Headless text has no hit-testable glyphs: a transparent background stands in for them.
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        window.Show();
        try
        {
            var urls = new List<string>();
            viewer.LinkClicked += (_, e) => { urls.Add(e.Url); e.Handled = true; };
            var block = viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().ElementAt(1);
            var caret = block.TextLayout.HitTestTextPosition(1);
            var point = block.TranslatePoint(new Point(caret.X + 0.5, caret.Y + caret.Height / 2), window)!.Value;

            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);

            Assert.Equal(["https://example.com/"], urls);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_pipeline_without_the_subset_drops_html_blocks()
    {
        var viewer = new MarkdownViewer
        {
            Pipeline = new MarkdownPipelineBuilder().UseHtmlDetails().Build(),
            Markdown = "<p align=\"center\">Hidden</p>\n\nShown",
        };

        viewer.SelectAll();

        Assert.Equal("Shown", viewer.GetSelectedText());
    }
}
