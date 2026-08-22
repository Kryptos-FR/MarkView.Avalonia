// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// Registers <see cref="MermaidBlockRenderer"/> so it intercepts <c>```mermaid</c> fences
/// before <c>CodeBlockRenderer</c>. Scans for the first renderer that would accept a
/// <see cref="FencedCodeBlock"/> and inserts just before it, rather than a fixed
/// <c>Insert(0, ...)</c> — that way a narrower, more specific fenced-block renderer
/// (e.g. MarkView.Avalonia.Math's <c>MathBlockRenderer</c>, which only accepts its own
/// <c>MathBlock</c> subtype) registered before or after this one is never displaced from the
/// front of the list, regardless of registration order.
/// </summary>
public sealed class MermaidExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        var insertAt = renderer.ObjectRenderers.Count;
        for (var i = 0; i < renderer.ObjectRenderers.Count; i++)
        {
            if (renderer.ObjectRenderers[i].Accept(renderer, typeof(FencedCodeBlock)))
            {
                insertAt = i;
                break;
            }
        }
        renderer.ObjectRenderers.Insert(insertAt, new MermaidBlockRenderer());
    }
}
