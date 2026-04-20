// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

using Avalonia.Controls.Documents;

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="HtmlInline"/>.
/// Recognizes common self-closing tags (e.g. &lt;br&gt;, &lt;br/&gt;) and renders them
/// as their visual equivalent. Other HTML tags are silently ignored.
/// </summary>
public sealed partial class HtmlInlineRenderer : AvaloniaObjectRenderer<HtmlInline>
{
    [GeneratedRegex(@"^<br\s*/?\s*>$", RegexOptions.IgnoreCase)]
    private static partial Regex BrTagRegex();

    protected override void Write(AvaloniaRenderer renderer, HtmlInline obj)
    {
        var tag = obj.Tag.Trim();

        if (BrTagRegex().IsMatch(tag))
        {
            renderer.WriteInline(new LineBreak());
        }
        // Other HTML tags are silently ignored in a non-HTML context.
    }
}
