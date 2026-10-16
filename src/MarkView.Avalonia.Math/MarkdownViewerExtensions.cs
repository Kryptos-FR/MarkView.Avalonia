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
    /// Enables LaTeX math parsing and rendering on the viewer: <c>$...$</c>/<c>$$...$$</c>, and the bracket
    /// delimiters <c>\(...\)</c>/<c>\[...\]</c> (see <see cref="MarkdownMathExtensions.UseMathBracketDelimiters"/>).
    /// Unlike <c>UseMermaid()</c>/<c>UseSvg()</c>, this also rebuilds the pipeline: the syntax
    /// needs Markdig's <c>UseMathematics()</c> to even be parsed, not just a renderer.
    /// </summary>
    public static MarkdownViewer UseMath(this MarkdownViewer viewer) => viewer.UseMath(bracketDelimiters: true);

    /// <summary>
    /// Enables LaTeX math parsing and rendering on the viewer, as <see cref="UseMath(MarkdownViewer)"/> does.
    /// With <paramref name="bracketDelimiters"/> <see langword="false"/>, only <c>$...$</c>/<c>$$...$$</c> are
    /// math, and <c>\(</c>/<c>\[</c> keep their CommonMark meaning of an escaped parenthesis or bracket.
    /// </summary>
    public static MarkdownViewer UseMath(this MarkdownViewer viewer, bool bracketDelimiters)
    {
        viewer.Extensions.AddMath();
        var builder = new MarkdownPipelineBuilder()
            .UseGitHubExtensions()
            .UseMathematics();
        if (bracketDelimiters)
            builder.UseMathBracketDelimiters();
        viewer.Pipeline = builder.Build();
        return viewer;
    }
}
