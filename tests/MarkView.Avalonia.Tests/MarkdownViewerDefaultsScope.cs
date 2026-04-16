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

    public MarkdownViewerDefaultsScope()
    {
        _savedPipeline = MarkdownViewerDefaults.Pipeline;
        _savedExtensions = MarkdownViewerDefaults.Extensions.ToArray();
    }

    public void Dispose()
    {
        MarkdownViewerDefaults.Pipeline = _savedPipeline;
        MarkdownViewerDefaults.Extensions.Clear();
        foreach (var e in _savedExtensions)
            MarkdownViewerDefaults.Extensions.Add(e);
    }
}
