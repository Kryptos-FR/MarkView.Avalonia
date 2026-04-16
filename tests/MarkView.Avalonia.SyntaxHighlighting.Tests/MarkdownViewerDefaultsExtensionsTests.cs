// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using TextMateSharp.Grammars;
using Xunit;

namespace MarkView.Avalonia.SyntaxHighlighting.Tests;

public class MarkdownViewerDefaultsExtensionsTests
{
    [AvaloniaFact]
    public void AddTextMateHighlighting_adds_TextMateExtension_to_defaults()
    {
        using var _ = new MarkdownViewerDefaultsSyntaxScope();

        MarkdownViewerDefaults.Extensions.AddTextMateHighlighting();

        Assert.Single(MarkdownViewerDefaults.Extensions.OfType<TextMateExtension>());
    }

    [AvaloniaFact]
    public void AddTextMateHighlighting_with_custom_themes_adds_TextMateExtension()
    {
        using var _ = new MarkdownViewerDefaultsSyntaxScope();

        MarkdownViewerDefaults.Extensions.AddTextMateHighlighting(
            ThemeName.DimmedMonokai, ThemeName.QuietLight);

        Assert.Single(MarkdownViewerDefaults.Extensions.OfType<TextMateExtension>());
    }
}

// Save/restore scope for this test project (self-contained, no dependency on MarkView.Avalonia.Tests)
internal sealed class MarkdownViewerDefaultsSyntaxScope : IDisposable
{
    private readonly Markdig.MarkdownPipeline? _savedPipeline;
    private readonly MarkView.Avalonia.Extensions.IMarkViewExtension[] _savedExtensions;

    public MarkdownViewerDefaultsSyntaxScope()
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
