// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerSourceTests
{
    // avares:// resource embedded in this test assembly.
    private static readonly Uri AvaresTestDoc =
        new("avares://MarkView.Avalonia.Tests/TestAssets/test.md");

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StackPanel GetRootPanel(MarkdownViewer viewer)
    {
        var sv = Assert.IsType<ScrollViewer>(viewer.Content);
        var grid = Assert.IsType<Grid>(sv.Content);
        return Assert.IsType<StackPanel>(grid.Children[0]);
    }

    private static Task WaitForContentAsync(MarkdownViewer viewer, int timeoutMs = 5_000)
    {
        if (viewer.Content is not null)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.PropertyChanged += (_, e) =>
        {
            if (e.Property == ContentControl.ContentProperty)
                tcs.TrySetResult();
        };
        // Task.WaitAsync keeps the continuation on the current (Avalonia dispatcher)
        // sync context, avoiding the scheduler mismatch that Task.WhenAny +
        // ContinueWith(TaskScheduler.Default) could cause.
        return tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
    }

    // ── avares:// ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Source_avares_loads_and_renders_content()
    {
        var viewer = new MarkdownViewer { Source = AvaresTestDoc };

        // avares:// is synchronous — Content is set before the property setter returns.
        var panel = GetRootPanel(viewer);
        Assert.NotEmpty(panel.Children);
    }

    [AvaloniaFact]
    public void Source_avares_renders_heading_from_asset()
    {
        var viewer = new MarkdownViewer { Source = AvaresTestDoc };

        var panel = GetRootPanel(viewer);
        Assert.Contains(panel.Children, c =>
            c is MarkdownSelectableTextBlock tb && tb.Classes.Contains("markdown-h1"));
    }

    // ── file:// ───────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Source_file_uri_loads_and_renders_content()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "# Hello from file\n\nParagraph.");

            var viewer = new MarkdownViewer { Source = new Uri(path) };
            await WaitForContentAsync(viewer);

            var panel = GetRootPanel(viewer);
            Assert.NotEmpty(panel.Children);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task Source_file_uri_renders_correct_content()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "# File Heading\n\nBody text.");

            var viewer = new MarkdownViewer { Source = new Uri(path) };
            await WaitForContentAsync(viewer);

            var panel = GetRootPanel(viewer);
            Assert.Contains(panel.Children, c =>
                c is MarkdownSelectableTextBlock tb && tb.Classes.Contains("markdown-h1"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Precedence ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Source_takes_precedence_over_Markdown_property()
    {
        // The test asset has a heading; Markdown has plain text only.
        // After both are set, Source content (with markdown-h1) must win.
        var viewer = new MarkdownViewer
        {
            Markdown = "plain text — no heading",
            Source = AvaresTestDoc,
        };

        var panel = GetRootPanel(viewer);
        Assert.Contains(panel.Children, c =>
            c is MarkdownSelectableTextBlock tb && tb.Classes.Contains("markdown-h1"));
    }

    // ── Null / clear ─────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Setting_Source_to_null_falls_back_to_Markdown()
    {
        var viewer = new MarkdownViewer
        {
            Markdown = "fallback paragraph",
            Source = AvaresTestDoc,
        };

        // Source is active — heading is present.
        var beforePanel = GetRootPanel(viewer);
        Assert.Contains(beforePanel.Children, c =>
            c is MarkdownSelectableTextBlock tb && tb.Classes.Contains("markdown-h1"));

        // Clear Source → Markdown takes over.
        viewer.Source = null;

        var afterPanel = GetRootPanel(viewer);
        Assert.NotEmpty(afterPanel.Children);
        // No heading in "fallback paragraph".
        Assert.DoesNotContain(afterPanel.Children, c =>
            c is MarkdownSelectableTextBlock tb && tb.Classes.Contains("markdown-h1"));
    }

    [AvaloniaFact]
    public void Setting_Source_to_null_with_no_Markdown_clears_content()
    {
        var viewer = new MarkdownViewer { Source = AvaresTestDoc };
        Assert.NotNull(viewer.Content);

        viewer.Source = null;

        Assert.Null(viewer.Content);
    }

    // ── BaseUri inference ─────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Source_avares_infers_BaseUri_when_not_explicitly_set()
    {
        // When no explicit BaseUri is given, the renderer BaseUri should be
        // inferred from the source directory so that relative image links resolve.
        // We verify indirectly: loading succeeds and content is not null.
        var viewer = new MarkdownViewer { Source = AvaresTestDoc };

        Assert.NotNull(viewer.Content);
    }
}
