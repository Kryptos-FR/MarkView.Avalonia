// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;

namespace MarkView.Avalonia.Rendering;

internal static class VisualBounds
{
    /// <summary>
    /// The rectangle <paramref name="visual"/> covers in the coordinates of <paramref name="target"/>, or
    /// <c>null</c> when they are not in the same visual tree. Unlike the point <c>TranslatePoint</c> maps
    /// (0, 0) to, this is right for a mirrored visual (a right-to-left panel), whose origin is its top-right corner.
    /// </summary>
    public static Rect? BoundsIn(this Visual visual, Visual target) =>
        visual.TransformToVisual(target) is { } transform
            ? new Rect(visual.Bounds.Size).TransformToAABB(transform)
            : null;
}
