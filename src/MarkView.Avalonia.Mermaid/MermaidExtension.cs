// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// Registers <see cref="MermaidBlockRenderer"/> at the front of the renderer list
/// so it intercepts <c>```mermaid</c> fences before <c>CodeBlockRenderer</c>.
/// </summary>
public sealed class MermaidExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        renderer.ObjectRenderers.Insert(0, new MermaidBlockRenderer());
    }
}
