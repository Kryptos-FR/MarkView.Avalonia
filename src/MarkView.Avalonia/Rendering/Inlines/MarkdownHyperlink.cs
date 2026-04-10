// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// A <see cref="Span"/> that carries hyperlink metadata for click detection
/// by the parent <see cref="MarkdownSelectableTextBlock"/>.
/// </summary>
public class MarkdownHyperlink : Span
{
    public Uri? NavigateUri { get; set; }
    public string? Title { get; set; }
}
