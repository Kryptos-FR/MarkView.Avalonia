// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using Xunit;

namespace MarkView.Avalonia.Svg.Tests;

public class MarkdownViewerDefaultsExtensionsTests
{
    [AvaloniaFact]
    public void AddSvg_adds_SvgExtension_to_defaults()
    {
        using var _ = new MarkdownViewerDefaultsSvgScope();

        MarkdownViewerDefaults.Extensions.AddSvg();

        Assert.Single(MarkdownViewerDefaults.Extensions.OfType<SvgExtension>());
    }
}

internal sealed class MarkdownViewerDefaultsSvgScope : IDisposable
{
    private readonly Markdig.MarkdownPipeline? _savedPipeline;
    private readonly MarkView.Avalonia.Extensions.IMarkViewExtension[] _savedExtensions;

    public MarkdownViewerDefaultsSvgScope()
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
