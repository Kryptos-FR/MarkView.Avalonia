// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

using Markdig.Syntax;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// Renders thematic breaks as a fixed-height, non-text block control whose
/// <see cref="MarkdownSelection.CopyTextProperty"/> is <see cref="CopyText"/>.
/// </summary>
internal sealed class CopyableBreakExtension : IMarkViewExtension
{
    public const string CopyText = "<break>";

    public void Register(AvaloniaRenderer renderer) => renderer.ObjectRenderers.Insert(0, new Renderer());

    private sealed class Renderer : AvaloniaObjectRenderer<ThematicBreakBlock>
    {
        protected override void Write(AvaloniaRenderer renderer, ThematicBreakBlock obj)
        {
            var border = new Border { Height = 20 };
            MarkdownSelection.SetCopyText(border, CopyText);
            renderer.WriteBlock(border);
        }
    }
}
