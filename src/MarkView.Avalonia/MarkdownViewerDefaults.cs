// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia;

/// <summary>
/// Application-wide defaults applied to every <see cref="MarkdownViewer"/> render pass.
/// Set these once at startup (e.g. in <c>App.axaml.cs OnFrameworkInitializationCompleted</c>).
/// </summary>
/// <example>
/// <code>
/// // App.axaml.cs
/// MarkdownViewerDefaults.Pipeline = new MarkdownPipelineBuilder()
///     .UseGitHubExtensions()
///     .UseAbbreviations()
///     .Build();
/// MarkdownViewerDefaults.Extensions.Add(new SyntaxHighlightingExtension(theme));
/// </code>
/// </example>
public static class MarkdownViewerDefaults
{
    /// <summary>
    /// Global pipeline used by all <see cref="MarkdownViewer"/> instances that do not set their own
    /// <see cref="MarkdownViewer.Pipeline"/>. If <see langword="null"/>, the built-in default
    /// pipeline (<c>UseGitHubExtensions</c>) is used.
    /// </summary>
    public static MarkdownPipeline? Pipeline { get; set; }

    /// <summary>
    /// Extensions applied to every <see cref="MarkdownViewer"/> render pass, before per-instance
    /// extensions. The same extension object must not appear in both this list and an instance's
    /// <see cref="MarkdownViewer.Extensions"/> list; duplicate references are silently skipped.
    /// </summary>
    public static IList<IMarkViewExtension> Extensions { get; } = [];

    /// <summary>
    /// The maximum number of loaded images kept in memory and shared by every
    /// <see cref="MarkdownViewer"/>, so that rendering an image again (a document edited and
    /// re-rendered, a viewer recreated) neither downloads nor decodes it again. Images are keyed by
    /// their resolved URL and the image loaders in use; the least recently used one is evicted first.
    /// <c>0</c> disables the cache. Defaults to 100. Lowering it evicts the images beyond the new size.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public static int ImageCacheSize
    {
        get => ImageCache.Shared.Capacity;
        set => ImageCache.Shared.Capacity = value;
    }

    /// <summary>
    /// Removes every image from the cache sized by <see cref="ImageCacheSize"/>, e.g. after the images
    /// behind some URLs have changed. Images already shown stay; the next render loads them again.
    /// </summary>
    public static void ClearImageCache() => ImageCache.Shared.Clear();
}
