// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

/// <summary>
/// Adding styles to a parent (switching a theme preset, for instance) clears the styled values of every
/// inline below it, and Avalonia only applies them again to controls. Inline code and links must get
/// their style back.
/// </summary>
public class InlineRestyleTests
{
    [AvaloniaTheory]
    [InlineData("A `code` span")]
    [InlineData("A **bold `code`** span")]
    [InlineData("| Feature | Notes |\n|---|---|\n| sizing | `=WxH` |")]
    [InlineData("> [!TIP] Use `flag`\n> Body")]
    public void Inline_code_keeps_its_style_after_styles_are_added(string markdown)
    {
        var (window, viewer) = Show(markdown);
        var code = Inlines(viewer).OfType<Run>().Single(r => r.Classes.Contains("markdown-code-inline"));
        Assert.Equal(Brushes.Red, code.Background);

        AddStyles(window);

        Assert.Equal(Brushes.Red, code.Background);
        window.Close();
    }

    [AvaloniaFact]
    public void Link_keeps_its_style_after_styles_are_added()
    {
        var (window, viewer) = Show("A [link](https://example.com).");
        var link = Inlines(viewer).OfType<MarkdownHyperlink>().Single();
        Assert.Equal(Brushes.Green, link.Foreground);

        AddStyles(window);

        Assert.Equal(Brushes.Green, link.Foreground);
        window.Close();
    }

    [AvaloniaFact]
    public void Alert_header_is_still_styled_as_a_TextBlock()
    {
        var (window, viewer) = Show("> [!TIP] Title\n> Body");
        var header = viewer.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-alert-header"));

        Assert.Equal(Brushes.Blue, header.Foreground);
        window.Close();
    }

    private static (Window Window, MarkdownViewer Viewer) Show(string markdown)
    {
        var viewer = new MarkdownViewer().UseAlertBlocks();
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Styles.Add(new Style(x => x.OfType<Run>().Class("markdown-code-inline"))
        {
            Setters = { new Setter(TextElement.BackgroundProperty, Brushes.Red) },
        });
        window.Styles.Add(new Style(x => x.OfType<MarkdownHyperlink>().Class("markdown-link"))
        {
            Setters = { new Setter(TextElement.ForegroundProperty, Brushes.Green) },
        });
        window.Styles.Add(new Style(x => x.OfType<TextBlock>().Class("markdown-alert-header"))
        {
            Setters = { new Setter(TextBlock.ForegroundProperty, Brushes.Blue) },
        });
        window.Show();
        return (window, viewer);
    }

    // Any styles with a setter make Avalonia clear the styled values below the window.
    private static void AddStyles(Window window)
    {
        window.Styles.Add(new Styles
        {
            new Style(x => x.OfType<Border>().Class("unused")) { Setters = { new Setter(Border.OpacityProperty, 0.5) } },
        });
        window.UpdateLayout();
    }

    private static IEnumerable<Inline> Inlines(MarkdownViewer viewer)
    {
        static IEnumerable<Inline> Flatten(IEnumerable<Inline> inlines) =>
            inlines.SelectMany(i => i is Span s ? Flatten(s.Inlines).Prepend(i) : [i]);
        return viewer.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Inlines is not null)
            .SelectMany(t => Flatten(t.Inlines!));
    }
}
