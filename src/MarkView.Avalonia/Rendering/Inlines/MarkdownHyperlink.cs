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
