// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

public class MarkdownViewerDefaultsExtensionsTests
{
    [AvaloniaFact]
    public void AddMermaid_adds_MermaidExtension_to_defaults()
    {
        using var _ = new MarkdownViewerDefaultsMermaidScope();

        MarkdownViewerDefaults.Extensions.AddMermaid();

        Assert.Single(MarkdownViewerDefaults.Extensions.OfType<MermaidExtension>());
    }
}

// Save/restore scope for this test project (self-contained, no dependency on MarkView.Avalonia.Tests)
internal sealed class MarkdownViewerDefaultsMermaidScope : IDisposable
{
    private readonly Markdig.MarkdownPipeline? _savedPipeline;
    private readonly MarkView.Avalonia.Extensions.IMarkViewExtension[] _savedExtensions;

    public MarkdownViewerDefaultsMermaidScope()
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
