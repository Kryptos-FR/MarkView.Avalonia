// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Svg;

/// <summary>
/// Registers <see cref="SvgImageLoader"/> at the front of the image loader chain.
/// </summary>
public sealed class SvgExtension : IMarkViewExtension
{
    public void Register(AvaloniaRenderer renderer)
    {
        renderer.ImageLoaders.Insert(0, new SvgImageLoader());
    }
}
