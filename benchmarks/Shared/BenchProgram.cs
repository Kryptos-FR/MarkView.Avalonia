// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using BenchmarkDotNet.Running;

namespace MarkView.Benchmarks.Shared;

/// <summary>
/// The command line every runner shares:
/// <list type="bullet">
/// <item><c>corpus [dir]</c> writes the four documents (and prints their fingerprints);</item>
/// <item><c>probe [repeats] [configurations] [sizes]</c> measures first-document time and retained memory, one process per case;</item>
/// <item><c>scenario name configuration size [iterations]</c> times one warm scenario (see <see cref="Scenarios"/>);</item>
/// <item><c>configurations</c> lists the runner's configurations;</item>
/// <item><c>shot configuration size [file.png]</c> saves the first rendered frame, to check what a library actually drew;</item>
/// <item>anything else goes to BenchmarkDotNet (e.g. <c>--filter * --job short</c>).</item>
/// </list>
/// Configurations and sizes are comma-separated lists; "all" or nothing means all of them.
/// </summary>
public static class BenchProgram
{
    public static int Run<TAdapter>(string[] args, Type benchmarks) where TAdapter : IViewerAdapter, new()
    {
        var adapter = new TAdapter();
        switch (args.FirstOrDefault())
        {
            case "corpus":
            {
                var dir = args.Length > 1 ? args[1] : "corpus";
                Directory.CreateDirectory(dir);
                foreach (var size in Corpus.Sizes)
                {
                    var text = Corpus.Get(size);
                    File.WriteAllText(Path.Combine(dir, size + ".md"), text);
                    Console.WriteLine($"{size}\t{text.Length} chars\t{text.Split('\n').Length} lines\t{Corpus.Fingerprint(text)}");
                }
                return 0;
            }
            case "probe":
            {
                var repeats = args.Length > 1 ? int.Parse(args[1]) : 3;
                return Probe.RunAll(adapter, List(args, 2, adapter.Configurations), List(args, 3, Corpus.Sizes), repeats);
            }
            case "probe-one":
                return Probe.RunOne(adapter, args[1], args[2]);
            case "scenario":
                return Scenarios.Run(adapter, args[1], args[2], args[3], args.Length > 4 ? int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 10);
            case "configurations":
                Console.WriteLine(string.Join(',', adapter.Configurations));
                return 0;
            case "shot":
                return Probe.Screenshot(adapter, args[1], args[2], args.Length > 3 ? args[3] : $"{args[1]}-{args[2]}.png");
            default:
                BenchmarkSwitcher.FromTypes([benchmarks]).Run(args);
                return 0;
        }
    }

    private static IEnumerable<string> List(string[] args, int index, IEnumerable<string> all)
        => args.Length > index && args[index] != "all" ? args[index].Split(',') : all;
}
