// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Net;

using Avalonia.Controls.Documents;

using Markdig.Extensions.SmartyPants;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="SmartyPant"/> (Markdig's <c>UseSmartyPants()</c>) as the typographic
/// character it stands for: curly quotes, en and em dashes, ellipsis and guillemets.
/// </summary>
public sealed class SmartyPantInlineRenderer : AvaloniaObjectRenderer<SmartyPant>
{
    /// <summary>
    /// The replacement for each kind of punctuation. Its values are HTML entities or plain text, as in
    /// Markdig's own options. <see cref="MarkdownViewer"/> sets it from the pipeline's
    /// <see cref="SmartyPantsExtension"/>, so custom replacements (for example French guillemets
    /// for double quotes) apply here too.
    /// </summary>
    public SmartyPantOptions Options { get; set; } = new();

    protected override void Write(AvaloniaRenderer renderer, SmartyPant obj)
    {
        var text = Options.Mapping.TryGetValue(obj.Type, out var replacement)
            ? WebUtility.HtmlDecode(replacement)
            : obj.ToString();
        renderer.WriteText(text);
    }
}
