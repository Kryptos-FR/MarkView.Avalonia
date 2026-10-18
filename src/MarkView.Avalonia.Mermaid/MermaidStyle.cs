// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// The visual style Mermaid diagrams are painted in. Every style draws the same layout in the
/// colours of the <c>MarkdownMermaid*</c> theme resources; only the paint changes.
/// </summary>
public enum MermaidStyle
{
    /// <summary>Tinted boxes with a soft gradient and a light shadow, the accent marking what to look at.</summary>
    Quiet,

    /// <summary>A technical drawing: outlines first, square corners, monospace captions, accent heads and rules.</summary>
    Blueprint,

    /// <summary>Filled tonal blocks without outlines, with generous corner radii and a soft shadow.</summary>
    Tonal,
}
