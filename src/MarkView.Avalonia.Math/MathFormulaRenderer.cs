// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;

using CSharpMath.Rendering.FrontEnd;
using CSharpMath.SkiaSharp;

using SkiaSharp;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Renders a LaTeX formula to a bitmap via CSharpMath.SkiaSharp. Shared by
/// <see cref="MathBlockRenderer"/> and <see cref="MathInlineRenderer"/>.
/// </summary>
internal static class MathFormulaRenderer
{
    private const int MaxLatexLength = 10_000;
    private const int MaxBraceNestingDepth = 50;

    /// <summary>
    /// Returns the bitmap of <paramref name="latex"/> in <paramref name="textColor"/>, from
    /// <see cref="FormulaCache.Shared"/> when it was rendered recently.
    /// </summary>
    public static Bitmap Render(string latex, SKColor textColor, float fontSize = 16f)
    {
        EnsureSafeToRender(latex);
        return FormulaCache.Shared.GetOrAdd(latex, textColor, fontSize, () => Bake(latex, textColor, fontSize));
    }

    /// <summary>Typesets <paramref name="latex"/> and draws it into a new bitmap.</summary>
    internal static Bitmap Bake(string latex, SKColor textColor, float fontSize)
    {
        using var pixels = Draw(latex, textColor, fontSize);
        return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, pixels.GetPixels(),
            new PixelSize(pixels.Width, pixels.Height), new Vector(96, 96), pixels.RowBytes);
    }

    /// <summary>
    /// Typesets <paramref name="latex"/> and draws it into BGRA premultiplied pixels, sized as CSharpMath's
    /// own <c>DrawAsStream()</c> sizes its surface, so that <see cref="Bake"/> needs no PNG encoding and decoding.
    /// </summary>
    internal static SKBitmap Draw(string latex, SKColor textColor, float fontSize)
    {
        var painter = new MathPainter
        {
            LaTeX = latex,
            FontSize = fontSize,
            TextColor = textColor,
            DisplayErrorInline = true, // bad LaTeX renders its own error text instead of throwing
        };

        var size = painter.Measure().Size;
        var pixels = new SKBitmap(new SKImageInfo(
            System.Math.Max((int)size.Width, 1), System.Math.Max((int)size.Height, 1), SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(pixels);
        // Stryker disable once Statement: a new SKBitmap is already zeroed; cleared in case that changes
        canvas.Clear(SKColors.Transparent);
        painter.Draw(canvas, TextAlignment.TopLeft);
        return pixels;
    }

    /// <summary>
    /// CSharpMath.SkiaSharp's typesetter recurses per nesting level (confirmed: deeply nested
    /// \frac/\sqrt overflow the stack). A StackOverflowException there is uncatchable and kills
    /// the whole host process, not just this control — and MarkdownViewer.Source accepts http(s)
    /// URLs, so rendering untrusted/remote markdown is a supported scenario. Reject pathological
    /// input before it ever reaches CSharpMath, so the existing (catchable) fallback path handles
    /// it instead.
    /// </summary>
    internal static void EnsureSafeToRender(string latex)
    {
        if (latex.Length > MaxLatexLength)
            throw new InvalidOperationException($"LaTeX source exceeds the {MaxLatexLength}-character safety limit.");

        int depth = 0, maxDepth = 0;
        foreach (var c in latex)
        {
            if (c == '{') { depth++; maxDepth = System.Math.Max(maxDepth, depth); }
            else if (c == '}') { depth--; }
        }
        if (maxDepth > MaxBraceNestingDepth)
            throw new InvalidOperationException($"LaTeX source exceeds the {MaxBraceNestingDepth}-level brace-nesting safety limit.");
    }

    /// <summary>
    /// Matches MarkView.Avalonia.Mermaid's exact two-hardcoded-hex-per-theme convention.
    /// </summary>
    public static SKColor GetThemeTextColor() =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Dark
            ? SKColor.Parse("#FAFAFA")
            : SKColor.Parse("#27272A");
}
