// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// An in-memory cache of the images loaded through an image loader chain, so that rendering the same
/// image again (a document edited and re-rendered, a viewer recreated) neither downloads nor decodes it
/// again. Entries are keyed by the resolved URL, the types of the loaders in the chain, since another
/// chain (e.g. one with the SVG loader) can load the same URL differently, and the resource resolver
/// they read through, since another resolver can find other content at the same URL.
/// </summary>
/// <remarks>
/// Holds at most <see cref="Capacity"/> bytes of images, weighed by <see cref="CostOf"/>, and evicts the
/// least recently used one first. An image heavier than a tenth of the capacity is not cached, so the cache
/// always has room for <see cref="MinimumImages"/> images. An evicted image is never disposed, since it may
/// still be displayed: it is collected once nothing shows it.
/// Failed loads are never cached, so the next render tries again. Concurrent loads of the same entry
/// share a single load, which is cancelled only once every one of them has been cancelled.
/// Thread-safe.
/// </remarks>
internal sealed class ImageCache(long capacity)
{
    /// <summary>The capacity of <see cref="Shared"/> unless the application changes it: 100 MB.</summary>
    public const long DefaultCapacity = 100L * 1024 * 1024;

    /// <summary>The number of images of the largest size it caches that the cache has room for.</summary>
    public const int MinimumImages = 10;

    /// <summary>The cache every render uses; its capacity is <see cref="MarkdownViewerDefaults.ImageCacheSize"/>.</summary>
    public static ImageCache Shared { get; } = new(DefaultCapacity);

    private readonly Lock _gate = new();
    // Most recently used first.
    private readonly LinkedList<(Key Key, IImage Image, long Cost)> _recency = new();
    private readonly Dictionary<Key, LinkedListNode<(Key Key, IImage Image, long Cost)>> _entries = [];
    private readonly Dictionary<Key, SharedLoad> _loads = [];
    private long _bytes;

    /// <summary>
    /// The maximum number of bytes of cached images; <c>0</c> disables the cache (each load then runs on
    /// its own). Lowering it evicts the least recently used images beyond the new capacity.
    /// </summary>
    public long Capacity
    {
        get { lock (_gate) return capacity; }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            lock (_gate)
            {
                capacity = value;
                Trim();
            }
        }
    }

    /// <summary>The number of cached images.</summary>
    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    /// <summary>The number of bytes of the cached images (see <see cref="CostOf"/>).</summary>
    public long Bytes
    {
        get { lock (_gate) return _bytes; }
    }

    /// <summary>Removes every cached image. Loads in flight are not affected, but their results are cached.</summary>
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
    /// Returns the image cached for <paramref name="url"/>, <paramref name="loaders"/> and
    /// <paramref name="resources"/> (the viewer's own loading when <c>null</c>), if any, marking it as the
    /// most recently used, without loading anything.
    /// </summary>
    public IImage? TryGet(string url, IList<IImageLoader> loaders, IResourceResolver? resources = null)
    {
        var key = KeyOf(url, loaders, resources ?? DefaultResourceResolver.Instance);
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var node))
                return null;
            _recency.Remove(node);
            _recency.AddFirst(node);
            return node.Value.Image;
        }
    }

    /// <summary>
    /// Returns the image cached for <paramref name="url"/>, <paramref name="loaders"/> and
    /// <paramref name="resources"/> (the viewer's own loading when <c>null</c>), or loads it with the first
    /// loader that accepts the URL and returns an image (see <see cref="LoadAsync"/>). Returns <c>null</c>
    /// when no loader could load it; a loader's exception propagates.
    /// </summary>
    public async Task<IImage?> GetOrLoadAsync(string url, IList<IImageLoader> loaders, CancellationToken cancellationToken, IResourceResolver? resources = null)
    {
        resources ??= DefaultResourceResolver.Instance;
        if (Capacity == 0)
            return await LoadAsync(url, loaders, cancellationToken, resources);

        var key = KeyOf(url, loaders, resources);
        SharedLoad? load;
        bool start;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _recency.Remove(node);
                _recency.AddFirst(node);
                return node.Value.Image;
            }
            start = !_loads.TryGetValue(key, out load);
            if (start)
                _loads[key] = load = new SharedLoad();
            load!.Waiters++;
        }

        // Started outside the lock, since a loader's code runs synchronously up to its first await.
        if (start)
            _ = RunAsync(key, load, url, loaders, resources);

        try
        {
            return await load.Completion.Task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            bool cancelLoad;
            lock (_gate)
            {
                cancelLoad = --load.Waiters == 0;
                if (cancelLoad)
                    _loads.Remove(key);
            }
            if (cancelLoad)
                load.Cancellation.Cancel();
            throw;
        }
    }

    private static Key KeyOf(string url, IList<IImageLoader> loaders, IResourceResolver resources) =>
        new(url, string.Join('|', loaders.Select(l => l.GetType().FullName)), resources);

    private async Task RunAsync(Key key, SharedLoad load, string url, IList<IImageLoader> loaders, IResourceResolver resources)
    {
        IImage? image;
        try
        {
            image = await LoadAsync(url, loaders, load.Cancellation.Token, resources);
        }
        catch (Exception e)
        {
            Complete(key, load, null);
            load.Completion.SetException(e);
            return;
        }
        Complete(key, load, image);
        load.Completion.SetResult(image);
    }

    // Ends a shared load: later requests no longer join it, and its image (if any) is cached. A load
    // every waiter gave up on was already removed, and a newer load of the same image may have
    // taken its place.
    private void Complete(Key key, SharedLoad load, IImage? image)
    {
        lock (_gate)
        {
            if (_loads.GetValueOrDefault(key) != load)
                return;
            _loads.Remove(key);
            if (image is null)
                return;
            var cost = CostOf(image);
            if (capacity == 0 || cost > capacity / MinimumImages)
                return;
            _entries[key] = _recency.AddFirst((key, image, cost));
            _bytes += cost;
            Trim();
        }
    }

    private void Trim()
    {
        while (_bytes > capacity)
        {
            var (key, _, cost) = _recency.Last!.Value;
            _entries.Remove(key);
            _recency.RemoveLast();
            _bytes -= cost;
        }
    }

    /// <summary>
    /// The memory <paramref name="image"/> takes, as counted by the cache: 4 bytes per pixel of a bitmap, and
    /// for any other image (an SVG drawing, say) as much as a bitmap of its size.
    /// </summary>
    internal static long CostOf(IImage image)
    {
        if (image is Bitmap bitmap)
            return 4L * bitmap.PixelSize.Width * bitmap.PixelSize.Height;
        var size = image.Size;
        return 4L * (long)Math.Ceiling(size.Width) * (long)Math.Ceiling(size.Height);
    }

    /// <summary>
    /// Tries each loader that accepts <paramref name="url"/> in order, reading through
    /// <paramref name="resources"/>, and returns the first image one of them returns, or <c>null</c> when
    /// none does.
    /// </summary>
    internal static async Task<IImage?> LoadAsync(string url, IList<IImageLoader> loaders, CancellationToken cancellationToken, IResourceResolver? resources = null)
    {
        foreach (var loader in loaders)
        {
            if (!loader.CanLoad(url))
                continue;
            var loaded = await loader.LoadAsync(url, resources ?? DefaultResourceResolver.Instance, cancellationToken);
            if (loaded != null)
                return loaded;
        }
        return null;
    }

    // Resources compares with Equals: FallbackResourceResolver instances wrapping the same resolver are equal.
    private readonly record struct Key(string Url, string Loaders, IResourceResolver Resources);

    private sealed class SharedLoad
    {
        public readonly TaskCompletionSource<IImage?> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly CancellationTokenSource Cancellation = new();
        // Guarded by the cache's lock.
        public int Waiters;
    }
}
