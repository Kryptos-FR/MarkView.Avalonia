// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

using AvaloniaInline = Avalonia.Controls.Documents.Inline;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A pattern the renderer looks for in the document's text, and what it writes for each match
/// (<see langword="null"/> to keep the text). Registered by <see cref="Extensions.InlinePatternExtension"/>
/// and <see cref="Extensions.LinkPatternExtension"/>.
/// </summary>
internal sealed record InlinePattern(Regex Regex, Func<AvaloniaRenderer, Match, AvaloniaInline?> Create);
