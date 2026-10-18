// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia.Tests;

/// <summary>
/// Saves and restores <see cref="MarkdownViewerDefaults"/> state around a test,
/// preventing cross-test pollution from the global mutable statics.
/// Usage: <c>using var _ = new MarkdownViewerDefaultsScope();</c>
/// </summary>
internal sealed class MarkdownViewerDefaultsScope : IDisposable
{
    private readonly Markdig.MarkdownPipeline? _savedPipeline;
    private readonly MarkView.Avalonia.Extensions.IMarkViewExtension[] _savedExtensions;
    private readonly MarkView.Avalonia.Extensions.IResourceResolver? _savedResourceResolver;
    private readonly MarkdownUrlTransform? _savedUrlTransform;
    private readonly string[] _savedLaunchableLinkSchemes;

    public MarkdownViewerDefaultsScope()
    {
        _savedPipeline = MarkdownViewerDefaults.Pipeline;
        _savedExtensions = MarkdownViewerDefaults.Extensions.ToArray();
        _savedResourceResolver = MarkdownViewerDefaults.ResourceResolver;
        _savedUrlTransform = MarkdownViewerDefaults.UrlTransform;
        _savedLaunchableLinkSchemes = [.. MarkdownViewerDefaults.LaunchableLinkSchemes];
    }

    public void Dispose()
    {
        MarkdownViewerDefaults.Pipeline = _savedPipeline;
        MarkdownViewerDefaults.ResourceResolver = _savedResourceResolver;
        MarkdownViewerDefaults.UrlTransform = _savedUrlTransform;
        MarkdownViewerDefaults.LaunchableLinkSchemes.Clear();
        foreach (var scheme in _savedLaunchableLinkSchemes)
            MarkdownViewerDefaults.LaunchableLinkSchemes.Add(scheme);
        MarkdownViewerDefaults.Extensions.Clear();
        foreach (var e in _savedExtensions)
            MarkdownViewerDefaults.Extensions.Add(e);
    }
}
