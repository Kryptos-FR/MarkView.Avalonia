// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace MarkView.Avalonia.Rendering.Containers;

/// <summary>
/// Wraps an <see cref="InlineCollection"/> to accept inline children.
/// </summary>
internal sealed class InlineContainer(InlineCollection inlines) : IContainer
{
    // The Run the last plain text went into, which plain text written right after extends.
    private Run? _text;

    public void Add(Inline inline) => inlines.Add(inline);

    /// <summary>
    /// Adds plain text: to the Run of the plain text added just before, if nothing came in between, or
    /// as a new Run.
    /// </summary>
    public void AddText(ReadOnlySpan<char> text)
    {
        if (_text is { } run && inlines.Count > 0 && ReferenceEquals(inlines[^1], run))
            run.Text = string.Concat(run.Text, text);
        else
            inlines.Add(_text = new Run(text.ToString()));
    }

    public void Add(Control control) => inlines.Add(new InlineUIContainer { Child = control });
}
