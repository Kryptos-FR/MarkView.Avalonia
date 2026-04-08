using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering.Containers;

/// <summary>
/// Wraps a <see cref="Panel"/> to accept block-level child controls.
/// </summary>
internal sealed class BlockContainer(Panel panel) : IContainer
{
    public void Add(Control child) => panel.Children.Add(child);
}
