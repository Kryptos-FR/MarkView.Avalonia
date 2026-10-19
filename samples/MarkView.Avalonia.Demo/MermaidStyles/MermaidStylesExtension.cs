// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Mermaid;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Demo.MermaidStyles;

/// <summary>
/// Demo-only: adds a "Styles" button to every Mermaid diagram, which opens
/// <see cref="MermaidStylesDialog"/> to compare the diagram in each <see cref="MermaidStyle"/>.
/// It wraps the <see cref="MermaidBlockRenderer"/> registered before it, so it must be added after
/// <c>AddMermaid()</c>. A viewer with its own <c>UseMermaid()</c> renders its diagrams without the
/// button, as its renderer is inserted ahead of this one.
/// </summary>
public sealed class MermaidStylesExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        var renderers = renderer.ObjectRenderers;
        for (var i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] is MermaidBlockRenderer mermaid)
            {
                renderers[i] = new MermaidStylesButtonRenderer(mermaid);
                return;
            }
        }
    }
}
