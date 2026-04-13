// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extension methods for enabling opt-in Markdig features on a <see cref="MarkdownViewer"/>.
/// Each method rebuilds the pipeline from <see cref="MarkdownExtensions.UseSupportedExtensions"/>
/// plus the requested feature. To combine multiple opt-in features, build the pipeline explicitly:
/// <code>
/// viewer.Pipeline = new MarkdownPipelineBuilder()
///     .UseSupportedExtensions()
///     .UseFootnotes()
///     .UseAlertBlocks()
///     .Build();
/// </code>
/// </summary>
public static class MarkdownViewerExtensions
{
    /// <summary>
    /// Enables footnote rendering on the viewer.
    /// </summary>
    public static MarkdownViewer UseFootnotes(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseSupportedExtensions()
            .UseFootnotes()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Enables GitHub-style alert block rendering on the viewer.
    /// </summary>
    public static MarkdownViewer UseAlertBlocks(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseSupportedExtensions()
            .UseAlertBlocks()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Enables abbreviation tooltip rendering on the viewer.
    /// </summary>
    public static MarkdownViewer UseAbbreviations(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseSupportedExtensions()
            .UseAbbreviations()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Enables figure block rendering on the viewer.
    /// </summary>
    public static MarkdownViewer UseFigures(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseSupportedExtensions()
            .UseFigures()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Enables YouTube thumbnail embed rendering on the viewer.
    /// </summary>
    public static MarkdownViewer UseMediaLinks(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseSupportedExtensions()
            .UseMediaLinks()
            .Build();
        return viewer;
    }
}
