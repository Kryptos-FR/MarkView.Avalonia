// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

using MarkView.Avalonia.Extensions;

using TextMateSharp.Grammars;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Highlights with a dark and a light TextMate theme that share one set of grammars, and
/// implements <see cref="IDualThemeCodeHighlighter"/> so code block renderers tokenise each line
/// once and recolour the tokens in-place when the theme changes.
/// </summary>
internal sealed class DualThemeTextMateHighlighter : IDualThemeCodeHighlighter
{
    private readonly TextMateGrammars _grammars;
    private readonly TextMateHighlighter _dark;
    private readonly TextMateHighlighter _light;
    private readonly LineTokenCache<(string Text, IBrush? Dark, IBrush? Light)[]> _cache = new();

    public DualThemeTextMateHighlighter(ThemeName darkTheme, ThemeName lightTheme)
    {
        _grammars = new TextMateGrammars(darkTheme);
        _dark = new TextMateHighlighter(_grammars, darkTheme);
        _light = new TextMateHighlighter(_grammars, lightTheme);
    }

    public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language)
        => HighlightVariant(line, language, Application.Current?.ActualThemeVariant == ThemeVariant.Dark);

    public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language, ref object? state)
        => HighlightVariant(line, language, Application.Current?.ActualThemeVariant == ThemeVariant.Dark, ref state);

    public IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(ReadOnlyMemory<char> line, string? language, bool isDark)
        => isDark ? _dark.Highlight(line, language) : _light.Highlight(line, language);

    // Both themes share the grammars, so a state one of them returned carries over to the other.
    public IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(ReadOnlyMemory<char> line, string? language, bool isDark, ref object? state)
        => isDark ? _dark.Highlight(line, language, ref state) : _light.Highlight(line, language, ref state);

    public IReadOnlyList<(string Text, IBrush? Dark, IBrush? Light)>? HighlightBoth(ReadOnlyMemory<char> line, string? language)
    {
        object? state = null;
        return HighlightBoth(line, language, ref state);
    }

    public IReadOnlyList<(string Text, IBrush? Dark, IBrush? Light)>? HighlightBoth(ReadOnlyMemory<char> line, string? language, ref object? state)
    {
        if (string.IsNullOrEmpty(language))
            return null;

        var grammar = _grammars.Get(language);
        if (grammar == null)
            return null;

        var start = state as IStateStack;
        if (_cache.TryGet(grammar, start, line.Span, out var cached, out var end))
        {
            state = end;
            return cached;
        }

        var tokens = new List<(string Text, IBrush? Dark, IBrush? Light)>();
        end = TextMateHighlighter.Tokenize(grammar, line, start, (text, scopes) => tokens.Add((text, _dark.GetBrush(scopes), _light.GetBrush(scopes))));
        var result = tokens.ToArray();
        _cache.Add(grammar, start, line.Span, result, end);
        state = end;
        return result;
    }
}
