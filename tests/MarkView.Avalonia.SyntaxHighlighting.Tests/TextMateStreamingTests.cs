// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;

using Xunit;

namespace MarkView.Avalonia.SyntaxHighlighting.Tests;

/// <summary>A highlighted code block that ends the text of a streaming session, which is written in chunks of lines.</summary>
public class TextMateStreamingTests
{
    private static List<TextBlock> Chunks(MarkdownViewer viewer)
    {
        var border = viewer.GetLogicalDescendants().OfType<Border>().First(b => b.Classes.Contains("markdown-code-block"));
        var chunks = Assert.IsType<StackPanel>(border.Child);
        Assert.Contains("markdown-code-chunks", chunks.Classes);
        return chunks.Children.Cast<TextBlock>().ToList();
    }

    private static Color ColourOf(TextBlock code, string text) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(code.Inlines!.OfType<Run>().First(r => r.Text!.Contains(text)).Foreground).Color;

    private static string Statements(int count) => string.Concat(Enumerable.Repeat("int a;\n", count));

    [AvaloniaFact]
    public void Chunk_is_highlighted_from_the_state_the_chunk_before_it_ended_in()
    {
        var viewer = new MarkdownViewer().UseTextMateHighlighting();
        var session = viewer.BeginStreaming();

        // A block comment opens on the last line of the first chunk and goes on in the second.
        session.Append("```csharp\n" + Statements(31) + "/* opened\nstill inside\n*/ int b;\n");
        session.ApplyPendingUpdate();

        var chunks = Chunks(viewer);
        Assert.Equal(2, chunks.Count);
        Assert.Equal(ColourOf(chunks[0], "/* opened"), ColourOf(chunks[1], "still inside"));
        Assert.NotEqual(ColourOf(chunks[0], "/* opened"), ColourOf(chunks[1], "int"));
    }

    [AvaloniaFact]
    public void Kept_chunk_hands_its_state_to_the_chunks_written_after_it()
    {
        var viewer = new MarkdownViewer().UseTextMateHighlighting();
        var session = viewer.BeginStreaming();
        session.Append("```csharp\n" + Statements(31) + "/* opened\n" + Statements(2));
        session.ApplyPendingUpdate();
        var first = Chunks(viewer)[0];

        session.Append("still inside\n");
        session.ApplyPendingUpdate();

        var chunks = Chunks(viewer);
        Assert.Same(first, chunks[0]);
        Assert.Equal(ColourOf(chunks[0], "/* opened"), ColourOf(chunks[1], "still inside"));
    }
}
