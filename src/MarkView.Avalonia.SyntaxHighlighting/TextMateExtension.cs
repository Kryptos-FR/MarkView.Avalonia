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
/// Every extension with the same pair of themes shares one highlighter while any of them is alive,
/// so a viewer per message (a chat) loads the themes and grammars once, not once per viewer.
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

    // Weak, so the themes and grammars are released once no extension uses them.
    private static readonly Dictionary<(ThemeName Dark, ThemeName Light), WeakReference<DualThemeTextMateHighlighter>> SharedHighlighters = [];

    // Stryker disable once assignment : lazily caches the highlighter; without the cache every Register() gets the shared instance again, only the lookup is repeated
    private DualThemeTextMateHighlighter GetHighlighter() => _highlighter ??= GetSharedHighlighter(_darkTheme, _lightTheme);

    internal static DualThemeTextMateHighlighter GetSharedHighlighter(ThemeName darkTheme, ThemeName lightTheme)
    {
        lock (SharedHighlighters)
        {
            if (SharedHighlighters.TryGetValue((darkTheme, lightTheme), out var reference) && reference.TryGetTarget(out var shared))
                return shared;
            var highlighter = new DualThemeTextMateHighlighter(darkTheme, lightTheme);
            SharedHighlighters[(darkTheme, lightTheme)] = new WeakReference<DualThemeTextMateHighlighter>(highlighter);
            return highlighter;
        }
    }
}
