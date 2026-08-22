// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class MarkdownViewerDefaultsExtensionsTests
{
    [AvaloniaFact]
    public void AddMath_adds_MathExtension_to_defaults()
    {
        using var _ = new MarkdownViewerDefaultsMathScope();

        MarkdownViewerDefaults.Extensions.AddMath();

        Assert.Single(MarkdownViewerDefaults.Extensions.OfType<MathExtension>());
    }
}

// Save/restore scope for this test project (self-contained, no dependency on MarkView.Avalonia.Tests)
internal sealed class MarkdownViewerDefaultsMathScope : IDisposable
{
    private readonly Markdig.MarkdownPipeline? _savedPipeline;
    private readonly MarkView.Avalonia.Extensions.IMarkViewExtension[] _savedExtensions;

    public MarkdownViewerDefaultsMathScope()
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
