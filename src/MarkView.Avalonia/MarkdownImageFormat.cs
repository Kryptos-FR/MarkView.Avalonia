// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia;

/// <summary>A file format an image or diagram of the document can be saved in (see <see cref="MarkdownViewer.SavingImage"/>).</summary>
public enum MarkdownImageFormat
{
    /// <summary>
    /// A PNG picture: a bitmap image's own pixels, or a picture drawn as vectors (a diagram, an SVG image)
    /// rendered at twice its size, on the diagram's background colour.
    /// </summary>
    Png,

    /// <summary>
    /// The SVG document of a picture drawn as vectors: an SVG image's own file, or a Mermaid diagram in the
    /// colours it is shown in, on its background colour.
    /// </summary>
    Svg,
}
