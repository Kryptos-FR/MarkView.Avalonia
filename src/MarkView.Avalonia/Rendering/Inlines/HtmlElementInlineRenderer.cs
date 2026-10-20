// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

using MarkView.Avalonia.Parsing;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders an inline HTML formatting element (<see cref="HtmlElementInline"/>) like its markdown counterpart:
/// <c>&lt;b&gt;</c> as <see cref="Bold"/>, <c>&lt;i&gt;</c> as <see cref="Italic"/>, <c>&lt;del&gt;</c> struck
/// through, <c>&lt;ins&gt;</c> as <see cref="Underline"/>, <c>&lt;mark&gt;</c> as a <c>markdown-marked</c> span;
/// <c>&lt;sub&gt;</c> and <c>&lt;sup&gt;</c> as <c>~sub~</c> and <c>^sup^</c> are; and <c>&lt;kbd&gt;</c> as a key (<see cref="HtmlKeyboardKey"/>).
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

        // An alias of the markdown emphasis it stands for, so both render and style alike.
        var span = obj.Kind switch
        {
            HtmlElementKind.Bold => EmphasisInlineRenderer.CreateSpan(renderer, '*', 2),
            HtmlElementKind.Italic => EmphasisInlineRenderer.CreateSpan(renderer, '*', 1),
            HtmlElementKind.Strikethrough => EmphasisInlineRenderer.CreateSpan(renderer, '~', 2),
            HtmlElementKind.Underline => EmphasisInlineRenderer.CreateSpan(renderer, '+', 2),
            HtmlElementKind.Mark => EmphasisInlineRenderer.CreateSpan(renderer, '=', 2),
            HtmlElementKind.Subscript => EmphasisInlineRenderer.CreateSpan(renderer, '~', 1),
            _ => EmphasisInlineRenderer.CreateSpan(renderer, '^', 1),
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
        renderer.ApplyTheme(key, "MarkdownKbdTheme");
        renderer.ApplyTheme(text, "MarkdownKbdTextTheme");
        var plainText = MarkdownSelectableTextBlock.ExtractPlainText(text.Inlines!);
        MarkdownSelection.SetCopyText(key, plainText);
        // Read as the key's text, once: the text block inside it is hidden.
        AutomationRoles.Set(key, AutomationControlType.Text);
        AutomationProperties.SetName(key, plainText);
        AutomationRoles.Hide(text);
        renderer.WriteInline(key);
    }
}
