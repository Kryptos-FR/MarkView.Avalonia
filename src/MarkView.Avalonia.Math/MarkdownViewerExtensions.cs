// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extensions for attaching LaTeX math rendering to a <see cref="MarkdownViewer"/>.
/// </summary>
public static class MarkdownViewerMathExtensions
{
    /// <summary>
    /// Adds <see cref="Math.MathExtension"/> to the extension list.
    /// </summary>
    /// <example>
    /// <code>
    /// // Global (App.axaml.cs)
    /// MarkdownViewerDefaults.Extensions.AddMath();
    /// // Per-instance
    /// viewer.Extensions.AddMath();
    /// </code>
    /// </example>
    public static void AddMath(this IList<IMarkViewExtension> extensions)
    {
        extensions.Add(new Math.MathExtension());
    }

    /// <summary>
    /// Enables <c>$...$</c>/<c>$$...$$</c> LaTeX math parsing and rendering on the viewer.
    /// Unlike <c>UseMermaid()</c>/<c>UseSvg()</c>, this also rebuilds the pipeline: the syntax
    /// needs Markdig's <c>UseMathematics()</c> to even be parsed, not just a renderer.
    /// </summary>
    public static MarkdownViewer UseMath(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseSupportedExtensions()
            .UseMathematics()
            .Build();
        viewer.Extensions.AddMath();
        return viewer;
    }
}
