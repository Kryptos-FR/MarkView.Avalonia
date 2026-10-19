// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkdownAvalonia.Benchmarks;
using MarkView.Benchmarks.Shared;

return BenchProgram.Run<MarkdownAvaloniaAdapter>(args, typeof(MarkdownAvaloniaBenchmarks));

namespace MarkdownAvalonia.Benchmarks
{
    public class MarkdownAvaloniaBenchmarks : ViewerBenchmarks<MarkdownAvaloniaAdapter>;
}
