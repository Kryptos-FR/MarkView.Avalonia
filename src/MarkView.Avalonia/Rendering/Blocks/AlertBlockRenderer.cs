// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;

using Markdig.Extensions.Alerts;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Parsing;

using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="AlertBlock"/> as a styled bordered panel with a header label.
/// Supports GitHub GFM variants: NOTE, TIP, WARNING, IMPORTANT, CAUTION; any other kind gets the
/// neutral <c>markdown-alert</c> style plus its own <c>markdown-alert-&lt;kind&gt;</c> class.
/// BorderBrush per variant is provided by AXAML theme styles (markdown-alert-note, etc.).
/// With <see cref="MarkdownExtensions.UseExtendedAlertBlocks"/>, an alert can also be foldable
/// (<c>[!KIND]-</c> / <c>[!KIND]+</c>: class <c>markdown-alert-collapsible</c>, see
/// <see cref="CollapsibleAlert"/>) and have a custom title (<c>[!KIND] Title</c>) shown instead of the kind.
/// </summary>
public sealed class AlertBlockRenderer : AvaloniaObjectRenderer<AlertBlock>
{
    protected override void Write(AvaloniaRenderer renderer, AlertBlock obj)
    {
        // obj.Kind is a StringSlice — call ToString() once, then use a lookup
        // to avoid two further allocations (ToLowerInvariant + ToUpperInvariant).
        var kindRaw = obj.Kind.ToString();
        var (kindLower, kindUpper) = kindRaw.ToUpperInvariant() switch
        {
            "NOTE" => ("note", "NOTE"),
            "TIP" => ("tip", "TIP"),
            "WARNING" => ("warning", "WARNING"),
            "IMPORTANT" => ("important", "IMPORTANT"),
            "CAUTION" => ("caution", "CAUTION"),
            var upper => (kindRaw.ToLowerInvariant(), upper),
        };

        var extended = obj as ExtendedAlertBlock;
        var fold = extended?.Fold ?? AlertFold.None;
        // The first paragraph holds what followed the "[!KIND]" line. With a custom title, the title is its
        // first line: its inlines up to the first line break. Without one, it is empty when the marker line
        // stood alone in its paragraph (a blank or nested-quote line follows), and is then left out.
        var firstParagraph = obj.Count > 0 ? obj[0] as ParagraphBlock : null;
        var titleParagraph = extended is { HasTitle: true } ? firstParagraph : null;
        var skipFirstParagraph = titleParagraph is not null || firstParagraph is { Inline.FirstChild: null };

        var header = new TextBlock();
        header.Classes.Add("markdown-alert-header");

        var contentPanel = new StackPanel { Spacing = 4 };
        contentPanel.Classes.Add("markdown-alert-content");

        var outer = new StackPanel { Spacing = 4 };
        outer.Children.Add(header);
        outer.Children.Add(contentPanel);

        var border = new Border { Child = outer };
        border.Classes.Add("markdown-alert");
        border.Classes.Add($"markdown-alert-{kindLower}");

        AlertChevron? chevron = null;
        if (fold != AlertFold.None)
        {
            border.Classes.Add("markdown-alert-collapsible");
            chevron = new AlertChevron();
            chevron.Classes.Add("markdown-alert-chevron");
            header.Inlines!.Add(new InlineUIContainer(chevron));
            header.Inlines.Add(new Run(" "));
        }

        MarkdigInline? bodyStart = null;
        if (titleParagraph?.Inline is { } titleInlines)
        {
            renderer.Push(header.Inlines!);
            var inline = titleInlines.FirstChild;
            for (; inline is not null and not LineBreakInline; inline = inline.NextSibling)
                renderer.Write(inline);
            renderer.Pop();
            bodyStart = inline?.NextSibling;
        }
        else if (chevron is not null)
        {
            header.Inlines!.Add(new Run(kindUpper));
        }
        else
        {
            header.Text = kindUpper;
        }

        renderer.Push(contentPanel);
        // The rest of the title paragraph, if any, is the first paragraph of the body.
        if (bodyStart is not null)
            ParagraphRenderer.Write(renderer, bodyStart);
        for (var i = skipFirstParagraph ? 1 : 0; i < obj.Count; i++)
            renderer.Write(obj[i]);
        renderer.Pop();

        if (chevron is not null)
            CollapsibleAlert.Attach(border, header, chevron, contentPanel, fold == AlertFold.Expanded);

        renderer.WriteBlock(border);
    }
}
