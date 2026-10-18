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
/// Fills a code block's <see cref="TextBlock"/> with highlighted lines, at once or once the block nears
/// the viewport, and keeps the token colours following the theme. Shared by the code block renderers of the extension packages.
/// </summary>
internal static class HighlightedCode
{
    /// <summary>
    /// Writes the lines of <paramref name="block"/> into <paramref name="textBlock"/>, highlighted by the
    /// renderer's <see cref="AvaloniaRenderer.CodeHighlighter"/>, and, when the highlighter is theme-aware,
    /// recolours them whenever the theme of <paramref name="border"/> changes. With
    /// <see cref="AvaloniaRenderer.IsLazyRenderingEnabled"/>, the lines are written as plain text and only
    /// highlighted once <paramref name="border"/> comes within a screen of the viewport.
    /// </summary>
    public static void Write(
        AvaloniaRenderer renderer,
        Border border,
        TextBlock textBlock,
        CodeBlock block,
        string? language)
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

        var highlighter = renderer.CodeHighlighter;
        if (highlighter != null && renderer.IsLazyRenderingEnabled)
        {
            // The plain lines have the same text and the same monospace layout as the highlighted
            // ones, so the block can be selected, searched and copied before it is coloured, and the
            // colours, applied before the block is first shown, move nothing.
            BuildInlines(textBlock, null, language, false, lines);
            ViewportTracking.RunWhenNearViewport(border, () =>
            {
                textBlock.Inlines!.Clear();
                Highlight(border, textBlock, highlighter, language, lines);
            });
            return;
        }

        Highlight(border, textBlock, highlighter, language, lines);
    }

    private static void Highlight(
        Border border,
        TextBlock textBlock,
        ICodeHighlighter? highlighter,
        string? language,
        IReadOnlyList<ReadOnlyMemory<char>> lines)
    {
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

            for (var t = 0; t < tokens.Count;)
            {
                var (_, dark, light) = tokens[t];
                var end = t + 1;
                while (end < tokens.Count && SameBrush(tokens[end].Dark, dark) && SameBrush(tokens[end].Light, light))
                    end++;
                var run = new Run(Concat(tokens, t, end, static token => token.Text));
                t = end;
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

    // Highlighters split a line by grammar scope, and neighbouring tokens often share a colour (a keyword's
    // space, a run of punctuation). Each token being its own Run is what makes a highlighted block cost
    // several times a plain one to create and lay out, so tokens of one colour go into one Run.
    private static bool SameBrush(IBrush? a, IBrush? b)
        => ReferenceEquals(a, b)
           || (a is ISolidColorBrush x && b is ISolidColorBrush y && x.Color == y.Color && x.Opacity == y.Opacity);

    // The text of tokens [first, end): a single token keeps its string.
    private static string Concat<T>(IReadOnlyList<T> tokens, int first, int end, Func<T, string> text)
    {
        // Stryker disable once Arithmetic : concatenating a single token gives the same text; only the copy is saved
        if (end - first == 1)
            return text(tokens[first]);

        var length = 0;
        for (var t = first; t < end; t++)
            length += text(tokens[t]).Length;
        return string.Create(length, (tokens, first, end, text), static (span, state) =>
        {
            for (var t = state.first; t < state.end; t++)
            {
                var part = state.text(state.tokens[t]);
                part.CopyTo(span);
                span = span[part.Length..];
            }
        });
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
                for (var t = 0; t < tokens.Count;)
                {
                    var foreground = tokens[t].Foreground;
                    var end = t + 1;
                    while (end < tokens.Count && SameBrush(tokens[end].Foreground, foreground))
                        end++;
                    var run = new Run(Concat(tokens, t, end, static token => token.Text));
                    t = end;
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
