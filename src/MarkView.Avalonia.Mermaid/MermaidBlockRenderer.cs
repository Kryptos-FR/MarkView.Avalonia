using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Platform;
using Markdig.Syntax;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// Handles all <see cref="FencedCodeBlock"/> nodes.  Mermaid blocks are rendered
/// via a native WebView (or a text fallback on Linux); all other fenced blocks are
/// rendered as styled code blocks and respect any <see cref="AvaloniaRenderer.CodeHighlighter"/>
/// that has been registered (e.g. by the SyntaxHighlighting extension).
/// </summary>
public class MermaidBlockRenderer : AvaloniaObjectRenderer<FencedCodeBlock>
{
    private static readonly Uri MermaidJsUri =
        new("avares://MarkView.Avalonia.Mermaid/Assets/mermaid.min.js");

    private readonly double _initialHeight;

    public MermaidBlockRenderer(double initialHeight = 300)
    {
        _initialHeight = initialHeight;
    }

    protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        if (string.Equals(obj.Info, "mermaid", StringComparison.OrdinalIgnoreCase))
        {
            var source = ExtractSource(obj);
            if (OperatingSystem.IsLinux())
                WriteFallback(renderer, source);
            else
                WriteWebView(renderer, source);
        }
        else
        {
            WriteStandardCodeBlock(renderer, obj);
        }
    }

    private void WriteWebView(AvaloniaRenderer renderer, string source)
    {
        var safeSource = JsonSerializer.Serialize(source);

        // NavigateToString has a 2 MB COM limit; mermaid.min.js alone exceeds it.
        // Write both files to a temp directory and Navigate to a file:// URI instead.
        var tempDir = Path.Combine(Path.GetTempPath(), $"markview-mermaid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        using (var assetStream = AssetLoader.Open(MermaidJsUri))
        using (var jsFile = File.Create(Path.Combine(tempDir, "mermaid.min.js")))
            assetStream.CopyTo(jsFile);

        var htmlPath = Path.Combine(tempDir, "diagram.html");
        File.WriteAllText(htmlPath, BuildHtml(safeSource));

        var webView = new NativeWebView { Height = _initialHeight };
        webView.Navigate(new Uri(htmlPath));
        webView.DetachedFromVisualTree += (_, _) =>
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        };

        renderer.WriteBlock(webView);
    }

    private static string BuildHtml(string safeSource) => $$"""
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8" />
          <style>
            body { margin: 0; padding: 8px; background: transparent; }
            .mermaid { width: 100%; }
          </style>
        </head>
        <body>
          <div id="diagram"></div>
          <script src="mermaid.min.js"></script>
          <script>
            mermaid.initialize({ startOnLoad: false, theme: 'default' });
            mermaid.render('mermaid-svg', {{safeSource}}).then(function(result) {
              document.getElementById('diagram').innerHTML = result.svg;
            });
          </script>
        </body>
        </html>
        """;

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
        var language = obj.Info;
        var textBlock = new TextBlock { TextWrapping = TextWrapping.NoWrap };
        var border = new Border { Child = textBlock };
        border.Classes.Add("markdown-code-block");

        if (!string.IsNullOrEmpty(language))
            border.Classes.Add($"language-{language}");

        if (obj.Lines.Lines != null)
        {
            var lines = obj.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                    textBlock.Inlines!.Add(new LineBreak());

                var lineText = lines.Lines[i].Slice.ToString();
                var tokens = renderer.CodeHighlighter?.Highlight(lineText, language);

                if (tokens != null)
                {
                    foreach (var (text, foreground) in tokens)
                    {
                        var run = new Run(text);
                        if (foreground != null)
                            run.Foreground = foreground;
                        textBlock.Inlines!.Add(run);
                    }
                }
                else
                {
                    textBlock.Inlines!.Add(new Run(lineText));
                }
            }
        }

        renderer.WriteBlock(border);
    }
}
