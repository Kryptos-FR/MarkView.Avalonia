// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using BenchmarkDotNet.Attributes;

namespace MarkView.Benchmarks.Shared;

/// <summary>
/// Steady-state timings, run by BenchmarkDotNet (each case in its own process, after warm-up, so the JIT is
/// done). Every operation ends when the document is laid out and drawn: two rendered frames after the viewer
/// reports it is settled. Creating the viewer and removing it afterwards are outside the measured operation,
/// so each invocation needs its own iteration setup: one invocation per iteration (a second invocation would
/// set the same text again, which a viewer may rightly skip).
/// Set BENCH_CONFIGS and BENCH_SIZES (comma-separated) to run a subset of the cases.
/// </summary>
[MemoryDiagnoser]
[InvocationCount(1, unrollFactor: 1)]
public abstract class ViewerBenchmarks<TAdapter> where TAdapter : IViewerAdapter, new()
{
    private readonly TAdapter _adapter = new();
    private Window _window = null!;
    private Control _viewer = null!;
    private string _document = "";
    private string _previous = "";

    [ParamsSource(nameof(Configurations))]
    public string Configuration { get; set; } = "";

    [ParamsSource(nameof(Sizes))]
    public string Size { get; set; } = "";

    public IEnumerable<string> Configurations => Subset("BENCH_CONFIGS", _adapter.Configurations);

    public static IEnumerable<string> Sizes => Subset("BENCH_SIZES", Corpus.Sizes);

    [GlobalSetup]
    public void GlobalSetup()
    {
        BenchHost.EnsureStarted(_adapter.AddStyles);
        _window = BenchHost.CreateWindow();
        _document = Corpus.Get(Size, seed: 1);
        _previous = Corpus.Get(Size, seed: 2);
    }

    [GlobalCleanup]
    public void GlobalCleanup() => _window.Close();

    [IterationSetup(Target = nameof(Load))]
    public void SetupLoad() => Attach(null);

    [IterationSetup(Target = nameof(Replace))]
    public void SetupReplace() => Attach(_previous);

    [IterationCleanup]
    public void Detach()
    {
        _window.Content = null;
        BenchHost.Settle();
    }

    /// <summary>An empty viewer, already shown, gets its first document (a chat bubble, a newly opened file).</summary>
    [Benchmark]
    public void Load() => Show(_document);

    /// <summary>A viewer showing a document of the same size gets another one (switching files, regenerating an answer).</summary>
    [Benchmark]
    public void Replace() => Show(_document);

    private static IEnumerable<string> Subset(string variable, IEnumerable<string> all)
        => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } list ? list.Split(',') : all;

    private void Attach(string? initial)
    {
        _viewer = _adapter.CreateViewer(Configuration);
        _window.Content = _viewer;
        BenchHost.Settle();
        if (initial is not null)
        {
            _adapter.SetMarkdown(_viewer, initial);
            BenchHost.Settle(() => _adapter.IsSettled(_viewer));
        }
    }

    private void Show(string markdown)
    {
        _adapter.SetMarkdown(_viewer, markdown);
        BenchHost.Settle(() => _adapter.IsSettled(_viewer));
    }
}
