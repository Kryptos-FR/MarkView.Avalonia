// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;

using SkiaSharp;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class FormulaCacheTests
{
    // 10 × 10 pixels
    private const long Cost = 400;

    private static Bitmap NewBitmap(int width = 10, int height = 10) =>
        new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96));

    [AvaloniaFact]
    public void A_formula_rendered_again_returns_the_cached_bitmap()
    {
        var cache = new FormulaCache(10 * Cost);
        var renders = 0;
        Bitmap Render() { renders++; return NewBitmap(); }

        var first = cache.GetOrAdd("x^2", SKColors.Black, 16f, Render);
        var second = cache.GetOrAdd("x^2", SKColors.Black, 16f, Render);

        Assert.Same(first, second);
        Assert.Equal(1, renders);
        Assert.Equal(1, cache.Count);
        Assert.Equal(Cost, cache.Bytes);
    }

    [AvaloniaTheory]
    [InlineData("y^2", 0xFF000000u, 16f)]
    [InlineData("x^2", 0xFFFFFFFFu, 16f)]
    [InlineData("x^2", 0xFF000000u, 20f)]
    public void Source_colour_and_font_size_are_all_part_of_the_key(string latex, uint color, float fontSize)
    {
        var cache = new FormulaCache(10 * Cost);
        var first = cache.GetOrAdd("x^2", SKColors.Black, 16f, () => NewBitmap());

        var other = cache.GetOrAdd(latex, new SKColor(color), fontSize, () => NewBitmap());

        Assert.NotSame(first, other);
        Assert.Equal(2, cache.Count);
        Assert.Equal(2 * Cost, cache.Bytes);
    }

    [AvaloniaFact]
    public void The_least_recently_used_bitmap_is_evicted_once_over_capacity()
    {
        var cache = new FormulaCache(2 * Cost);
        var a = cache.GetOrAdd("a", SKColors.Black, 16f, () => NewBitmap());
        cache.GetOrAdd("b", SKColors.Black, 16f, () => NewBitmap());
        // Using "a" again makes "b" the least recently used.
        Assert.Same(a, cache.GetOrAdd("a", SKColors.Black, 16f, () => NewBitmap()));

        cache.GetOrAdd("c", SKColors.Black, 16f, () => NewBitmap());

        Assert.Equal(2, cache.Count);
        Assert.Equal(2 * Cost, cache.Bytes);
        Assert.Same(a, cache.GetOrAdd("a", SKColors.Black, 16f, () => NewBitmap()));
        var rendered = false;
        cache.GetOrAdd("b", SKColors.Black, 16f, () => { rendered = true; return NewBitmap(); });
        Assert.True(rendered);
    }

    [AvaloniaFact]
    public void Eviction_frees_as_many_bitmaps_as_a_heavy_one_needs()
    {
        var cache = new FormulaCache(4 * Cost);
        foreach (var latex in new[] { "a", "b", "c", "d" })
            cache.GetOrAdd(latex, SKColors.Black, 16f, () => NewBitmap());

        // Three times as heavy as each of the others: only the most recent one still fits beside it.
        cache.GetOrAdd("heavy", SKColors.Black, 16f, () => NewBitmap(30, 10));

        Assert.Equal(2, cache.Count);
        Assert.Equal(4 * Cost, cache.Bytes);
    }

    [AvaloniaFact]
    public void A_bitmap_heavier_than_the_capacity_is_returned_but_not_cached()
    {
        var cache = new FormulaCache(Cost);
        cache.GetOrAdd("a", SKColors.Black, 16f, () => NewBitmap());

        var heavy = cache.GetOrAdd("heavy", SKColors.Black, 16f, () => NewBitmap(11, 10));

        Assert.Equal(new PixelSize(11, 10), heavy.PixelSize);
        Assert.Equal(1, cache.Count);
        Assert.Equal(Cost, cache.Bytes);
    }

    [AvaloniaFact]
    public void A_bitmap_as_heavy_as_the_capacity_is_cached()
    {
        var cache = new FormulaCache(Cost);

        cache.GetOrAdd("a", SKColors.Black, 16f, () => NewBitmap());

        Assert.Equal(1, cache.Count);
    }

    [AvaloniaFact]
    public void A_failed_render_propagates_and_caches_nothing()
    {
        var cache = new FormulaCache(10 * Cost);

        Assert.Throws<InvalidOperationException>(() =>
            cache.GetOrAdd("a", SKColors.Black, 16f, () => throw new InvalidOperationException()));

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Bytes);
    }

    [AvaloniaFact]
    public void A_formula_cached_while_it_was_rendering_keeps_the_first_bitmap()
    {
        var cache = new FormulaCache(10 * Cost);
        Bitmap? inner = null;

        var outer = cache.GetOrAdd("a", SKColors.Black, 16f, () =>
        {
            // Stands for another thread caching the same formula meanwhile.
            inner = cache.GetOrAdd("a", SKColors.Black, 16f, () => NewBitmap());
            return NewBitmap();
        });

        Assert.Same(inner, outer);
        Assert.Equal(1, cache.Count);
        Assert.Equal(Cost, cache.Bytes);
    }

    [AvaloniaFact]
    public void Clear_removes_every_bitmap()
    {
        var cache = new FormulaCache(10 * Cost);
        cache.GetOrAdd("a", SKColors.Black, 16f, () => NewBitmap());
        cache.GetOrAdd("b", SKColors.Black, 16f, () => NewBitmap());

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Bytes);
        var rendered = false;
        cache.GetOrAdd("a", SKColors.Black, 16f, () => { rendered = true; return NewBitmap(); });
        Assert.True(rendered);
    }

    [AvaloniaFact]
    public void A_cleared_cache_evicts_only_what_it_holds_since()
    {
        var cache = new FormulaCache(2 * Cost);
        cache.GetOrAdd("a", SKColors.Black, 16f, () => NewBitmap());
        cache.GetOrAdd("b", SKColors.Black, 16f, () => NewBitmap());
        cache.Clear();

        foreach (var latex in new[] { "c", "d", "e" })
            cache.GetOrAdd(latex, SKColors.Black, 16f, () => NewBitmap());

        Assert.Equal(2, cache.Count);
        Assert.Equal(2 * Cost, cache.Bytes);
    }
}
