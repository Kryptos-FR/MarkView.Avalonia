// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

using Markdig.Extensions.Mathematics;

using MarkView.Avalonia.Math.Parsing;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Renders Markdig <see cref="MathInline"/> nodes (<c>$...$</c>, and <c>\(...\)</c> or an in-line
/// <c>\[...\]</c> once <c>UseMathBracketDelimiters()</c> is enabled) as an inline image
/// produced by CSharpMath.SkiaSharp, once it comes near the viewport when
/// <see cref="AvaloniaRenderer.IsLazyRenderingEnabled"/> is set.
/// </summary>
public sealed class MathInlineRenderer : AvaloniaObjectRenderer<MathInline>
{
    protected override void Write(AvaloniaRenderer renderer, MathInline obj)
    {
        // A formula spanning lines inside a paragraph keeps the document's line endings.
        var source = obj.Content.ToString().ReplaceLineEndings("\n");

        var image = new Image { Stretch = Stretch.None };
        image.Classes.Add("markdown-math-inline");
        AutomationProperties.SetName(image, source);
        MarkdownSelection.SetCopyText(image, obj switch
        {
            BracketMathInline { IsDisplay: true } => $"\\[{source}\\]",
            BracketMathInline => $"\\({source}\\)",
            _ => $"${source}$",
        });

        if (renderer.IsLazyRenderingEnabled)
        {
            // Only the bitmap waits until the formula comes within a screen of the viewport. A formula
            // over CSharpMath's safety limits still becomes plain text now, while it can be written.
            if (!IsSafeToRender(source))
            {
                renderer.WriteInline(new Run(source));
                return;
            }

            var rendered = false;
            ThemeTracking.ReapplyOnThemeChange(image, () =>
            {
                if (rendered) TryApplyTheme();
            });
            // Past the safety limits, CSharpMath draws bad LaTeX as its own error text instead of throwing.
            ViewportTracking.RunWhenNearViewport(image, () =>
            {
                rendered = true;
                TryApplyTheme();
            });
            renderer.WriteInline(image);
            return;
        }

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

    private static bool IsSafeToRender(string source)
    {
        try
        {
            MathFormulaRenderer.EnsureSafeToRender(source);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
