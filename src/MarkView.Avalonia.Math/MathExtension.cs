// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Extensions.Mathematics;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Registers <see cref="MathBlockRenderer"/> and <see cref="MathInlineRenderer"/>.
/// <see cref="MathBlock"/> extends Markdig's <c>FencedCodeBlock</c>, the same type
/// <c>MermaidBlockRenderer</c> broadly accepts — unlike a plain <c>Insert(0, ...)</c>
/// (which would make whichever extension registers *last* win), this scans for the first
/// renderer that would accept a <see cref="MathBlock"/> and inserts just before it, so
/// <c>$$...$$</c> blocks are never swallowed by another extension regardless of the order
/// <c>.UseMermaid()</c>/<c>.UseMath()</c> (or their <c>IMarkViewExtension</c> equivalents) were
/// registered in.
/// </summary>
public sealed class MathExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        var insertAt = renderer.ObjectRenderers.Count;
        for (var i = 0; i < renderer.ObjectRenderers.Count; i++)
        {
            if (renderer.ObjectRenderers[i].Accept(renderer, typeof(MathBlock)))
            {
                insertAt = i;
                break;
            }
        }
        renderer.ObjectRenderers.Insert(insertAt, new MathBlockRenderer());
        renderer.ObjectRenderers.Add(new MathInlineRenderer()); // plain LeafInline — no precedence conflict
    }
}
