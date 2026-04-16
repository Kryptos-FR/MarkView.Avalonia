// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

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

        var viewer = new MarkdownViewer { Markdown = "hello" };

        // No exception = built-in default pipeline was used
        Assert.NotNull(viewer.Content);
    }

    [AvaloniaFact]
    public void Global_pipeline_used_when_instance_pipeline_is_null()
    {
        using var _ = new MarkdownViewerDefaultsScope();

        var globalPipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
        MarkdownViewerDefaults.Pipeline = globalPipeline;

        var viewer = new MarkdownViewer { Markdown = "hello" };

        // Pipeline property is null (not set on instance) — global should have been used.
        // We can only verify indirectly: render should succeed without error.
        Assert.Null(viewer.Pipeline);
        Assert.NotNull(viewer.Content);
    }

    [AvaloniaFact]
    public void Instance_pipeline_wins_over_global_pipeline()
    {
        using var _ = new MarkdownViewerDefaultsScope();

        var globalPipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();
        var instancePipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().UseFootnotes().Build();
        MarkdownViewerDefaults.Pipeline = globalPipeline;

        var viewer = new MarkdownViewer
        {
            Pipeline = instancePipeline,
            Markdown = "hello"
        };

        Assert.Same(instancePipeline, viewer.Pipeline);
        Assert.NotNull(viewer.Content);
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

        var viewer = new MarkdownViewer { Markdown = "# hi" };
        viewer.Extensions.Add(tracker);
        viewer.Markdown = "# hello"; // trigger re-render

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
}
