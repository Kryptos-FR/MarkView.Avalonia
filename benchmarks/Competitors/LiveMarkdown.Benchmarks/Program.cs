// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using LiveMarkdown.Benchmarks;
using MarkView.Benchmarks.Shared;

if (args.FirstOrDefault() == "pipeline")
    return LiveMarkdownAdapter.PrintDefaultPipeline();
return BenchProgram.Run<LiveMarkdownAdapter>(args, typeof(LiveMarkdownBenchmarks));

namespace LiveMarkdown.Benchmarks
{
    public class LiveMarkdownBenchmarks : ViewerBenchmarks<LiveMarkdownAdapter>;
}
