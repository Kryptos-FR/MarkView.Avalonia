// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;

using Markdig.Syntax;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Mermaid.Tests;

public class MermaidExtensionTests
{
    private sealed class OtherFencedRenderer : AvaloniaObjectRenderer<FencedCodeBlock>
    {
        protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj) { }
    }

    [AvaloniaFact]
    public void Register_inserts_before_the_first_renderer_that_accepts_fenced_blocks()
    {
        var renderer = new AvaloniaRenderer();
        renderer.ObjectRenderers.Insert(0, new OtherFencedRenderer());

        new MermaidExtension().Register(renderer);

        Assert.IsType<MermaidBlockRenderer>(renderer.ObjectRenderers[0]);
        Assert.IsType<OtherFencedRenderer>(renderer.ObjectRenderers[1]);
    }

    [AvaloniaFact]
    public void Register_appends_when_no_renderer_accepts_fenced_blocks()
    {
        var renderer = new AvaloniaRenderer();
        renderer.ObjectRenderers.RemoveAll(r => r is CodeBlockRenderer);

        new MermaidExtension().Register(renderer);

        Assert.IsType<MermaidBlockRenderer>(renderer.ObjectRenderers[^1]);
    }
}
