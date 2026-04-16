// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerDefaultsTests
{
    // ── Pipeline resolution ───────────────────────────────────────────────────

    [AvaloniaFact]
    public void No_globals_uses_built_in_default_pipeline()
    {
        using var _ = new MarkdownViewerDefaultsScope();

        MarkdownViewerDefaults.Pipeline = null;

        var viewer = new MarkdownViewer { Markdown = "- [ ] item" };

        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var contentGrid = Assert.IsType<Grid>(scrollViewer.Content);
        var rootPanel = contentGrid.Children.OfType<StackPanel>().First();

        // Built-in default uses UseSupportedExtensions which includes UseTaskLists.
        // A task list item renders a TextBlock with class "markdown-task-list" when the extension is active.
        Assert.NotNull(FindTaskMarker(rootPanel));
    }

    [AvaloniaFact]
    public void Global_pipeline_used_when_instance_pipeline_is_null()
    {
        using var _ = new MarkdownViewerDefaultsScope();

        // A pipeline without UseTaskLists
        MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder().Build();

        var viewer = new MarkdownViewer { Markdown = "- [ ] item" };

        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var contentGrid = Assert.IsType<Grid>(scrollViewer.Content);
        var rootPanel = contentGrid.Children.OfType<StackPanel>().First();

        // Without UseTaskLists, no task marker is rendered — task list item renders as plain text.
        Assert.Null(FindTaskMarker(rootPanel));
    }

    [AvaloniaFact]
    public void Instance_pipeline_wins_over_global_pipeline()
    {
        using var _ = new MarkdownViewerDefaultsScope();

        // Global has task lists; instance does not
        MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
        var instancePipeline = new MarkdownPipelineBuilder().Build(); // no task lists

        var viewer = new MarkdownViewer
        {
            Pipeline = instancePipeline,
            Markdown = "- [ ] item"
        };

        var scrollViewer = Assert.IsType<ScrollViewer>(viewer.Content);
        var contentGrid = Assert.IsType<Grid>(scrollViewer.Content);
        var rootPanel = contentGrid.Children.OfType<StackPanel>().First();

        // Instance pipeline (no task lists) wins over global — no task marker rendered.
        Assert.Null(FindTaskMarker(rootPanel));
    }

    // ── Extension composition ─────────────────────────────────────────────────

    [AvaloniaFact]
    public void Global_extension_is_registered_once()
    {
        using var _ = new MarkdownViewerDefaultsScope();

        var tracker = new TrackingExtension();
        MarkdownViewerDefaults.Extensions.Add(tracker);

        var viewer = new MarkdownViewer { Markdown = "hello" };

        Assert.Equal(1, tracker.RegisterCallCount);
    }

    [AvaloniaFact]
    public void Instance_extension_is_registered_once()
    {
        using var _ = new MarkdownViewerDefaultsScope();

        var tracker = new TrackingExtension();

        var viewer = new MarkdownViewer();
        viewer.Extensions.Add(tracker);
        viewer.Markdown = "# hello";

        Assert.Equal(1, tracker.RegisterCallCount);
    }

    [AvaloniaFact]
    public void Same_extension_object_in_both_lists_is_registered_exactly_once()
    {
        using var _ = new MarkdownViewerDefaultsScope();

        var tracker = new TrackingExtension();
        MarkdownViewerDefaults.Extensions.Add(tracker);

        var viewer = new MarkdownViewer();
        viewer.Extensions.Add(tracker); // same object reference
        viewer.Markdown = "hello";

        Assert.Equal(1, tracker.RegisterCallCount);
    }

    [AvaloniaFact]
    public void Global_and_instance_extensions_both_run_global_first()
    {
        using var _ = new MarkdownViewerDefaultsScope();

        var order = new List<string>();
        var globalExt = new OrderTrackingExtension("global", order);
        var instanceExt = new OrderTrackingExtension("instance", order);

        MarkdownViewerDefaults.Extensions.Add(globalExt);

        var viewer = new MarkdownViewer();
        viewer.Extensions.Add(instanceExt);
        viewer.Markdown = "hello";

        Assert.Equal(new[] { "global", "instance" }, order);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class TrackingExtension : IMarkViewExtension
    {
        public int RegisterCallCount { get; private set; }
        public void Register(AvaloniaRenderer renderer) => RegisterCallCount++;
    }

    private sealed class OrderTrackingExtension(string name, List<string> log) : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) => log.Add(name);
    }

    private static TextBlock? FindTaskMarker(Control root)
    {
        if (root is TextBlock tb && tb.Classes.Contains("markdown-task-list"))
            return tb;
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                var found = FindTaskMarker(child);
                if (found != null) return found;
            }
        }
        if (root is ContentControl cc && cc.Content is Control content)
            return FindTaskMarker(content);
        if (root is Decorator dec && dec.Child is Control decChild)
            return FindTaskMarker(decChild);
        return null;
    }
}
