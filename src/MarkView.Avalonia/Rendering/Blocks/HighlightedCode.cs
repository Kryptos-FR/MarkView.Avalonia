// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;

using Markdig.Syntax;

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Fills a code block's <see cref="TextBlock"/> with highlighted lines and keeps the token
/// colours following the theme. Shared by the code block renderers of the extension packages.
/// </summary>
internal static class HighlightedCode
{
    /// <summary>
    /// Writes the lines of <paramref name="block"/> into <paramref name="textBlock"/> and, when the
    /// highlighter is theme-aware, recolours them whenever the theme of <paramref name="border"/> changes.
    /// </summary>
    public static void Write(
        Border border,
        TextBlock textBlock,
        CodeBlock block,
        string? language,
        ICodeHighlighter? highlighter)
    {
        if (block.Lines.Lines == null)
            return;

        // Materialise source lines once so they can be reused on theme change.
        // AsMemory() references the Markdig source buffer directly — no per-line allocation.
        var lines = new List<ReadOnlyMemory<char>>(block.Lines.Count);
        for (int i = 0; i < block.Lines.Count; i++)
        {
            var slice = block.Lines.Lines[i].Slice;
            lines.Add(slice.Text.AsMemory(slice.Start, slice.Length));
        }

        var isDark = IsDark();
        switch (highlighter)
        {
            case IDualThemeCodeHighlighter dual:
                WriteDual(border, textBlock, dual, language, isDark, lines);
                break;
            case IThemeAwareCodeHighlighter themeAware:
                BuildInlines(textBlock, themeAware, language, isDark, lines);
                // Rebuild only TextBlock.Inlines when the theme changes; the Border and its
                // position in the document stay untouched.
                ThemeTracking.ReapplyOnThemeChange(border, () =>
                {
                    textBlock.Inlines!.Clear();
                    BuildInlines(textBlock, themeAware, language, IsDark(), lines);
                });
                break;
            default:
                BuildInlines(textBlock, highlighter, language, isDark, lines);
                break;
        }
    }

    private static bool IsDark() => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    // Tokenises once. Tokens whose two colours are both opaque solid colours share one mutable
    // brush per colour pair: a theme switch only changes those brushes' colours, which needs a
    // repaint but no new runs, no re-tokenising and no new text layout. Any other token keeps
    // its brushes and has its Foreground swapped instead.
    private static void WriteDual(
        Border border,
        TextBlock textBlock,
        IDualThemeCodeHighlighter highlighter,
        string? language,
        bool isDark,
        IReadOnlyList<ReadOnlyMemory<char>> lines)
    {
        Dictionary<(Color Dark, Color Light), SolidColorBrush>? palette = null;
        List<(Run Run, IBrush? Dark, IBrush? Light)>? others = null;

        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) textBlock.Inlines!.Add(new LineBreak());

            var tokens = highlighter.HighlightBoth(lines[i], language);
            if (tokens == null)
            {
                textBlock.Inlines!.Add(new Run(lines[i].ToString()));
                continue;
            }

            foreach (var (text, dark, light) in tokens)
            {
                var run = new Run(text);
                if (dark is ISolidColorBrush { Opacity: 1 } d && light is ISolidColorBrush { Opacity: 1 } l)
                {
                    palette ??= [];
                    if (!palette.TryGetValue((d.Color, l.Color), out var brush))
                        palette[(d.Color, l.Color)] = brush = new SolidColorBrush(isDark ? d.Color : l.Color);
                    run.Foreground = brush;
                }
                else if (dark != null || light != null)
                {
                    (others ??= []).Add((run, dark, light));
                    SetForeground(run, isDark ? dark : light);
                }
                textBlock.Inlines!.Add(run);
            }
        }

        if (palette == null && others == null)
            return;

        ThemeTracking.ReapplyOnThemeChange(border, () =>
        {
            var dark = IsDark();
            if (palette != null)
            {
                foreach (var ((darkColor, lightColor), brush) in palette)
                    brush.Color = dark ? darkColor : lightColor;
            }
            if (others != null)
            {
                foreach (var (run, darkBrush, lightBrush) in others)
                    SetForeground(run, dark ? darkBrush : lightBrush);
            }
        });
    }

    // A token without a brush inherits the TextBlock foreground, which follows the theme on its own.
    private static void SetForeground(Run run, IBrush? brush)
    {
        if (brush != null)
            run.Foreground = brush;
        else
            run.ClearValue(TextElement.ForegroundProperty);
    }

    private static void BuildInlines(
        TextBlock textBlock,
        ICodeHighlighter? highlighter,
        string? language,
        bool isDark,
        IReadOnlyList<ReadOnlyMemory<char>> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) textBlock.Inlines!.Add(new LineBreak());

            var line = lines[i];
            var tokens = highlighter is IThemeAwareCodeHighlighter th
                ? th.HighlightVariant(line, language, isDark)
                : highlighter?.Highlight(line, language);

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
                textBlock.Inlines!.Add(new Run(line.ToString()));
            }
        }
    }
}
