// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;
using Avalonia.Media;

using Markdig.Extensions.CustomContainers;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Rendering.Inlines;

public sealed class EmphasisInlineRenderer : AvaloniaObjectRenderer<EmphasisInline>
{
    protected override void Write(AvaloniaRenderer renderer, EmphasisInline obj)
    {
        // inline custom container (::text::)
        var span = obj is CustomContainerInline && obj.DelimiterChar == ':'
            ? new Span { Classes = { "markdown-custom-inline" } }
            : CreateSpan(renderer, obj.DelimiterChar, obj.DelimiterCount);

        renderer.Push(span.Inlines);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteInline(span);
    }

    /// <summary>
    /// The span an emphasis written with <paramref name="delimiterCount"/> <paramref name="delimiterChar"/>
    /// characters renders as. HTML formatting elements render as the same spans (see
    /// <see cref="HtmlElementInlineRenderer"/>), so both syntaxes look and style alike.
    /// </summary>
    internal static Span CreateSpan(AvaloniaRenderer renderer, char delimiterChar, int delimiterCount) => delimiterChar switch
    {
        // bold
        '*' or '_' when delimiterCount == 2 => new Bold(),
        // italic
        '*' or '_' => new Italic(),
        // strikethrough
        '~' when delimiterCount == 2 => new Span
        {
            TextDecorations = TextDecorations.Strikethrough
        },
        // subscript: the font's subscript glyphs, on the subscript baseline
        '~' => new Span
        {
            BaselineAlignment = BaselineAlignment.Subscript,
            FontFeatures = [FontFeature.Parse("subs")],
            Classes = { "markdown-subscript" },
        },
        // superscript
        '^' => new Span
        {
            BaselineAlignment = BaselineAlignment.Superscript,
            FontFeatures = [FontFeature.Parse("sups")],
            Classes = { "markdown-superscript" },
        },
        // underline
        '+' when delimiterCount == 2 => new Underline(),
        // marked
        '=' when delimiterCount == 2 => Themed(renderer, new Span { Classes = { "markdown-marked" } }, "MarkdownMarkedTheme"),
        // citation
        '"' when delimiterCount == 2 => Themed(renderer, new Span { Classes = { "markdown-citation" } }, "MarkdownCitationTheme"),
        _ => new Span(),
    };

    private static Span Themed(AvaloniaRenderer renderer, Span span, string theme)
    {
        renderer.ApplyTheme(span, theme);
        return span;
    }
}
