// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

using CSharpMath.SkiaSharp;

using SkiaSharp;

namespace MarkView.Avalonia.Math;

/// <summary>
/// Renders a LaTeX formula to a bitmap via CSharpMath.SkiaSharp. Shared by
/// <see cref="MathBlockRenderer"/> and <see cref="MathInlineRenderer"/>.
/// </summary>
internal static class MathFormulaRenderer
{
    public static Bitmap Render(string latex, SKColor textColor, float fontSize = 16f)
    {
        var painter = new MathPainter
        {
            LaTeX = latex,
            FontSize = fontSize,
            TextColor = textColor,
            DisplayErrorInline = true, // bad LaTeX renders its own error text instead of throwing
        };

        using var stream = painter.DrawAsStream()
            ?? throw new InvalidOperationException("CSharpMath failed to render the formula to a stream.");
        return new Bitmap(stream);
    }

    /// <summary>
    /// Matches MarkView.Avalonia.Mermaid's exact two-hardcoded-hex-per-theme convention.
    /// </summary>
    public static SKColor GetThemeTextColor() =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Dark
            ? SKColor.Parse("#FAFAFA")
            : SKColor.Parse("#27272A");
}
