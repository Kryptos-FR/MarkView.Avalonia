using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace MarkView.Avalonia.Rendering.Containers;

/// <summary>
/// Wraps an <see cref="InlineCollection"/> to accept inline children.
/// </summary>
internal sealed class InlineContainer(InlineCollection inlines) : IContainer
{
    public void Add(Inline inline) => inlines.Add(inline);

    public void Add(string text) => inlines.Add(new Run(text));

    public void Add(Control control) => inlines.Add(new InlineUIContainer { Child = control });
}
