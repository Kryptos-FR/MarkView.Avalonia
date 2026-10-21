// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Security.Cryptography;
using System.Text;

namespace MarkView.Benchmarks.Shared;

/// <summary>
/// The documents every runner renders. They are generated, not read from files, so that every library gets
/// byte-identical input without committing half a megabyte of text, and so that the "large" document is not
/// the same section repeated (which would flatter any cache keyed on block text). The output depends only on
/// the size and the seed: <see cref="Random(int)"/> with a seed uses the same algorithm on every .NET version.
/// Each runner prints <see cref="Fingerprint"/> so a results table can show that all libraries rendered the
/// same bytes.
/// </summary>
public static class Corpus
{
    public const string Tiny = "tiny";
    public const string Medium = "medium";
    public const string Long = "long";
    public const string Large = "large";

    public static readonly string[] Sizes = [Tiny, Medium, Long, Large];

    /// <summary>The document for <paramref name="size"/>; <paramref name="seed"/> gives a different document of the same shape.</summary>
    public static string Get(string size, int seed = 1) => size switch
    {
        Tiny => ChatReply(new Random(seed)),
        // One section is about 2.7 KB, so these are about 5, 50 and 500 KB of distinct sections.
        Medium => Sections(new Random(seed), 2),
        Long => Sections(new Random(seed), 20),
        Large => Sections(new Random(seed), 200),
        _ when size.StartsWith("file:") => File.ReadAllText(size[5..]),
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, null),
    };

    public static string Fingerprint(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();

    private static readonly string[] Words =
    [
        "render", "control", "layout", "theme", "document", "block", "inline", "parser", "viewer", "style",
        "selection", "pipeline", "extension", "highlight", "measure", "arrange", "the", "a", "of", "and", "to",
        "in", "is", "for", "with", "that", "on", "it", "as", "by", "this", "each", "when", "every", "text",
        "line", "table", "image", "diagram", "formula", "list", "item", "heading", "quote", "link", "code",
        "markdown", "avalonia", "window", "brush", "font", "glyph", "run", "span", "panel", "grid", "border",
    ];

    // A short assistant reply: what a chat bubble shows most of the time.
    private static string ChatReply(Random rng)
    {
        var sb = new StringBuilder();
        sb.Append("Sure! Call `").Append(Word(rng)).Append("()` before you **").Append(Word(rng))
          .Append("** the ").Append(Word(rng)).Append(", then check the [docs](https://example.com/").Append(Word(rng)).AppendLine("):");
        sb.AppendLine();
        sb.AppendLine("```csharp");
        sb.Append("var viewer = new MarkdownViewer { Markdown = \"").Append(Sentence(rng, 4)).AppendLine("\" };");
        sb.AppendLine("panel.Children.Add(viewer);");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.Append(Sentence(rng, 18)).AppendLine();
        return sb.ToString();
    }

    private static string Sections(Random rng, int count)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(Title(rng)).AppendLine().AppendLine();
        for (var i = 0; i < count; i++)
            Section(sb, rng, i);
        sb.AppendLine("[^1]: A footnote shared by every section.");
        return sb.ToString();
    }

    // One section exercises every construct once, so that each extension package has work in every size,
    // and a library without the extension renders the same bytes as plain text or a code block.
    private static void Section(StringBuilder sb, Random rng, int index)
    {
        sb.Append("## ").Append(index + 1).Append(". ").Append(Title(rng)).AppendLine().AppendLine();

        for (var p = 0; p < 3; p++)
            sb.AppendLine(Paragraph(rng)).AppendLine();

        sb.AppendLine("- " + Sentence(rng, 8));
        sb.AppendLine("- " + Sentence(rng, 6));
        sb.AppendLine("  - " + Sentence(rng, 5));
        sb.AppendLine("  - " + Sentence(rng, 7));
        sb.AppendLine("- [x] " + Sentence(rng, 5));
        sb.AppendLine("- [ ] " + Sentence(rng, 6));
        sb.AppendLine();

        sb.AppendLine("1. " + Sentence(rng, 7));
        sb.AppendLine("2. " + Sentence(rng, 9));
        sb.AppendLine("3. " + Sentence(rng, 5));
        sb.AppendLine();

        sb.AppendLine("> " + Sentence(rng, 20));
        sb.AppendLine();
        sb.AppendLine("> [!NOTE]");
        sb.AppendLine("> " + Sentence(rng, 14));
        sb.AppendLine();

        sb.AppendLine("### Example").AppendLine();
        CodeBlock(sb, rng, index % 3);

        sb.AppendLine("| Name | Kind | Size | Notes |");
        sb.AppendLine("|---|:---:|---:|---|");
        for (var r = 0; r < 6; r++)
            sb.Append("| `").Append(Word(rng)).Append("` | ").Append(Word(rng)).Append(" | ").Append(rng.Next(1, 5000))
              .Append(" | ").Append(Sentence(rng, 5)).AppendLine(" |");
        sb.AppendLine();

        sb.Append("The cost is $O(n \\log n)$ for $n = ").Append(rng.Next(2, 99)).AppendLine("$ blocks, and:");
        sb.AppendLine();
        sb.AppendLine("$$");
        sb.AppendLine("\\sum_{i=1}^{n} \\frac{w_i}{h_i} = \\int_0^1 f(x)\\,dx");
        sb.AppendLine("$$");
        sb.AppendLine();

        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart LR");
        sb.Append("    A[").Append(Word(rng)).Append("] --> B{").Append(Word(rng)).AppendLine("}");
        sb.Append("    B -->|yes| C[").Append(Word(rng)).AppendLine("]");
        sb.Append("    B -->|no| D[").Append(Word(rng)).AppendLine("]");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.Append("Press <kbd>Ctrl</kbd>+<kbd>F</kbd> to find ").Append(Word(rng))
          .Append(" :smile: ~~").Append(Word(rng)).Append("~~ www.example.com/").Append(Word(rng)).AppendLine("[^1].");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void CodeBlock(StringBuilder sb, Random rng, int kind)
    {
        switch (kind)
        {
            case 0:
                sb.AppendLine("```csharp");
                sb.Append("public sealed class ").Append(Pascal(rng)).AppendLine(" : IDisposable");
                sb.AppendLine("{");
                sb.Append("    private readonly List<string> _").Append(Word(rng)).AppendLine(" = [];");
                sb.AppendLine();
                sb.Append("    public int ").Append(Pascal(rng)).AppendLine("(string text, int count = 3)");
                sb.AppendLine("    {");
                sb.AppendLine("        // Sum the lengths of the first lines.");
                sb.AppendLine("        var total = 0;");
                sb.AppendLine("        foreach (var line in text.Split('\\n').Take(count))");
                sb.AppendLine("            total += line.Length;");
                sb.Append("        return total + ").Append(rng.Next(100)).AppendLine(";");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    public void Dispose() { }");
                sb.AppendLine("}");
                break;
            case 1:
                sb.AppendLine("```json");
                sb.AppendLine("{");
                sb.Append("  \"name\": \"").Append(Word(rng)).AppendLine("\",");
                sb.Append("  \"version\": \"").Append(rng.Next(1, 20)).Append('.').Append(rng.Next(10)).AppendLine(".0\",");
                sb.AppendLine("  \"enabled\": true,");
                sb.Append("  \"tags\": [\"").Append(Word(rng)).Append("\", \"").Append(Word(rng)).AppendLine("\"],");
                sb.AppendLine("  \"limits\": { \"width\": 900, \"height\": 700 }");
                sb.AppendLine("}");
                break;
            default:
                sb.AppendLine("```python");
                sb.Append("def ").Append(Word(rng)).AppendLine("(items, factor=2):");
                sb.AppendLine("    \"\"\"Scale every item.\"\"\"");
                sb.AppendLine("    result = []");
                sb.AppendLine("    for item in items:");
                sb.AppendLine("        if item is None:");
                sb.AppendLine("            continue");
                sb.AppendLine("        result.append(item * factor)");
                sb.AppendLine("    return result");
                break;
        }
        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static string Paragraph(Random rng)
    {
        var sb = new StringBuilder();
        var sentences = rng.Next(3, 6);
        for (var s = 0; s < sentences; s++)
        {
            if (s > 0)
                sb.Append(' ');
            var sentence = Sentence(rng, rng.Next(8, 20));
            switch (rng.Next(6))
            {
                case 0: sb.Append(sentence.Replace(" the ", " **the** ")); break;
                case 1: sb.Append(sentence.Replace(" a ", " *a* ")); break;
                case 2: sb.Append(sentence).Append(" See `").Append(Word(rng)).Append("`."); break;
                case 3: sb.Append(sentence).Append(" Read [the ").Append(Word(rng)).Append(" guide](https://example.com/").Append(Word(rng)).Append(")."); break;
                default: sb.Append(sentence); break;
            }
        }
        return sb.ToString();
    }

    private static string Sentence(Random rng, int words)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < words; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(Word(rng));
        }
        sb[0] = char.ToUpperInvariant(sb[0]);
        sb.Append('.');
        return sb.ToString();
    }

    private static string Title(Random rng)
    {
        var t = Sentence(rng, rng.Next(2, 5));
        return t[..^1];
    }

    private static string Word(Random rng) => Words[rng.Next(Words.Length)];

    private static string Pascal(Random rng)
    {
        var w = Word(rng);
        return char.ToUpperInvariant(w[0]) + w[1..] + char.ToUpperInvariant(Word(rng)[0]) + "er";
    }
}
