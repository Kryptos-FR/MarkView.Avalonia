// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

using MarkView.Avalonia.Tests.Shared;

using CSharpMath.SkiaSharp;

using SkiaSharp;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class MathFormulaRendererTests
{
    [Fact]
    public void EnsureSafeToRender_accepts_source_at_the_length_limit() =>
        MathFormulaRenderer.EnsureSafeToRender(new string('x', 10_000));

    [Fact]
    public void EnsureSafeToRender_rejects_source_over_the_length_limit()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MathFormulaRenderer.EnsureSafeToRender(new string('x', 10_001)));
        Assert.Contains("10000-character", ex.Message);
    }

    [Fact]
    public void EnsureSafeToRender_accepts_nesting_at_the_depth_limit() =>
        MathFormulaRenderer.EnsureSafeToRender(new string('{', 50) + "x" + new string('}', 50));

    [Fact]
    public void EnsureSafeToRender_rejects_nesting_over_the_depth_limit()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MathFormulaRenderer.EnsureSafeToRender(new string('{', 51) + "x" + new string('}', 51)));
        Assert.Contains("50-level", ex.Message);
    }

    [Fact]
    public void EnsureSafeToRender_measures_depth_not_total_brace_count() =>
        MathFormulaRenderer.EnsureSafeToRender(string.Concat(Enumerable.Repeat("{x}", 60)));

    [Fact]
    public void EnsureSafeToRender_closing_braces_reset_depth_between_groups() =>
        MathFormulaRenderer.EnsureSafeToRender(new string('{', 30) + new string('}', 30) + new string('{', 30) + new string('}', 30));

    [AvaloniaFact]
    public void Render_with_malformed_latex_returns_a_bitmap_instead_of_throwing()
    {
        var bitmap = MathFormulaRenderer.Render(@"\frac{", SKColors.Black);

        Assert.NotNull(bitmap);
    }

    [AvaloniaFact]
    public void Render_returns_the_same_bitmap_for_the_same_formula()
    {
        var first = MathFormulaRenderer.Render(@"\frac{a}{b} + \text{cached}", SKColors.Black);

        Assert.Same(first, MathFormulaRenderer.Render(@"\frac{a}{b} + \text{cached}", SKColors.Black));
        Assert.NotSame(first, MathFormulaRenderer.Render(@"\frac{a}{b} + \text{cached}", SKColors.White));
    }

    [AvaloniaFact]
    public void Render_rejects_unsafe_source_before_the_cache()
    {
        var latex = new string('{', 51) + "x" + new string('}', 51);

        Assert.Throws<InvalidOperationException>(() => MathFormulaRenderer.Render(latex, SKColors.Black));
    }

    [AvaloniaTheory]
    [InlineData(@"x^2")]
    [InlineData(@"\sum_{i=0}^{n} i^2 = \frac{n(n+1)(2n+1)}{6}")]
    [InlineData(@"\begin{pmatrix} a & b \\ c & d \end{pmatrix}")]
    [InlineData(@"\frac{")]
    [InlineData("")]
    public void Bake_draws_the_pixels_of_CSharpMaths_own_png(string latex)
    {
        var color = SKColor.Parse("#27272A");
        var painter = new MathPainter { LaTeX = latex, FontSize = 16f, TextColor = color, DisplayErrorInline = true };
        using var stream = painter.DrawAsStream()!;
        var expected = new Bitmap(stream);

        var actual = MathFormulaRenderer.Bake(latex, color, 16f);

        Assert.Equal(expected.PixelSize, actual.PixelSize);
        Assert.Equal(expected.Dpi, actual.Dpi);
        Assert.Equal(Pixels(expected), Pixels(actual));
    }

    private static byte[] Pixels(Bitmap bitmap)
    {
        var stride = bitmap.PixelSize.Width * 4;
        var bytes = new byte[stride * bitmap.PixelSize.Height];
        var buffer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), buffer, bytes.Length, stride);
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return bytes;
    }

    [AvaloniaTheory]
    [InlineData(false, "#27272A")]
    [InlineData(true, "#FAFAFA")]
    public void GetThemeTextColor_matches_the_active_theme_variant(bool dark, string expected)
    {
        using var theme = new ThemeScope(dark ? ThemeVariant.Dark : ThemeVariant.Light);

        Assert.Equal(SKColor.Parse(expected), MathFormulaRenderer.GetThemeTextColor());
    }
}
