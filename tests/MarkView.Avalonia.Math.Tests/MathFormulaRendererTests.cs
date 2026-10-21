// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using Avalonia.Styling;

using CSharpMath.SkiaSharp;

using MarkView.Avalonia.Tests.Shared;

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
        // A formula no other test renders, so that the shared cache cannot already hold it.
        var latex = $@"\frac{{a}}{{b}} + \text{{{Guid.NewGuid():N}}}";

        var first = MathFormulaRenderer.Render(latex, SKColors.Black);

        Assert.NotNull(first);
        Assert.Same(first, MathFormulaRenderer.Render(latex, SKColors.Black));
        Assert.NotSame(first, MathFormulaRenderer.Render(latex, SKColors.White));
    }

    [AvaloniaFact]
    public void Render_rejects_unsafe_source_before_the_cache()
    {
        var latex = new string('{', 51) + "x" + new string('}', 51);

        Assert.Throws<InvalidOperationException>(() => MathFormulaRenderer.Render(latex, SKColors.Black));
    }

    // One test over every formula rather than a theory: mutation testing does not map theory cases
    // whose data holds backslashes back to their test. On the UI thread, like every other render, since
    // CSharpMath is not known to be thread-safe.
    [AvaloniaFact]
    public void Draw_draws_the_pixels_of_CSharpMaths_own_png()
    {
        string[] formulas =
        [
            @"x^2",
            @"\sum_{i=0}^{n} i^2 = \frac{n(n+1)(2n+1)}{6}",
            @"\begin{pmatrix} a & b \\ c & d \end{pmatrix}",
            @"\frac{",
            "",
        ];
        var color = SKColor.Parse("#27272A");
        foreach (var latex in formulas)
        {
            var painter = new MathPainter { LaTeX = latex, FontSize = 16f, TextColor = color, DisplayErrorInline = true };
            using var stream = painter.DrawAsStream()!;
            using var codec = SKCodec.Create(stream);
            using var png = SKBitmap.Decode(codec, codec.Info with { ColorType = SKColorType.Bgra8888, AlphaType = SKAlphaType.Premul });

            using var drawn = MathFormulaRenderer.Draw(latex, color, 16f);

            Assert.Equal((png.Width, png.Height), (drawn.Width, drawn.Height));
            Assert.Equal(SKColorType.Bgra8888, drawn.ColorType);
            Assert.Equal(SKAlphaType.Premul, drawn.AlphaType);
            Assert.True(png.GetPixelSpan().SequenceEqual(drawn.GetPixelSpan()), $"Pixels differ for {latex}");
        }
    }

    [AvaloniaFact]
    public void Draw_draws_formulas_in_their_colour_and_size()
    {
        using var dark = MathFormulaRenderer.Draw("x", SKColors.Black, 16f);
        using var light = MathFormulaRenderer.Draw("x", SKColors.White, 16f);
        using var large = MathFormulaRenderer.Draw("x", SKColors.Black, 32f);

        Assert.Contains(dark.GetPixelSpan().ToArray(), b => b != 0);
        Assert.False(dark.GetPixelSpan().SequenceEqual(light.GetPixelSpan()));
        Assert.True(large.Width > dark.Width);
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
