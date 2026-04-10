// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering.Containers;

/// <summary>
/// Wraps a <see cref="Panel"/> to accept block-level child controls.
/// </summary>
internal sealed class BlockContainer(Panel panel) : IContainer
{
    public void Add(Control child) => panel.Children.Add(child);
}
