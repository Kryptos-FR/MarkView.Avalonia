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
///     .UseSupportedExtensions()
///     .UseAlertBlocks()
///     .Build();
/// MarkdownViewerDefaults.Extensions.Add(new SyntaxHighlightingExtension(theme));
/// </code>
/// </example>
public static class MarkdownViewerDefaults
{
    /// <summary>
    /// Global pipeline used by all <see cref="MarkdownViewer"/> instances that do not set their own
    /// <see cref="MarkdownViewer.Pipeline"/>. If <see langword="null"/>, the built-in default
    /// pipeline (<c>UseSupportedExtensions</c>) is used.
    /// </summary>
    public static MarkdownPipeline? Pipeline { get; set; }

    /// <summary>
    /// Extensions applied to every <see cref="MarkdownViewer"/> render pass, before per-instance
    /// extensions. The same extension object must not appear in both this list and an instance's
    /// <see cref="MarkdownViewer.Extensions"/> list; duplicate references are silently skipped.
    /// </summary>
    public static IList<IMarkViewExtension> Extensions { get; } = new List<IMarkViewExtension>();
}
