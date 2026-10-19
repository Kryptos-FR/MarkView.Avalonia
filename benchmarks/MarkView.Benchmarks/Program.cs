// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Benchmarks.Shared;
using MarkView.Benchmarks;

return BenchProgram.Run<MarkViewAdapter>(args, typeof(MarkViewBenchmarks));

namespace MarkView.Benchmarks
{
    public class MarkViewBenchmarks : ViewerBenchmarks<MarkViewAdapter>;
}
