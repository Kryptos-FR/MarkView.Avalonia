// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.CompilerServices;

using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using Markdig;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class DelimiterInlineRendererTests
{
    [AvaloniaFact]
    public void Delimiter_literal_is_rendered_as_Run()
    {
        var container = RenderInline(CreateLinkDelimiter());

        var run = Assert.IsType<Run>(Assert.Single(container.Inlines!));
        Assert.Equal("[", run.Text);
    }

    [AvaloniaFact]
    public void Delimiter_children_are_rendered_after_literal()
    {
        var delimiter = CreateLinkDelimiter();
        delimiter.AppendChild(new LiteralInline("foo"));

        var container = RenderInline(delimiter);

        var inlines = container.Inlines!.ToList();
        Assert.Equal(2, inlines.Count);
        Assert.Equal("[", Assert.IsType<Run>(inlines[0]).Text);
        Assert.Equal("foo", Assert.IsType<Run>(inlines[1]).Text);
    }

    private static Span RenderInline(Markdig.Syntax.Inlines.Inline inline)
    {
        var renderer = new AvaloniaRenderer();
        var pipeline = new MarkdownPipelineBuilder().Build();
        pipeline.Setup(renderer);

        var container = new Span();
        renderer.Push(container.Inlines);
        renderer.Write(inline);
        renderer.Pop();

        return container;
    }

    private static LinkDelimiterInline CreateLinkDelimiter()
        => (LinkDelimiterInline)RuntimeHelpers.GetUninitializedObject(typeof(LinkDelimiterInline));
}
