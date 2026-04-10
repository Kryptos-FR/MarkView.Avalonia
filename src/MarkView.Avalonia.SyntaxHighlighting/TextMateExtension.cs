// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using TextMateSharp.Grammars;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Wires <see cref="DualThemeTextMateHighlighter"/> and <see cref="TextMateCodeBlockRenderer"/>
/// into an <see cref="AvaloniaRenderer"/>. Two <see cref="TextMateHighlighter"/> instances are
/// created lazily — one for dark themes and one for light — and exposed as an
/// <see cref="IThemeAwareCodeHighlighter"/> so code block renderers can update token colours
/// in-place when the theme changes without rebuilding the entire document.
/// </summary>
public sealed class TextMateExtension : IMarkViewExtension
{
    private readonly ThemeName _darkTheme;
    private readonly ThemeName _lightTheme;
    private TextMateHighlighter? _darkHighlighter;
    private TextMateHighlighter? _lightHighlighter;

    public TextMateExtension(ThemeName darkTheme = ThemeName.DarkPlus, ThemeName lightTheme = ThemeName.LightPlus)
    {
        _darkTheme = darkTheme;
        _lightTheme = lightTheme;
    }

    public void Register(AvaloniaRenderer renderer)
    {
        renderer.CodeHighlighter = new DualThemeTextMateHighlighter(GetDark(), GetLight());
        renderer.ReplaceOrAdd<CodeBlockRenderer>(new TextMateCodeBlockRenderer());
    }

    private TextMateHighlighter GetDark() => _darkHighlighter ??= new TextMateHighlighter(_darkTheme);
    private TextMateHighlighter GetLight() => _lightHighlighter ??= new TextMateHighlighter(_lightTheme);
}
