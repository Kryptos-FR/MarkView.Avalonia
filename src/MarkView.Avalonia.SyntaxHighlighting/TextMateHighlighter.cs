// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.ObjectModel;

using Avalonia.Media;
using Avalonia.Media.Immutable;
using MarkView.Avalonia.Extensions;
using TextMateSharp.Grammars;
using TextMateSharp.Themes;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Tokenises source-code lines using TextMate grammars and maps token
/// scopes to <see cref="IBrush"/> colours via the chosen theme.
/// </summary>
public sealed class TextMateHighlighter : ICodeHighlighter
{
    private readonly TextMateGrammars _grammars;
    private readonly Theme _theme;

    // Brush cache: theme colour hex string → brush (avoids per-token allocation)
    private readonly Dictionary<string, IBrush> _brushCache = new(StringComparer.OrdinalIgnoreCase);

    // Tokens of recent lines; read-only, since the same list is returned for the same line.
    private readonly LineTokenCache<ReadOnlyCollection<(string Text, IBrush? Foreground)>> _tokenCache = new();

    public TextMateHighlighter(ThemeName theme = ThemeName.DarkPlus)
        : this(new TextMateGrammars(theme), theme)
    {
    }

    internal TextMateHighlighter(TextMateGrammars grammars, ThemeName theme)
    {
        _grammars = grammars;
        _theme = Theme.CreateFromRawTheme(grammars.Options.LoadTheme(theme), grammars.Options);
    }

    /// <inheritdoc/>
    public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language)
    {
        if (string.IsNullOrEmpty(language))
            return null;

        var grammar = _grammars.Get(language);
        if (grammar == null)
            return null;

        if (_tokenCache.TryGet(grammar, line.Span, out var cached))
            return cached;

        var tokens = new List<(string Text, IBrush? Foreground)>();
        foreach (var (text, scopes) in Tokenize(grammar, line))
            tokens.Add((text, GetBrush(scopes)));
        var result = tokens.AsReadOnly();
        _tokenCache.Add(grammar, line.Span, result);
        return result;
    }

    /// <summary>
    /// Splits <paramref name="line"/> into its tokens' text and scopes. Scopes do not depend on
    /// the colour theme; <see cref="GetBrush"/> maps them to a colour.
    /// </summary>
    internal static IEnumerable<(string Text, IList<string> Scopes)> Tokenize(IGrammar grammar, ReadOnlyMemory<char> line)
    {
        // Implicit ReadOnlyMemory<char> → LineText conversion; TextMateSharp 2.x uses
        // ArrayPool internally for the required trailing '\n', avoiding a string allocation.
        var result = grammar.TokenizeLine(line, null, TimeSpan.MaxValue);

        for (int i = 0; i < result.Tokens.Length; i++)
        {
            var token = result.Tokens[i];
            int start = token.StartIndex;
            int end = i + 1 < result.Tokens.Length
                ? result.Tokens[i + 1].StartIndex
                : line.Length;
            end = Math.Min(end, line.Length);

            if (start >= end)
                continue;

            yield return (line.Span[start..end].ToString(), token.Scopes);
        }
    }

    internal IBrush? GetBrush(IList<string> scopes)
    {
        var rules = _theme.Match(scopes);
        if (rules.Count == 0 || rules[0].foreground == 0)
            return null;

        var hex = _theme.GetColor(rules[0].foreground);
        if (string.IsNullOrEmpty(hex))
            return null;

        if (!_brushCache.TryGetValue(hex, out var brush))
            _brushCache[hex] = brush = new ImmutableSolidColorBrush(Color.Parse(hex));
        return brush;
    }
}
