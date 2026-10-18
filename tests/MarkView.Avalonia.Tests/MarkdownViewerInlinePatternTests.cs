// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerInlinePatternTests
{
    private const string Document = "Thanks @bob, fixed in #123.";

    private static readonly Regex Mention = new(@"(?<!\w)@(\w+)");
    private static readonly Regex Issue = new(@"(?<![\w&])#(\d+)\b");

    private static string IssueUrl(Match match) => $"https://github.com/o/r/issues/{match.Groups[1].Value}";

    private static InlineCollection Paragraph(MarkdownViewer viewer) =>
        Assert.IsType<MarkdownSelectableTextBlock>(
            Assert.IsType<Grid>(viewer.Content).Children.OfType<StackPanel>().First().Children[0]).Inlines!;

    private static MarkdownViewer Viewer()
    {
        var viewer = new MarkdownViewer()
            .UseInlinePattern(Mention, m => new Run(m.Value) { Classes = { "mention" } })
            .UseLinkPattern(Issue, IssueUrl);
        viewer.Markdown = Document;
        return viewer;
    }

    [AvaloniaFact]
    public void Use_helpers_render_the_patterns_and_return_the_viewer()
    {
        var viewer = new MarkdownViewer { Markdown = Document };
        Assert.Single(Paragraph(viewer));

        Assert.Same(viewer, viewer.UseInlinePattern(Mention, m => new Run(m.Value) { Classes = { "mention" } }));
        Assert.Same(viewer, viewer.UseLinkPattern(Issue, IssueUrl));

        var inlines = Paragraph(viewer);
        Assert.Contains("mention", Assert.IsType<Run>(inlines[1]).Classes);
        Assert.Equal(new Uri("https://github.com/o/r/issues/123"), Assert.IsType<MarkdownHyperlink>(inlines[3]).NavigateUri);
    }

    [AvaloniaFact]
    public void Use_helpers_replace_the_viewers_earlier_registration_of_the_same_pattern()
    {
        var viewer = Viewer();
        var fence = new FenceRendererExtension("chart", (_, _) => null);
        viewer.Extensions.Insert(0, fence);
        viewer.Extensions.Add(new InlinePatternExtension(new Regex(Issue.ToString(), RegexOptions.IgnoreCase), _ => null));

        viewer.UseInlinePattern(new Regex(Issue.ToString()), m => new Run("issue") { Classes = { "issue" } });
        viewer.UseLinkPattern(new Regex(Mention.ToString()), m => $"https://example.com/{m.Groups[1].Value}");

        // The same pattern text with other options is another pattern.
        Assert.Collection(viewer.Extensions,
            e => Assert.Same(fence, e),
            e => Assert.Equal(RegexOptions.IgnoreCase, Assert.IsType<InlinePatternExtension>(e).Pattern.Options),
            e => Assert.Equal(Issue.ToString(), Assert.IsType<InlinePatternExtension>(e).Pattern.ToString()),
            e => Assert.Equal(Mention.ToString(), Assert.IsType<LinkPatternExtension>(e).Pattern.ToString()));
        Assert.IsType<MarkdownHyperlink>(Paragraph(viewer)[1]);
    }

    [AvaloniaFact]
    public void Add_helpers_on_the_defaults_apply_to_every_viewer()
    {
        using var scope = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.Extensions.AddInlinePattern(Mention, m => new Run(m.Value) { Classes = { "mention" } });
        MarkdownViewerDefaults.Extensions.AddLinkPattern(Issue, IssueUrl);

        var inlines = Paragraph(new MarkdownViewer { Markdown = Document });

        Assert.Contains("mention", Assert.IsType<Run>(inlines[1]).Classes);
        Assert.IsType<MarkdownHyperlink>(inlines[3]);
    }

    [AvaloniaFact]
    public void Viewer_pattern_overrides_a_global_one_matching_at_the_same_place()
    {
        using var scope = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.Extensions.AddLinkPattern(Issue, IssueUrl);

        var viewer = new MarkdownViewer().UseLinkPattern(new Regex(@"#(\d+)"), m => $"https://example.com/{m.Groups[1].Value}");
        viewer.Markdown = Document;

        Assert.Equal(new Uri("https://example.com/123"), Assert.IsType<MarkdownHyperlink>(Paragraph(viewer)[1]).NavigateUri);
    }

    [AvaloniaFact]
    public void Selection_copies_the_text_as_written()
    {
        var viewer = Viewer();

        viewer.SelectAll();

        Assert.Equal(Document, viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Find_finds_the_matched_text()
    {
        var viewer = Viewer();

        Assert.Equal(1, viewer.Find("#123"));
        Assert.Equal(1, viewer.Find("@bob"));
    }

    [AvaloniaFact]
    public void Pattern_link_is_a_Tab_stop_that_raises_LinkClicked()
    {
        var viewer = Viewer();
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var clicked = new List<string>();
            viewer.LinkClicked += (_, e) => { clicked.Add(e.Url); e.Handled = true; };
            viewer.Focus();

            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("#123", Assert.IsType<LinkFocusTarget>(window.FocusManager?.GetFocusedElement()).AccessibleName);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

            Assert.Equal(["https://github.com/o/r/issues/123"], clicked);
        }
        finally
        {
            window.Close();
        }
    }
}
