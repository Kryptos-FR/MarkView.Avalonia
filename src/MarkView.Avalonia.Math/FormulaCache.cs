// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media.Imaging;

using SkiaSharp;

namespace MarkView.Avalonia.Math;

/// <summary>
/// The bitmaps of the formulas rendered recently, by source, colour and font size, so that a formula
/// rendered again (a document re-rendered, a streamed paragraph updated, a theme switched back) is not
/// typeset again.
/// </summary>
/// <remarks>
/// Holds at most <see cref="Capacity"/> bytes of pixels and evicts the least recently used bitmap first.
/// An evicted bitmap is never disposed, since it may still be displayed: it is collected once nothing
/// shows it. Thread-safe.
/// </remarks>
internal sealed class FormulaCache(long capacity)
{
    /// <summary>The cache every formula uses.</summary>
    public static FormulaCache Shared { get; } = new(DefaultCapacity);

    /// <summary>The capacity of <see cref="Shared"/>, in bytes of pixels: about two thousand inline formulas.</summary>
    public const long DefaultCapacity = 16 * 1024 * 1024;

    private readonly Lock _gate = new();
    // Most recently used first.
    private readonly LinkedList<(Key Key, Bitmap Bitmap, long Cost)> _recency = new();
    private readonly Dictionary<Key, LinkedListNode<(Key Key, Bitmap Bitmap, long Cost)>> _entries = [];
    private long _bytes;

    /// <summary>The number of cached bitmaps.</summary>
    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    /// <summary>The bytes of pixels of the cached bitmaps.</summary>
    public long Bytes
    {
        get { lock (_gate) return _bytes; }
    }

    /// <summary>Removes every cached bitmap.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _recency.Clear();
            _bytes = 0;
        }
    }

    /// <summary>
    /// Returns the bitmap cached for the formula, or renders it with <paramref name="render"/> and caches
    /// it. An exception from <paramref name="render"/> propagates and nothing is cached.
    /// </summary>
    public Bitmap GetOrAdd(string latex, SKColor color, float fontSize, Func<Bitmap> render)
    {
        var key = new Key(latex, color, fontSize);
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _recency.Remove(node);
                _recency.AddFirst(node);
                return node.Value.Bitmap;
            }
        }

        // Rendered outside the lock: two threads rendering the same formula at once both render it.
        var bitmap = render();
        var cost = 4L * bitmap.PixelSize.Width * bitmap.PixelSize.Height;
        if (cost > capacity)
            return bitmap;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node))
                return node.Value.Bitmap;
            _entries[key] = _recency.AddFirst((key, bitmap, cost));
            _bytes += cost;
            while (_bytes > capacity)
            {
                var last = _recency.Last!.Value;
                _entries.Remove(last.Key);
                _recency.RemoveLast();
                _bytes -= last.Cost;
            }
        }
        return bitmap;
    }

    private readonly record struct Key(string Latex, SKColor Color, float FontSize);
}
