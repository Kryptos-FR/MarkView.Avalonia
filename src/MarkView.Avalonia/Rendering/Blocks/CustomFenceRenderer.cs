// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Logging;

using Markdig.Syntax;

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders the fenced code blocks whose language has a factory in
/// <see cref="AvaloniaRenderer.FenceRenderers"/> as the control that factory returns, and hands
/// every other fenced code block to the renderer that would have written it without this one.
/// <see cref="AvaloniaRenderer"/> places it just before the first renderer of fenced code blocks
/// when it starts rendering, once every extension has installed its own.
/// </summary>
internal sealed class CustomFenceRenderer : AvaloniaObjectRenderer<FencedCodeBlock>
{
    protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        if (obj.Info is { } language
            && renderer.FenceRenderers.TryGetValue(language, out var factory)
            && TryCreate(factory, obj, language) is { } border)
        {
            renderer.WriteBlock(border);
            return;
        }

        WriteWithNextRenderer(renderer, obj);
    }

    // The factory is application code: a failure renders the block as code instead of losing the
    // rest of the document.
    private Border? TryCreate(FenceRendererFactory factory, FencedCodeBlock obj, string language)
    {
        var code = CodeBlockToolbar.GetCode(obj);
        var info = string.IsNullOrEmpty(obj.Arguments) ? language : $"{language} {obj.Arguments}";
        try
        {
            if (factory(code, info) is not { } control)
                return null;

            // Assigning the child throws when the factory returns a control that already has a parent.
            var border = new Border { Child = control };
            border.Classes.Add("markdown-custom-fence");
            border.Classes.Add($"markdown-fence-{language.ToLowerInvariant()}");

            var fence = new string(obj.FencedChar, obj.OpeningFencedCharCount);
            MarkdownSelection.SetCopyText(border, control.IsSet(MarkdownSelection.CopyTextProperty)
                ? MarkdownSelection.GetCopyText(control)
                : $"{fence}{info}\n{code}\n{fence}");
            return border;
        }
        catch (Exception ex)
        {
            // Stryker disable once String : the message text is diagnostic output only
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this,
                "The fence renderer for '{Language}' failed, the block renders as code: {Exception}", language, ex);
            return null;
        }
    }

    private void WriteWithNextRenderer(AvaloniaRenderer renderer, FencedCodeBlock obj)
    {
        var renderers = renderer.ObjectRenderers;
        for (var i = renderers.IndexOf(this) + 1; i < renderers.Count; i++)
        {
            if (renderers[i].Accept(renderer, obj.GetType()))
            {
                renderers[i].Write(renderer, obj);
                return;
            }
        }
    }
}
