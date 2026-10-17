// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

using MarkView.Avalonia.Parsing;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders an inline HTML formatting element (<see cref="HtmlElementInline"/>) like its markdown counterpart:
/// <c>&lt;b&gt;</c> as <see cref="Bold"/>, <c>&lt;i&gt;</c> as <see cref="Italic"/>, <c>&lt;del&gt;</c> struck
/// through, <c>&lt;ins&gt;</c> as <see cref="Underline"/>, <c>&lt;mark&gt;</c> as a <c>markdown-marked</c> span;
/// <c>&lt;sub&gt;</c> and <c>&lt;sup&gt;</c> as <c>markdown-html-sub</c> / <c>markdown-html-sup</c> spans on the
/// subscript or superscript baseline; and <c>&lt;kbd&gt;</c> as a key (<see cref="HtmlKeyboardKey"/>).
/// </summary>
internal sealed class HtmlElementInlineRenderer : AvaloniaObjectRenderer<HtmlElementInline>
{
    protected override void Write(AvaloniaRenderer renderer, HtmlElementInline obj)
    {
        if (obj.Kind == HtmlElementKind.Keyboard)
        {
            WriteKey(renderer, obj);
            return;
        }

        Span span = obj.Kind switch
        {
            HtmlElementKind.Bold => new Bold(),
            HtmlElementKind.Italic => new Italic(),
            HtmlElementKind.Strikethrough => new Span { TextDecorations = TextDecorations.Strikethrough },
            HtmlElementKind.Underline => new Underline(),
            HtmlElementKind.Mark => new Span { Classes = { "markdown-marked" } },
            HtmlElementKind.Subscript => new Span
            {
                BaselineAlignment = BaselineAlignment.Subscript,
                Classes = { "markdown-html-sub" },
            },
            _ => new Span
            {
                BaselineAlignment = BaselineAlignment.Superscript,
                Classes = { "markdown-html-sup" },
            },
        };

        renderer.Push(span.Inlines);
        renderer.WriteChildren(obj);
        renderer.Pop();

        renderer.WriteInline(span);
    }

    /// <summary>
    /// Writes a <c>&lt;kbd&gt;</c> element as a <c>markdown-html-kbd</c> key around a <see cref="TextBlock"/> of its
    /// content. Selection takes the key as a whole and copies its text.
    /// </summary>
    private static void WriteKey(AvaloniaRenderer renderer, HtmlElementInline obj)
    {
        var text = new TextBlock();
        renderer.Push(text.Inlines!);
        renderer.WriteChildren(obj);
        renderer.Pop();

        var key = new HtmlKeyboardKey { Child = text };
        key.Classes.Add("markdown-html-kbd");
        MarkdownSelection.SetCopyText(key, MarkdownSelectableTextBlock.ExtractPlainText(text.Inlines!));
        renderer.WriteInline(key);
    }
}
