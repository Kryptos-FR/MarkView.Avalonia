# Benchmarks (proof of concept)

Measures how long a markdown viewer takes to show a document, and how much memory it uses, for MarkView.Avalonia and other Avalonia markdown viewers, on the same generated documents.

This folder is not part of `MarkView.Avalonia.slnx` and CI does not build it.

## Layout

| Path | What it is |
|---|---|
| `Shared/` | Compiled into every runner: the corpus, the headless host, the benchmarks and the memory probe |
| `MarkView.Benchmarks/` | MarkView, through project references to `src/` |
| `Competitors/LiveMarkdown.Benchmarks/` | LiveMarkdown.Avalonia from NuGet |
| `Competitors/MarkdownAvalonia.Benchmarks/` | Markdown.Avalonia from NuGet |

Each library runs in its own executable, with the dependencies it ships with. Avalonia, SkiaSharp, HarfBuzz, the font and the window size are the same for all (`Directory.Build.props`, `Directory.Packages.props`). A runner's only library-specific code is its `IViewerAdapter`.

## Running

Build in Release, then run a runner's executable (`bin/Release/net10.0/<Runner>`):

```bash
dotnet build benchmarks/MarkView.Benchmarks -c Release

# Write the four documents and print their fingerprints
MarkView.Benchmarks corpus out/corpus

# First document in a fresh process, retained memory, visual count: 3 processes per case
MarkView.Benchmarks probe 3 GitHub,GitHub+All medium,long

# Save the rendered frame of one case
MarkView.Benchmarks shot GitHub+All medium out/github-all-medium.png

# Steady-state Load and Replace timings with BenchmarkDotNet (all arguments go to BenchmarkDotNet)
BENCH_CONFIGS=GitHub BENCH_SIZES=medium,long MarkView.Benchmarks --filter '*' --job short
```

Configurations and sizes are comma-separated lists (`all` for every one). The configurations of each runner are listed in its adapter. The sizes are `tiny` (0.3 KB), `medium` (5 KB), `long` (55 KB) and `large` (540 KB).

Numbers from a shared machine (a cloud VM, a CI runner) are only good for spotting large regressions.
