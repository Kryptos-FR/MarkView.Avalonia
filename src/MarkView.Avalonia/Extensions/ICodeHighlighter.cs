// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Tokenises a single source-code line into coloured text segments.
/// Return <c>null</c> to signal that the language is unsupported; the
/// caller should fall back to plain monochrome rendering.
/// An empty list means the language is supported but the line produces no tokens.
/// </summary>
public interface ICodeHighlighter
{
    IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language);

    /// <summary>
    /// Highlights <paramref name="line"/> from the state the previous line of its block ended in, so a construct
    /// that spans lines (a block comment, a multi-line string) is coloured on every line it covers.
    /// <paramref name="state"/> is <c>null</c> for the first line of a block and receives the state this line ends
    /// in, to pass with the next line. The default ignores it and highlights the line on its own.
    /// </summary>
    IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language, ref object? state)
        => Highlight(line, language);
}

/// <summary>
/// Extends <see cref="ICodeHighlighter"/> with explicit dark/light variant highlighting,
/// enabling code block renderers to update token colours in-place when the theme changes
/// without rebuilding the entire document.
/// </summary>
public interface IThemeAwareCodeHighlighter : ICodeHighlighter
{
    IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(ReadOnlyMemory<char> line, string? language, bool isDark);

    /// <summary>
    /// <see cref="HighlightVariant(ReadOnlyMemory{char}, string?, bool)"/> from the state the previous line ended in:
    /// see <see cref="ICodeHighlighter.Highlight(ReadOnlyMemory{char}, string?, ref object?)"/>. The default ignores
    /// <paramref name="state"/>.
    /// </summary>
    IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(ReadOnlyMemory<char> line, string? language, bool isDark, ref object? state)
        => HighlightVariant(line, language, isDark);
}

/// <summary>
/// Extends <see cref="IThemeAwareCodeHighlighter"/> by tokenising a line once and returning the
/// colour of every token for both variants, so a theme switch can recolour the existing runs
/// without tokenising the code again or rebuilding the text.
/// </summary>
public interface IDualThemeCodeHighlighter : IThemeAwareCodeHighlighter
{
    IReadOnlyList<(string Text, IBrush? Dark, IBrush? Light)>? HighlightBoth(ReadOnlyMemory<char> line, string? language);

    /// <summary>
    /// <see cref="HighlightBoth(ReadOnlyMemory{char}, string?)"/> from the state the previous line ended in: see
    /// <see cref="ICodeHighlighter.Highlight(ReadOnlyMemory{char}, string?, ref object?)"/>. The default ignores
    /// <paramref name="state"/>.
    /// </summary>
    IReadOnlyList<(string Text, IBrush? Dark, IBrush? Light)>? HighlightBoth(ReadOnlyMemory<char> line, string? language, ref object? state)
        => HighlightBoth(line, language);
}
