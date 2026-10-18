// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;

namespace MarkView.Avalonia;

/// <summary>
/// Convenience extension methods for enabling opt-in Markdig features on a <see cref="MarkdownViewer"/>.
/// Each method rebuilds the pipeline from <see cref="MarkdownExtensions.UseGitHubExtensions"/> (the
/// viewer's default pipeline) plus the requested feature. To combine multiple opt-in features, build
/// the pipeline explicitly:
/// <code>
/// viewer.Pipeline = new MarkdownPipelineBuilder()
///     .UseGitHubExtensions()
///     .UseAbbreviations()
///     .UseFigures()
///     .Build();
/// </code>
/// </summary>
public static class MarkdownViewerExtensions
{
    /// <summary>
    /// Enables abbreviation tooltip rendering on the viewer.
    /// </summary>
    public static MarkdownViewer UseAbbreviations(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseGitHubExtensions()
            .UseAbbreviations()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Enables the alert extras on the viewer: foldable alerts and custom titles. GitHub-style alerts
    /// themselves are already part of the default pipeline.
    /// </summary>
    public static MarkdownViewer UseAlertBlocks(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseGitHubExtensions()
            .UseExtendedAlertBlocks()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Enables citation rendering (<c>""quoted text""</c>) on the viewer.
    /// </summary>
    public static MarkdownViewer UseCitations(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseGitHubExtensions()
            .UseCitations()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Enables figure block rendering on the viewer.
    /// </summary>
    public static MarkdownViewer UseFigures(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseGitHubExtensions()
            .UseFigures()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Enables footnote rendering on the viewer. Footnotes are already part of the default pipeline, so
    /// this only matters to restore the default after another pipeline was set.
    /// </summary>
    public static MarkdownViewer UseFootnotes(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseGitHubExtensions()
            .UseFootnotes()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Enables hardline-break rendering on the viewer: every soft line break renders as an
    /// explicit line break instead of collapsing to a space.
    /// </summary>
    public static MarkdownViewer UseHardlineBreaks(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseGitHubExtensions()
            .UseHardlineBreaks()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Enables YouTube thumbnail embed rendering on the viewer.
    /// </summary>
    public static MarkdownViewer UseMediaLinks(this MarkdownViewer viewer)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseGitHubExtensions()
            .UseMediaLinks()
            .Build();
        return viewer;
    }

    /// <summary>
    /// Shows a language model's reasoning (<c>&lt;think&gt;…&lt;/think&gt;</c>) in a block that folds away, as
    /// <see cref="MarkdownExtensions.UseThinkingBlocks"/> describes.
    /// </summary>
    /// <param name="viewer">The viewer.</param>
    /// <param name="options">The tags, titles and fold behaviour; the defaults when <c>null</c>.</param>
    public static MarkdownViewer UseThinkingBlocks(this MarkdownViewer viewer, ThinkingBlockOptions? options = null)
    {
        viewer.Pipeline = new MarkdownPipelineBuilder()
            .UseGitHubExtensions()
            .UseThinkingBlocks(options)
            .Build();
        return viewer;
    }
}
