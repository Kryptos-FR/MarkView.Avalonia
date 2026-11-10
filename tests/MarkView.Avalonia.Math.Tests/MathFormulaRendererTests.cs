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
    public void Draw_draws_what_CSharpMaths_own_png_shows()
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
            using var own = DecodePng(painter.DrawAsStream()!);
            using var png = DecodePng(DrawAsRoundedUpStream(painter));

            using var drawn = MathFormulaRenderer.Draw(latex, color, 16f);

            // CSharpMath's own surface truncates the measured size, which cuts off the last row or column of ink.
            Assert.InRange(drawn.Width - own.Width, 0, 1);
            Assert.InRange(drawn.Height - own.Height, 0, 1);
            Assert.Equal((png.Width, png.Height), (drawn.Width, drawn.Height));
            Assert.Equal(SKColorType.Bgra8888, drawn.ColorType);
            Assert.Equal(SKAlphaType.Premul, drawn.AlphaType);
            var delta = MaxDelta(png.GetPixelSpan(), drawn.GetPixelSpan());
            Assert.True(delta <= 8, $"Pixels differ by up to {delta} for {latex}");
        }
    }

    // CSharpMath's DrawAsStream() steps, with the surface size rounded up instead of truncated.
    private static Stream DrawAsRoundedUpStream(MathPainter painter)
    {
        var size = painter.Measure().Size;
        using var surface = SKSurface.Create(new SKImageInfo(
            System.Math.Max((int)System.Math.Ceiling(size.Width), 1), System.Math.Max((int)System.Math.Ceiling(size.Height), 1)));
        painter.Draw(surface.Canvas, CSharpMath.Rendering.FrontEnd.TextAlignment.TopLeft);
        using var snapshot = surface.Snapshot();
        return snapshot.Encode(SKEncodedImageFormat.Png, 100).AsStream();
    }

    private static SKBitmap DecodePng(Stream stream)
    {
        using (stream)
        using (var codec = SKCodec.Create(stream))
            return SKBitmap.Decode(codec, codec.Info with { ColorType = SKColorType.Bgra8888, AlphaType = SKAlphaType.Premul });
    }

    // CSharpMath measures in fractional pixels: at 16 px, E = mc^2 is about 17.9 px high and its ink
    // reaches the 18th row, which a bitmap sized by truncating the measure cuts off (#269).
    [AvaloniaFact]
    public void Draw_keeps_the_whole_formula()
    {
        string[] formulas =
        [
            @"E = mc^2",
            @"x^2",
            @"\sum_{i=0}^{n} i^2 = \frac{n(n+1)(2n+1)}{6}",
            @"\int_0^1 f(x)\,dx",
            @"\begin{pmatrix} a & b \\ c & d \end{pmatrix}",
        ];
        var color = SKColor.Parse("#27272A");
        foreach (var latex in formulas)
        {
            var painter = new MathPainter { LaTeX = latex, FontSize = 16f, TextColor = color, DisplayErrorInline = true };
            var size = painter.Measure().Size;
            const int margin = 8;
            using var roomy = new SKBitmap(new SKImageInfo(
                (int)System.Math.Ceiling(size.Width) + margin, (int)System.Math.Ceiling(size.Height) + margin,
                SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(roomy))
            {
                canvas.Clear(SKColors.Transparent);
                painter.Draw(canvas, CSharpMath.Rendering.FrontEnd.TextAlignment.TopLeft);
            }

            using var drawn = MathFormulaRenderer.Draw(latex, color, 16f);

            Assert.Equal(((int)System.Math.Ceiling(size.Width), (int)System.Math.Ceiling(size.Height)), (drawn.Width, drawn.Height));
            var inkBottom = -1;
            var inkRight = -1;
            for (var y = 0; y < roomy.Height; y++)
            {
                for (var x = 0; x < roomy.Width; x++)
                {
                    if (roomy.GetPixel(x, y).Alpha == 0)
                        continue;
                    inkBottom = System.Math.Max(inkBottom, y);
                    inkRight = System.Math.Max(inkRight, x);
                }
            }
            Assert.True(inkBottom < drawn.Height, $"Ink reaches row {inkBottom} of a {drawn.Height} px high bitmap for {latex}");
            Assert.True(inkRight < drawn.Width, $"Ink reaches column {inkRight} of a {drawn.Width} px wide bitmap for {latex}");
            Assert.True(Region(roomy, drawn.Width, drawn.Height).AsSpan().SequenceEqual(Region(drawn, drawn.Width, drawn.Height)),
                $"Pixels differ for {latex}");
        }
    }

    // The top-left width × height pixels of a bitmap, row by row.
    private static byte[] Region(SKBitmap source, int width, int height)
    {
        var pixels = source.GetPixelSpan();
        var region = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            pixels.Slice(y * source.RowBytes, width * 4).CopyTo(region.AsSpan(y * width * 4));
        return region;
    }

    // The PNG round trip unpremultiplies and premultiplies again, which on ARM (macOS CI) moves the
    // edge pixels of a glyph by a few levels out of 255; a missing or miscoloured formula is off by far more.
    private static int MaxDelta(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        var max = 0;
        for (var i = 0; i < expected.Length; i++)
            max = System.Math.Max(max, System.Math.Abs(expected[i] - actual[i]));
        return max;
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
