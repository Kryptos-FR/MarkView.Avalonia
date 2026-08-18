// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Demo.ColorExtension;

/// <summary>
/// MarkView-side half of the colour span extension: registers <see cref="ColorSpanRenderer"/>
/// so a <see cref="ColorSpanInline"/> node (produced by <see cref="ColorMarkdownExtension"/>) renders.
/// </summary>
public sealed class ColorSpanExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        renderer.ObjectRenderers.Add(new ColorSpanRenderer());
    }
}
