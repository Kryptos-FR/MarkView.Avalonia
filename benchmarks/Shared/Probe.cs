// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;

namespace MarkView.Benchmarks.Shared;

/// <summary>
/// What BenchmarkDotNet does not measure: the first document of a fresh process (JIT, type loading, style and
/// grammar caches: what a user waits for when an application opens its first markdown view), and the memory a
/// shown document keeps alive. Each (configuration, size) pair runs in its own child process so that no case
/// inherits another's warm caches.
/// </summary>
public static class Probe
{
    public static int RunAll(IViewerAdapter adapter, IEnumerable<string> configurations, IEnumerable<string> sizes, int repeats)
    {
        Console.WriteLine($"# {adapter.Library}, {Environment.ProcessorCount} logical CPUs, .NET {Environment.Version}, {(GCSettings.IsServerGC ? "server" : "workstation")} GC");
        Console.WriteLine("library\tconfiguration\tsize\tdocument\trun\tfirst_ms\tsecond_ms\tretained_kb\tfirst_alloc_kb\tvisuals");
        foreach (var configuration in configurations)
        {
            foreach (var size in sizes)
            {
                for (var run = 1; run <= repeats; run++)
                {
                    var psi = new ProcessStartInfo(Environment.ProcessPath!)
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                    };
                    foreach (var arg in Environment.GetCommandLineArgs().Skip(1).TakeWhile(a => a != "probe"))
                        psi.ArgumentList.Add(arg);
                    psi.ArgumentList.Add("probe-one");
                    psi.ArgumentList.Add(configuration);
                    psi.ArgumentList.Add(size);
                    using var child = Process.Start(psi)!;
                    var line = child.StandardOutput.ReadToEnd().Trim();
                    child.WaitForExit();
                    Console.WriteLine(child.ExitCode == 0
                        ? line.Replace("{run}", run.ToString(CultureInfo.InvariantCulture))
                        : $"{adapter.Library}\t{configuration}\t{size}\t-\t{run}\tFAILED (exit {child.ExitCode}) {line}");
                }
            }
        }
        return 0;
    }

    public static int RunOne(IViewerAdapter adapter, string configuration, string size)
    {
        var document = Corpus.Get(size, seed: 1);
        var second = Corpus.Get(size, seed: 2);
        BenchHost.EnsureStarted(adapter.AddStyles);
        var window = BenchHost.CreateWindow();

        // Cold: the first document this process shows (JIT, type loading, style, font and grammar caches).
        var viewer = adapter.CreateViewer(configuration);
        window.Content = viewer;
        BenchHost.Settle();
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var first = Stopwatch.StartNew();
        adapter.SetMarkdown(viewer, document);
        BenchHost.Settle(() => adapter.IsSettled(viewer));
        first.Stop();
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        window.Content = null;
        BenchHost.Settle();

        // Warm: the same work with a new viewer and another document of the same shape. The memory it keeps
        // alive is measured against the empty viewer, so it excludes the caches the cold run filled.
        var viewer2 = adapter.CreateViewer(configuration);
        window.Content = viewer2;
        BenchHost.Settle();
        var before = Collect();
        var secondTime = Stopwatch.StartNew();
        adapter.SetMarkdown(viewer2, second);
        BenchHost.Settle(() => adapter.IsSettled(viewer2));
        secondTime.Stop();
        var retained = Collect() - before;
        var visuals = viewer2.GetVisualDescendants().Count();
        // BENCH_HOLD=seconds keeps the document shown so that a heap snapshot can be taken
        // (dotnet-gcdump collect -p <pid>); the process id goes to standard error.
        if (int.TryParse(Environment.GetEnvironmentVariable("BENCH_HOLD"), out var hold))
        {
            Console.Error.WriteLine($"pid {Environment.ProcessId}: holding for {hold} s");
            Thread.Sleep(TimeSpan.FromSeconds(hold));
        }
        GC.KeepAlive(viewer2);

        Console.WriteLine(string.Join('\t',
            adapter.Library, configuration, size, Corpus.Fingerprint(document), "{run}",
            F(first.Elapsed.TotalMilliseconds), F(secondTime.Elapsed.TotalMilliseconds),
            F(retained / 1024.0), F(allocated / 1024.0), visuals.ToString(CultureInfo.InvariantCulture)));
        window.Close();
        return 0;
    }

    public static int Screenshot(IViewerAdapter adapter, string configuration, string size, string path)
    {
        BenchHost.EnsureStarted(adapter.AddStyles);
        var window = BenchHost.CreateWindow();
        var viewer = adapter.CreateViewer(configuration);
        window.Content = viewer;
        BenchHost.Settle();
        adapter.SetMarkdown(viewer, Corpus.Get(size, seed: 1));
        BenchHost.Settle(() => adapter.IsSettled(viewer), minFrames: 10);
        window.CaptureRenderedFrame()!.Save(path);
        Console.WriteLine(path);
        return 0;
    }

    private static long Collect()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    private static string F(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
