// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig.Syntax;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Replaces the built-in <see cref="CodeBlockRenderer"/> when syntax highlighting is active.
/// For each source line it calls <see cref="AvaloniaRenderer.CodeHighlighter"/> and emits
/// coloured <see cref="Run"/> elements; falls back to plain <see cref="Run"/> when the
/// highlighter returns <c>null</c> (unsupported language or no highlighter registered).
/// </summary>
public class TextMateCodeBlockRenderer : AvaloniaObjectRenderer<CodeBlock>
{
    protected override void Write(AvaloniaRenderer renderer, CodeBlock obj)
    {
        var language = obj is FencedCodeBlock fenced ? fenced.Info : null;

        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
        };

        var border = new Border { Child = textBlock };
        border.Classes.Add("markdown-code-block");

        if (!string.IsNullOrEmpty(language))
            border.Classes.Add($"language-{language}");

        if (obj.Lines.Lines == null)
        {
            renderer.WriteBlock(border);
            return;
        }

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

        renderer.WriteBlock(border);
    }
}
