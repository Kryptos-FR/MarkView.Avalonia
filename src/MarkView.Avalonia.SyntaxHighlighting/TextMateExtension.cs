// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using TextMateSharp.Grammars;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Wires <see cref="DualThemeTextMateHighlighter"/> and <see cref="TextMateCodeBlockRenderer"/>
/// into an <see cref="AvaloniaRenderer"/>. The highlighter is created lazily, holds the dark and
/// the light theme over one shared set of grammars, and is exposed as an
/// <see cref="IDualThemeCodeHighlighter"/> so code block renderers can update token colours
/// in-place when the theme changes without rebuilding the entire document.
/// </summary>
public sealed class TextMateExtension : IMarkViewExtension
{
    private readonly ThemeName _darkTheme;
    private readonly ThemeName _lightTheme;
    private DualThemeTextMateHighlighter? _highlighter;

    public TextMateExtension(ThemeName darkTheme = ThemeName.DarkPlus, ThemeName lightTheme = ThemeName.LightPlus)
    {
        _darkTheme = darkTheme;
        _lightTheme = lightTheme;
    }

    public void Register(AvaloniaRenderer renderer)
    {
        renderer.CodeHighlighter = GetHighlighter();
        renderer.ReplaceOrAdd<CodeBlockRenderer>(new TextMateCodeBlockRenderer());
    }

    // Stryker disable once assignment : lazily caches the highlighter; without the cache every Register() builds its own instance whose highlighting output is identical, only instance reuse differs
    private DualThemeTextMateHighlighter GetHighlighter() => _highlighter ??= new DualThemeTextMateHighlighter(_darkTheme, _lightTheme);
}
