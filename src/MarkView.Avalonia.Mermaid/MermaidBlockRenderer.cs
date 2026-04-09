using Avalonia.Controls;
using Avalonia.Media;
using Markdig.Syntax;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// Handles <c>```mermaid</c> fenced code blocks by rendering a styled fallback border
/// that displays the diagram source.
/// Non-mermaid fenced blocks are rendered as standard styled code blocks.
/// </summary>
/// <remarks>
/// <para>
/// A native WebView-based rendering path (using <c>Avalonia.Controls.WebView</c>) was
/// considered but not implemented because that package requires a commercial AvaloniaUI
/// subscription and is therefore incompatible with this open-source library.
/// </para>
/// </remarks>
public class MermaidBlockRenderer : AvaloniaObjectRenderer<FencedCodeBlock>
{
    private readonly double _initialHeight;

    public MermaidBlockRenderer(double initialHeight = 300)
    {
        _initialHeight = initialHeight;
    }

    protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        if (string.Equals(obj.Info, "mermaid", StringComparison.OrdinalIgnoreCase))
        {
            WriteFallback(renderer, ExtractSource(obj));
        }
        else
        {
            WriteStandardCodeBlock(renderer, obj);
        }
    }

    private static string ExtractSource(FencedCodeBlock block)
    {
        if (block.Lines.Lines == null)
            return string.Empty;

        var lines = block.Lines;
        return string.Join("\n",
            Enumerable.Range(0, lines.Count)
                      .Select(i => lines.Lines[i].Slice.ToString()));
    }

    private static void WriteFallback(AvaloniaRenderer renderer, string source)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Mermaid diagram (preview unavailable on this platform)" });
        panel.Children.Add(new TextBlock { Text = source });

        var border = new Border { Child = panel };
        border.Classes.Add("markdown-mermaid-fallback");

        renderer.WriteBlock(border);
    }

    private static void WriteStandardCodeBlock(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
        };

        var border = new Border { Child = textBlock };
        border.Classes.Add("markdown-code-block");

        if (!string.IsNullOrEmpty(obj.Info))
            border.Classes.Add($"language-{obj.Info}");

        renderer.Push(textBlock.Inlines!);
        renderer.WriteLeafRawLines(obj);
        renderer.Pop();

        renderer.WriteBlock(border);
    }
}
