// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;

using Xunit;

namespace MarkView.Avalonia.Tests.Accessibility;

/// <summary>
/// <see cref="LinkFocusLayer"/> can drop its later keyboard stops and link targets, and collect
/// new root children one at a time, so a streamed document can discard and re-render its open tail.
/// </summary>
public class LinkFocusLayerTruncateTests
{
    // The keyboard stops are: link "a", the task checkbox, link "b".
    private const string Source = "[a](x)\n\n- [ ] task\n\n[b](y)";

    private sealed record Fixture(Window Window, StackPanel Root, LinkFocusLayer Layer, List<string> Activated)
        : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    private static Fixture Show()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().Build();
        var renderer = new AvaloniaRenderer { AreTaskListsInteractive = true };
        pipeline.Setup(renderer);
        renderer.Render(Markdig.Markdown.Parse(Source, pipeline));

        var activated = new List<string>();
        var layer = LinkFocusLayer.Create(renderer.RootPanel, link => activated.Add(link.NavigateUri!.OriginalString));
        var window = new Window
        {
            Width = 400,
            Height = 600,
            Content = new Grid { Children = { renderer.RootPanel, layer } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Fixture(window, renderer.RootPanel, layer, activated);
    }

    private static string? NameOf(object? stop) => (stop as LinkFocusTarget)?.Link.NavigateUri?.OriginalString;

    [AvaloniaFact]
    public void Counts_follow_collection()
    {
        using var f = Show();

        Assert.Equal(3, f.Layer.StopCount);
        Assert.Equal(2, f.Layer.TargetCount);
        Assert.Equal(f.Layer.Children.Count, f.Layer.TargetCount);
    }

    [AvaloniaFact]
    public void TruncateTo_drops_later_stops_and_targets()
    {
        using var f = Show();
        var first = Assert.IsType<LinkFocusTarget>(f.Layer.Children[0]);

        f.Layer.TruncateTo(1, 1);

        Assert.Equal(1, f.Layer.StopCount);
        Assert.Equal(1, f.Layer.TargetCount);
        Assert.Single(f.Layer.Children);
        Assert.Same(first, Assert.Single(f.Layer.GetActiveStops()));
    }

    [AvaloniaFact]
    public void TruncateTo_clears_link_targets_of_discarded_text_blocks()
    {
        using var f = Show();
        var first = Assert.IsType<LinkFocusTarget>(f.Layer.Children[0]);
        var last = Assert.IsType<LinkFocusTarget>(f.Layer.Children[1]);
        Assert.Same(last, Assert.Single(last.TextBlock.LinkTargets!));

        f.Layer.TruncateTo(1, 1);

        Assert.Null(last.TextBlock.LinkTargets);
        Assert.Same(first, Assert.Single(first.TextBlock.LinkTargets!));
    }

    [AvaloniaFact]
    public void TruncateTo_keeps_the_stop_count_independent_of_the_target_count()
    {
        using var f = Show();

        // Keeps link "a" and the checkbox, drops link "b".
        f.Layer.TruncateTo(2, 1);

        Assert.Equal(2, f.Layer.StopCount);
        Assert.Equal(1, f.Layer.TargetCount);
        Assert.Equal(["x", null], f.Layer.GetActiveStops().Select(NameOf));

        // Keeps all three stops but drops the last target: only the target count shrinks.
        using var g = Show();
        g.Layer.TruncateTo(3, 1);
        Assert.Equal(3, g.Layer.StopCount);
        Assert.Equal(1, g.Layer.TargetCount);
    }

    [AvaloniaFact]
    public void TruncateTo_to_the_current_counts_changes_nothing()
    {
        using var f = Show();
        var stops = f.Layer.GetActiveStops();

        f.Layer.TruncateTo(3, 2);

        Assert.Equal(3, f.Layer.StopCount);
        Assert.Equal(2, f.Layer.TargetCount);
        Assert.Equal(stops, f.Layer.GetActiveStops());
        Assert.NotNull(((LinkFocusTarget)f.Layer.Children[1]).TextBlock.LinkTargets);
    }

    [AvaloniaFact]
    public void TruncateTo_zero_empties_the_layer()
    {
        using var f = Show();

        f.Layer.TruncateTo(0, 0);

        Assert.Equal(0, f.Layer.StopCount);
        Assert.Equal(0, f.Layer.TargetCount);
        Assert.Empty(f.Layer.GetActiveStops());
    }

    [AvaloniaTheory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 3)]
    public void TruncateTo_rejects_counts_out_of_range(int stopCount, int targetCount)
    {
        using var f = Show();

        Assert.Throws<ArgumentOutOfRangeException>(() => f.Layer.TruncateTo(stopCount, targetCount));

        Assert.Equal(3, f.Layer.StopCount);
        Assert.Equal(2, f.Layer.TargetCount);
    }

    [AvaloniaFact]
    public void Collect_appends_in_order()
    {
        using var f = Show();
        f.Layer.TruncateTo(1, 1);

        f.Layer.Collect(f.Root.Children[2]);

        Assert.Equal(2, f.Layer.StopCount);
        Assert.Equal(2, f.Layer.TargetCount);
        var added = Assert.IsType<LinkFocusTarget>(f.Layer.Children[1]);
        Assert.Equal("y", added.Link.NavigateUri!.OriginalString);
        Assert.Equal(["x", "y"], f.Layer.GetActiveStops().Select(NameOf));
        Assert.Same(added, Assert.Single(added.TextBlock.LinkTargets!));
    }

    [AvaloniaFact]
    public void Create_still_wires_activation()
    {
        using var f = Show();
        var first = Assert.IsType<LinkFocusTarget>(f.Layer.Children[0]);

        first.Focus();
        f.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

        Assert.Equal(["x"], f.Activated);
    }

    [AvaloniaFact]
    public void Collect_after_truncation_keeps_activation_wired()
    {
        using var f = Show();
        f.Layer.TruncateTo(1, 1);
        f.Layer.Collect(f.Root.Children[2]);
        var added = Assert.IsType<LinkFocusTarget>(f.Layer.Children[1]);

        added.Focus();
        f.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

        Assert.Equal(["y"], f.Activated);
    }
}
