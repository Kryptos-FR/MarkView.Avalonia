// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;

using Markdig.Syntax;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Replaces the built-in <see cref="CodeBlockRenderer"/> when syntax highlighting is active.
/// For fenced code blocks it calls <see cref="IThemeAwareCodeHighlighter.HighlightVariant"/>
/// and subscribes to theme changes so token colours update in-place when the user switches
/// between light and dark themes — without rebuilding the surrounding document tree.
/// </summary>
public class TextMateCodeBlockRenderer : AvaloniaObjectRenderer<CodeBlock>
{
    protected override void Write(AvaloniaRenderer renderer, CodeBlock obj)
    {
        var language = obj is FencedCodeBlock fenced ? fenced.Info : null;

        var textBlock = new TextBlock { TextWrapping = TextWrapping.NoWrap };
        var border = new Border { Child = textBlock };
        border.Classes.Add("markdown-code-block");

        if (!string.IsNullOrEmpty(language))
            border.Classes.Add($"language-{language}");

        if (obj.Lines.Lines == null)
        {
            renderer.WriteBlock(border);
            return;
        }

        // Materialise source lines once so they can be reused on theme change.
        var lineTexts = Enumerable.Range(0, obj.Lines.Count)
            .Select(i => obj.Lines.Lines[i].Slice.ToString())
            .ToList();

        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        BuildInlines(textBlock, renderer.CodeHighlighter, language, isDark, lineTexts);

        // If the highlighter is theme-aware, rebuild only TextBlock.Inlines when the
        // theme changes — the Border and its position in the document stay untouched.
        if (renderer.CodeHighlighter is IThemeAwareCodeHighlighter themeAware)
        {
            void OnThemeChanged(object? s, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property.Name != nameof(Application.ActualThemeVariant)) return;
                var newIsDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                textBlock.Inlines!.Clear();
                BuildInlines(textBlock, themeAware, language, newIsDark, lineTexts);
            }
            Application.Current!.PropertyChanged += OnThemeChanged;
            border.DetachedFromVisualTree += (_, _) => Application.Current?.PropertyChanged -= OnThemeChanged;
        }

        renderer.WriteBlock(border);
    }

    private static void BuildInlines(
        TextBlock textBlock,
        ICodeHighlighter? highlighter,
        string? language,
        bool isDark,
        IReadOnlyList<string> lineTexts)
    {
        for (int i = 0; i < lineTexts.Count; i++)
        {
            if (i > 0) textBlock.Inlines!.Add(new LineBreak());

            var lineText = lineTexts[i];
            var tokens = highlighter is IThemeAwareCodeHighlighter th
                ? th.HighlightVariant(lineText, language, isDark)
                : highlighter?.Highlight(lineText, language);

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
}
