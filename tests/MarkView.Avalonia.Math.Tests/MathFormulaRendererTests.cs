// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using Avalonia.Styling;

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

    [AvaloniaTheory]
    [InlineData(false, "#27272A")]
    [InlineData(true, "#FAFAFA")]
    public void GetThemeTextColor_matches_the_active_theme_variant(bool dark, string expected)
    {
        using var theme = new ThemeScope(dark ? ThemeVariant.Dark : ThemeVariant.Light);

        Assert.Equal(SKColor.Parse(expected), MathFormulaRenderer.GetThemeTextColor());
    }
}
