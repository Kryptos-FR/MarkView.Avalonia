// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A <see cref="TextBlock"/> whose inlines get their styles back after the application's styles change.
/// Styled as a plain <see cref="TextBlock"/>, so <c>TextBlock.markdown-*</c> selectors still match it.
/// </summary>
/// <remarks>
/// When styles are added to or removed from a parent (switching a theme preset, for instance), Avalonia
/// removes the styled values of every element below it, inlines included, and leaves each control to
/// apply its styles again at its next measure. Inlines are never measured, so a <c>Run</c> styled as
/// inline code or a link would keep none of its style. Measuring the text block applies them first.
/// </remarks>
internal sealed class StyledInlinesTextBlock : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override Size MeasureOverride(Size availableSize)
    {
        ApplyInlineStyling(Inlines);
        return base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// Applies the styles of <paramref name="inlines"/> and of the inlines nested in their spans, where
    /// they are not applied yet. Call it before the text layout is built, i.e. before measuring.
    /// </summary>
    internal static void ApplyInlineStyling(InlineCollection? inlines)
    {
        if (inlines is null) return;
        foreach (var inline in inlines)
        {
            inline.ApplyStyling();
            if (inline is Span span)
                ApplyInlineStyling(span.Inlines);
        }
    }
}
