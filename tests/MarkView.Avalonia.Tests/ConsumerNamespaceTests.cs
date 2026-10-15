// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using MarkView.Avalonia;
using Xunit;

// Deliberately outside the MarkView.Avalonia namespace, like application code: inside it, the
// library's extension methods would win over Markdig's and hide a name clash between the two.
namespace MarkViewConsumer;

public class ConsumerNamespaceTests
{
    [Fact]
    public void Pipeline_methods_compile_with_both_namespaces_imported()
    {
        // Each call would fail to compile (CS0121) if MarkView.Avalonia defined a method with
        // the same name as Markdig's.
        var pipeline = new MarkdownPipelineBuilder()
            .UseSupportedExtensions()
            .UseHardlineBreaks()
            .UseAbbreviations()
            .UseAlertBlocks()
            .UseCitations()
            .UseFigures()
            .UseFootnotes()
            .UseMediaLinks()
            .Build();

        Assert.Contains(pipeline.Extensions, e => e is Markdig.Extensions.Footnotes.FootnoteExtension);
        Assert.Contains(pipeline.Extensions, e => e is Markdig.Extensions.Alerts.AlertExtension);
    }
}
