// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Demo.ColorExtension;

/// <summary>
/// A leaf inline produced by <see cref="ColorSpanParser"/> for <c>%[color:NAME]text%</c> spans.
/// Content is literal — like <see cref="CodeInline"/>, it does not support nested markdown.
/// </summary>
public sealed class ColorSpanInline : LeafInline
{
    public string Color { get; }

    public string Content { get; }

    public ColorSpanInline(string color, string content)
    {
        Color = color;
        Content = content;
    }
}
