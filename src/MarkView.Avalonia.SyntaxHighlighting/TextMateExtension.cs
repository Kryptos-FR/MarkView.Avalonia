// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using TextMateSharp.Grammars;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Wires <see cref="TextMateHighlighter"/> and <see cref="TextMateCodeBlockRenderer"/>
/// into an <see cref="AvaloniaRenderer"/>.
/// </summary>
public sealed class TextMateExtension : IMarkViewExtension
{
    private readonly TextMateHighlighter _highlighter;

    public TextMateExtension(ThemeName theme = ThemeName.DarkPlus)
    {
        _highlighter = new TextMateHighlighter(theme);
    }

    public void Register(AvaloniaRenderer renderer)
    {
        renderer.CodeHighlighter = _highlighter;
        renderer.ReplaceOrAdd<CodeBlockRenderer>(new TextMateCodeBlockRenderer());
    }
}
