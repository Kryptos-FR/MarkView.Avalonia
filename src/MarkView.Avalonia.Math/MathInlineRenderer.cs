// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

using Markdig.Extensions.Mathematics;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Renders Markdig <see cref="MathInline"/> nodes (<c>$...$</c>) as an inline image
/// produced by CSharpMath.SkiaSharp.
/// </summary>
public sealed class MathInlineRenderer : AvaloniaObjectRenderer<MathInline>
{
    protected override void Write(AvaloniaRenderer renderer, MathInline obj)
    {
        var source = obj.Content.ToString();

        var image = new Image { Stretch = Stretch.None };
        image.Classes.Add("markdown-math-inline");

        if (!TryApplyTheme())
        {
            // First render failed before the image was ever written to the renderer's inline
            // stack — we're still inside the synchronous initial Write() call here, so writing a
            // plain-text fallback instead is safe (unlike from the later theme-change callback,
            // which must never re-enter the stack — see the comment inside TryApplyTheme below).
            renderer.WriteInline(new Run(source));
            return;
        }

        ThemeTracking.ReapplyOnThemeChange(image, () => TryApplyTheme());

        renderer.WriteInline(image);

        bool TryApplyTheme()
        {
            try
            {
                image.Source = MathFormulaRenderer.Render(source, MathFormulaRenderer.GetThemeTextColor());
                return true;
            }
            catch (Exception)
            {
                // The caller decides how to react: the first render writes a plain-text fallback,
                // while the theme-change callback ignores the failure. That callback can't safely
                // re-enter the renderer's inline stack — unlike the block renderer's Border, an
                // inline Image has no room for a fallback panel — so the image keeps showing its
                // last successfully rendered bitmap rather than risk corrupting whatever inline
                // collection is active at callback time.
                return false;
            }
        }
    }
}
