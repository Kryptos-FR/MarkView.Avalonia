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
        var painter = new MathPainter
        {
            LaTeX = latex,
            FontSize = fontSize,
            TextColor = textColor,
            DisplayErrorInline = true, // bad LaTeX renders its own error text instead of throwing
        };

        // Sized as CSharpMath's own DrawAsStream() sizes its surface, but drawn straight into pixels
        // Avalonia copies, without encoding and decoding a PNG.
        var size = painter.Measure().Size;
        var info = new SKImageInfo(System.Math.Max((int)size.Width, 1), System.Math.Max((int)size.Height, 1), SKColorType.Bgra8888, SKAlphaType.Premul);
        using var pixels = new SKBitmap(info);
        using (var canvas = new SKCanvas(pixels))
        {
            canvas.Clear(SKColors.Transparent);
            painter.Draw(canvas, TextAlignment.TopLeft);
        }
        return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, pixels.GetPixels(),
            new PixelSize(info.Width, info.Height), new Vector(96, 96), pixels.RowBytes);
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
