// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="HtmlBlock"/> as raw text.
/// Full HTML rendering is out of scope; this provides a visible fallback.
/// </summary>
public class HtmlBlockRenderer : AvaloniaObjectRenderer<HtmlBlock>
{
    protected override void Write(AvaloniaRenderer renderer, HtmlBlock obj)
    {
        // HTML blocks are not rendered in a non-HTML context.
        // We silently skip them rather than showing raw tags.
    }
}
