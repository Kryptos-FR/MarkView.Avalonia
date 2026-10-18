// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

using Avalonia.Controls.Documents;
using Avalonia.Logging;

using Markdig.Syntax.Inlines;

using AvaloniaInline = Avalonia.Controls.Documents.Inline;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="LiteralInline"/> as an Avalonia <see cref="Run"/>. Where one of the
/// renderer's <see cref="AvaloniaRenderer.InlinePatterns"/> matches the text, outside a link, the match
/// is written as the inline the pattern creates instead.
/// </summary>
public sealed class LiteralInlineRenderer : AvaloniaObjectRenderer<LiteralInline>
{
    protected override void Write(AvaloniaRenderer renderer, LiteralInline obj)
    {
        if (obj.Content.IsEmpty)
            return;

        var text = obj.Content.ToString();
        if (renderer.InlinePatterns.Count == 0 || IsInsideLink(obj))
        {
            renderer.WriteInline(new Run(text));
            return;
        }

        WriteWithPatterns(renderer, text);
    }

    private void WriteWithPatterns(AvaloniaRenderer renderer, string text)
    {
        var patterns = renderer.InlinePatterns;
        // Each pattern's next match (an unsuccessful one once it has none left), searched again only
        // once the text before it has been written.
        var next = new Match?[patterns.Count];
        var position = 0;
        while (position < text.Length)
        {
            // The match that starts first wins; the pattern registered last wins a tie, so a viewer's own
            // pattern overrides a global one.
            Match? first = null;
            InlinePattern? owner = null;
            for (var i = 0; i < patterns.Count; i++)
            {
                if (next[i] is not { } match || (match.Success && match.Index < position))
                    next[i] = match = NextMatch(patterns[i].Regex, text, position);
                if (match.Success && (first is null || match.Index <= first.Index))
                {
                    first = match;
                    owner = patterns[i];
                }
            }
            if (first is null)
                break;

            if (first.Index > position)
                renderer.WriteInline(new Run(text[position..first.Index]));
            renderer.WriteInline(Create(renderer, owner!, first) ?? new Run(first.Value));
            position = first.Index + first.Length;
        }

        if (position < text.Length)
            renderer.WriteInline(new Run(text[position..]));
    }

    /// <summary>
    /// The first non-empty match of <paramref name="regex"/> at or after <paramref name="start"/>, or an
    /// unsuccessful match when there is none.
    /// </summary>
    private static Match NextMatch(Regex regex, string text, int start)
    {
        var match = regex.Match(text, start);
        while (match.Success && match.Length == 0)
            match = match.NextMatch();
        return match;
    }

    // The factory is application code: a failure keeps the matched text instead of losing the rest
    // of the document.
    private AvaloniaInline? Create(AvaloniaRenderer renderer, InlinePattern pattern, Match match)
    {
        try
        {
            var inline = pattern.Create(renderer, match);
            // A control copies the text it stands for, unless it says otherwise.
            if (inline is InlineUIContainer { Child: { } child } && !child.IsSet(MarkdownSelection.CopyTextProperty))
                MarkdownSelection.SetCopyText(child, match.Value);
            return inline;
        }
        catch (Exception ex)
        {
            // Stryker disable once String : the message text is diagnostic output only
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this,
                "The inline pattern '{Pattern}' failed on '{Text}', the text is kept: {Exception}", pattern.Regex.ToString(), match.Value, ex);
            return null;
        }
    }

    private static bool IsInsideLink(LiteralInline literal)
    {
        for (var parent = literal.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is LinkInline)
                return true;
        }
        return false;
    }
}
