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
    IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(string line, string? language);
}
