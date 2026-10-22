// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Media;

using Markdig.Syntax;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

namespace MarkView.Avalonia.SyntaxHighlighting;

/// <summary>
/// Replaces the built-in <see cref="CodeBlockRenderer"/> when syntax highlighting is active.
/// For fenced code blocks it highlights each line with the <see cref="AvaloniaRenderer.CodeHighlighter"/>
/// and, when that highlighter is an <see cref="IDualThemeCodeHighlighter"/>, recolours the tokens
/// in-place when the user switches between light and dark themes — without tokenising the code
/// again or rebuilding the surrounding document tree.
/// </summary>
public sealed class TextMateCodeBlockRenderer : AvaloniaObjectRenderer<CodeBlock>
{
    protected override void Write(AvaloniaRenderer renderer, CodeBlock obj)
    {
        if (GrowingCodeBlock.TryWrite(renderer, obj, renderer.CodeHighlighter))
            return;

        var language = obj is FencedCodeBlock fenced ? fenced.Info : null;

        var textBlock = new TextBlock { TextWrapping = TextWrapping.NoWrap };
        var border = new Border { Child = textBlock };
        border.Classes.Add("markdown-code-block");

        if (!string.IsNullOrEmpty(language))
            border.Classes.Add($"language-{language}");

        HighlightedCode.Write(renderer, border, textBlock, obj, language);

        CodeBlockToolbar.Write(renderer, border, obj, language);
    }
}
