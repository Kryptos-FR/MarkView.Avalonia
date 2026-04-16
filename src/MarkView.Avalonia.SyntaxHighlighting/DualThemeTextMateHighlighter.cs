// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Wraps a dark and a light <see cref="TextMateHighlighter"/> and implements
/// <see cref="IThemeAwareCodeHighlighter"/> so code block renderers can update
/// token colours in-place when the theme changes.
/// </summary>
internal sealed class DualThemeTextMateHighlighter(TextMateHighlighter dark, TextMateHighlighter light)
    : IThemeAwareCodeHighlighter
{
    public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(ReadOnlyMemory<char> line, string? language)
        => HighlightVariant(line, language, Application.Current?.ActualThemeVariant == ThemeVariant.Dark);

    public IReadOnlyList<(string Text, IBrush? Foreground)>? HighlightVariant(ReadOnlyMemory<char> line, string? language, bool isDark)
        => isDark ? dark.Highlight(line, language) : light.Highlight(line, language);
}
