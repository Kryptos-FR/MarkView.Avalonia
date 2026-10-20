// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia;

namespace MarkView.Benchmarks.Shared;

/// <summary>
/// Warm, in-process timings of what a user does with a shown document, for comparing two builds of the same
/// library on a noisy machine (one process per scenario, the driver alternating builds):
/// <list type="bullet">
/// <item><c>load</c>: an empty, shown viewer gets its document;</item>
/// <item><c>replace</c>: a viewer showing a document gets another of the same size;</item>
/// <item><c>theme</c>: light ↔ dark on a shown document;</item>
/// <item><c>scroll</c>: page down to the end of a freshly shown document (pays for whatever was deferred);</item>
/// <item><c>stream</c>: the document arrives in 20-character chunks, each shown before the next;</item>
/// <item><c>memory</c>: what a shown document keeps alive, managed (live heap) and native.</item>
/// </list>
/// Each prints one tab-separated line: the median, 95th percentile and minimum of the measured operation, the
/// median allocation per operation, and the number of samples.
/// </summary>
public static class Scenarios
{
    public const int ChunkLength = 20;

    public static readonly string[] Names = ["load", "replace", "theme", "scroll", "stream", "memory"];

    public static int Run(IViewerAdapter adapter, string scenario, string configuration, string size, int iterations)
    {
        BenchHost.EnsureStarted(adapter.AddStyles);
        var window = BenchHost.CreateWindow();
        var document = Corpus.Get(size, seed: 1);
        var other = Corpus.Get(size, seed: 2);
        var samples = new List<(double Ms, long Bytes)>();
        var extra = "";

        Control Show(string? markdown)
        {
            var viewer = adapter.CreateViewer(configuration);
            window.Content = viewer;
            BenchHost.Settle();
            if (markdown is not null)
            {
                adapter.SetMarkdown(viewer, markdown);
                BenchHost.Settle(() => adapter.IsSettled(viewer));
            }
            return viewer;
        }

        void Clear()
        {
            window.Content = null;
            BenchHost.Settle();
            Collect();
        }

        switch (scenario)
        {
            case "load":
            case "replace":
                for (var i = -Warmups(iterations); i < iterations; i++)
                {
                    var viewer = Show(scenario == "replace" ? other : null);
                    var sample = Measure(() =>
                    {
                        adapter.SetMarkdown(viewer, document);
                        BenchHost.Settle(() => adapter.IsSettled(viewer));
                    });
                    if (i >= 0)
                        samples.Add(sample);
                    Clear();
                }
                break;

            case "theme":
            {
                var app = Application.Current!;
                app.RequestedThemeVariant = ThemeVariant.Light;
                Show(document);
                Collect();
                for (var i = -Warmups(iterations); i < iterations; i++)
                {
                    var variant = app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
                    var sample = Measure(() =>
                    {
                        app.RequestedThemeVariant = variant;
                        BenchHost.Settle();
                    });
                    if (i >= 0)
                        samples.Add(sample);
                }
                Clear();
                break;
            }

            case "scroll":
                for (var i = -1; i < iterations; i++)
                {
                    var viewer = Show(document);
                    var scrollViewer = viewer.GetVisualDescendants().OfType<ScrollViewer>().First();
                    var pages = 0;
                    var sample = Measure(() =>
                    {
                        while (scrollViewer.Offset.Y + scrollViewer.Viewport.Height < scrollViewer.Extent.Height - 1)
                        {
                            scrollViewer.Offset = scrollViewer.Offset.WithY(scrollViewer.Offset.Y + scrollViewer.Viewport.Height);
                            BenchHost.Settle();
                            pages++;
                        }
                    });
                    if (i >= 0)
                        samples.Add(sample);
                    extra = $"pages={pages}";
                    Clear();
                }
                break;

            case "stream":
            {
                var chunks = Enumerable.Range(0, (document.Length + ChunkLength - 1) / ChunkLength)
                    .Select(c => document.Substring(c * ChunkLength, Math.Min(ChunkLength, document.Length - c * ChunkLength)))
                    .ToArray();
                var totals = new List<double>();
                for (var i = -1; i < iterations; i++)
                {
                    var viewer = Show(null);
                    var writer = adapter.BeginStreaming(viewer);
                    var updates = new List<(double, long)>();
                    var total = Stopwatch.StartNew();
                    foreach (var chunk in chunks)
                    {
                        updates.Add(Measure(() =>
                        {
                            writer.Append(chunk);
                            BenchHost.Settle(() => writer.IsSettled);
                        }));
                    }
                    writer.Complete();
                    BenchHost.Settle(() => adapter.IsSettled(viewer));
                    total.Stop();
                    if (i >= 0)
                    {
                        samples.AddRange(updates);
                        totals.Add(total.Elapsed.TotalMilliseconds);
                    }
                    Clear();
                }
                totals.Sort();
                extra = $"updates={chunks.Length} total_ms={F(totals[totals.Count / 2])}";
                break;
            }

            case "memory":
            {
                Show(other); // warms every cache with a document of the same shape first
                var viewer = Show(null);
                var managedBefore = Collect();
                var heapBefore = Heap();
                var nativeBefore = PrivateBytes();
                adapter.SetMarkdown(viewer, document);
                BenchHost.Settle(() => adapter.IsSettled(viewer));
                var managed = Collect() - managedBefore;
                var heap = Heap() - heapBefore;
                var native = PrivateBytes() - nativeBefore;
                var visuals = viewer.GetVisualDescendants().Count();
                GC.KeepAlive(viewer);
                extra = $"heap_kb={F(heap / 1024.0)} managed_kb={F(managed / 1024.0)} private_kb={F(native / 1024.0)} visuals={visuals}";
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        window.Close();
        var times = samples.Select(s => s.Ms).Order().ToArray();
        var bytes = samples.Select(s => s.Bytes).Order().ToArray();
        Console.WriteLine(string.Join('\t',
            adapter.Library, scenario, configuration, size, samples.Count.ToString(CultureInfo.InvariantCulture),
            times.Length > 0 ? F(Percentile(times, 0.5)) : "-",
            times.Length > 0 ? F(Percentile(times, 0.95)) : "-",
            times.Length > 0 ? F(times[0]) : "-",
            bytes.Length > 0 ? F(bytes[bytes.Length / 2] / 1024.0) : "-",
            extra));
        return 0;
    }

    private static int Warmups(int iterations) => Math.Max(3, iterations / 3);

    private static (double Ms, long Bytes) Measure(Action action)
    {
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var watch = Stopwatch.StartNew();
        action();
        watch.Stop();
        return (watch.Elapsed.TotalMilliseconds, GC.GetTotalAllocatedBytes(precise: true) - allocated);
    }

    private static double Percentile(double[] sorted, double p) => sorted[Math.Min(sorted.Length - 1, (int)(p * sorted.Length))];

    private static long Collect()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    /// <summary>
    /// The live managed heap as of the last collection: each generation's size less its fragmentation. Unlike
    /// <see cref="GC.GetTotalMemory(bool)"/>, whose difference can be off by megabytes, this agrees with gcdump totals.
    /// Call it right after <see cref="Collect"/>.
    /// </summary>
    private static long Heap()
    {
        var info = GC.GetGCMemoryInfo(GCKind.Any);
        long total = 0;
        foreach (var generation in info.GenerationInfo)
            total += generation.SizeAfterBytes - generation.FragmentationAfterBytes;
        return total;
    }

    private static long PrivateBytes()
    {
        using var process = Process.GetCurrentProcess();
        return process.PrivateMemorySize64;
    }

    private static string F(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
