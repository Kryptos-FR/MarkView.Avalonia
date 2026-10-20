// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Media;

using MarkView.Avalonia.Extensions;

using Xunit;

namespace MarkView.Avalonia.Tests.Extensions;

/// <summary>
/// <see cref="ImageCache"/> on its own instances, so that no test depends on what others put in the
/// shared cache. Constructs no Avalonia object.
/// </summary>
public class ImageCacheTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // 1×1 by default, so 4 bytes to the cache.
    private sealed class FakeImage(double width = 1, double height = 1) : IImage
    {
        public Size Size => new(width, height);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
    }

    // Room for ten 1×1 images, the fewest a cache has room for.
    private const long Ten = ImageCache.MinimumImages * 4;

    private static IEnumerable<string> Urls(int count) => Enumerable.Range(0, count).Select(i => $"u{i}");

    /// <summary>Loads every URL it is given as a new image, or with <see cref="Load"/> when set; counts its loads.</summary>
    private sealed class CountingLoader : IImageLoader
    {
        public int Loads;
        public List<string> Urls { get; } = [];
        public Func<string, CancellationToken, Task<IImage?>>? Load { get; init; }

        public bool CanLoad(string url) => true;

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Loads);
            lock (Urls) Urls.Add(url);
            return Load?.Invoke(url, cancellationToken) ?? Task.FromResult<IImage?>(new FakeImage());
        }
    }

    private sealed class OtherLoader : IImageLoader
    {
        public bool CanLoad(string url) => true;
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult<IImage?>(new FakeImage());
    }

    private sealed class DecliningLoader : IImageLoader
    {
        public bool CanLoad(string url) => false;
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("never called");
    }

    [Fact]
    public async Task A_cached_image_is_returned_without_loading_it_again()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader();

        var first = await cache.GetOrLoadAsync("a", [loader], Token);
        var second = await cache.GetOrLoadAsync("a", [loader], Token);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, loader.Loads);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task A_cache_hit_completes_synchronously()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader();
        await cache.GetOrLoadAsync("a", [loader], Token);

        Assert.True(cache.GetOrLoadAsync("a", [loader], Token).IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Each_url_is_cached_on_its_own()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader();

        var a = await cache.GetOrLoadAsync("a", [loader], Token);
        var b = await cache.GetOrLoadAsync("b", [loader], Token);

        Assert.NotSame(a, b);
        Assert.Equal(["a", "b"], loader.Urls);
    }

    [Fact]
    public async Task Another_loader_chain_loads_the_same_url_again()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader();
        await cache.GetOrLoadAsync("a", [loader], Token);

        var withOther = await cache.GetOrLoadAsync("a", [new OtherLoader(), loader], Token);
        var otherOrder = await cache.GetOrLoadAsync("a", [loader, new OtherLoader()], Token);

        Assert.NotNull(withOther);
        // [loader, OtherLoader] is a third chain: loader is first, so it loads.
        Assert.Equal(2, loader.Loads);
        Assert.Same(otherOrder, await cache.GetOrLoadAsync("a", [loader, new OtherLoader()], Token));
        Assert.Equal(3, cache.Count);
    }

    [Fact]
    public async Task Another_chain_of_the_same_loader_types_shares_the_cached_image()
    {
        var cache = new ImageCache(Ten);
        var image = await cache.GetOrLoadAsync("a", [new CountingLoader()], Token);
        var loader = new CountingLoader();

        Assert.Same(image, await cache.GetOrLoadAsync("a", [loader], Token));
        Assert.Equal(0, loader.Loads);
    }

    [Fact]
    public async Task The_least_recently_used_image_is_evicted_first()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader();
        foreach (var url in Urls(10))
            await cache.GetOrLoadAsync(url, [loader], Token);
        await cache.GetOrLoadAsync("u0", [loader], Token); // "u1" is now the least recently used

        await cache.GetOrLoadAsync("c", [loader], Token);
        await cache.GetOrLoadAsync("u0", [loader], Token);
        await cache.GetOrLoadAsync("c", [loader], Token);
        Assert.Equal([.. Urls(10), "c"], loader.Urls);

        await cache.GetOrLoadAsync("u1", [loader], Token);
        Assert.Equal([.. Urls(10), "c", "u1"], loader.Urls);
        Assert.Equal(10, cache.Count);
        Assert.Equal(Ten, cache.Bytes);
    }

    [Fact]
    public async Task Images_are_evicted_until_the_new_one_fits()
    {
        var cache = new ImageCache(2 * Ten);
        var loader = new CountingLoader();
        foreach (var url in Urls(20))
            await cache.GetOrLoadAsync(url, [loader], Token);

        // 8 bytes over: the two least recently used images go.
        await cache.GetOrLoadAsync("big", [new CountingLoader { Load = (_, _) => Task.FromResult<IImage?>(new FakeImage(2, 1)) }], Token);

        Assert.Equal(19, cache.Count);
        Assert.Equal(2 * Ten, cache.Bytes);
        await cache.GetOrLoadAsync("u2", [loader], Token);
        Assert.Equal(20, loader.Loads);
        await cache.GetOrLoadAsync("u1", [loader], Token);
        Assert.Equal(21, loader.Loads);
    }

    [Fact]
    public async Task An_image_heavier_than_a_tenth_of_the_capacity_is_not_cached()
    {
        var cache = new ImageCache(10 * 400);
        var image = new FakeImage(10, 10); // 400 bytes: cached
        var heavier = new FakeImage(10, 10.5); // 440 bytes
        var loader = new CountingLoader { Load = (url, _) => Task.FromResult<IImage?>(url == "a" ? image : heavier) };

        Assert.Same(image, await cache.GetOrLoadAsync("a", [loader], Token));
        Assert.Same(heavier, await cache.GetOrLoadAsync("b", [loader], Token));
        await cache.GetOrLoadAsync("a", [loader], Token);
        await cache.GetOrLoadAsync("b", [loader], Token);

        Assert.Equal(["a", "b", "b"], loader.Urls);
        Assert.Equal(1, cache.Count);
        Assert.Equal(400, cache.Bytes);
    }

    [Fact]
    public void A_bitmap_costs_four_bytes_per_pixel_and_another_image_as_much_as_a_bitmap_of_its_size()
    {
        Assert.Equal(4, ImageCache.CostOf(new FakeImage()));
        Assert.Equal(4 * 3 * 2, ImageCache.CostOf(new FakeImage(2.5, 1.2)));
        Assert.Equal(0, ImageCache.CostOf(new FakeImage(0, 0)));
    }

    [Fact]
    public async Task An_evicted_image_is_not_disposed()
    {
        var cache = new ImageCache(Ten);
        var bitmap = new DisposableImage();
        await cache.GetOrLoadAsync("a", [new CountingLoader { Load = (_, _) => Task.FromResult<IImage?>(bitmap) }], Token);

        foreach (var url in Urls(10))
            await cache.GetOrLoadAsync(url, [new CountingLoader()], Token);

        Assert.Equal(10, cache.Count);
        Assert.False(bitmap.IsDisposed);
    }

    private sealed class DisposableImage : IImage, IDisposable
    {
        public bool IsDisposed { get; private set; }
        public Size Size => new(1, 1);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
        public void Dispose() => IsDisposed = true;
    }

    [Fact]
    public async Task Capacity_zero_disables_the_cache()
    {
        var cache = new ImageCache(0);
        var gate = new TaskCompletionSource<IImage?>();
        var loader = new CountingLoader { Load = (_, _) => gate.Task };

        var first = cache.GetOrLoadAsync("a", [loader], Token);
        var second = cache.GetOrLoadAsync("a", [loader], Token);
        gate.SetResult(new FakeImage());
        await Task.WhenAll(first, second);
        await cache.GetOrLoadAsync("a", [loader], Token);

        // Not even concurrent loads are shared.
        Assert.Equal(3, loader.Loads);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task Lowering_the_capacity_evicts_the_least_recently_used_images()
    {
        var cache = new ImageCache(2 * Ten);
        var loader = new CountingLoader();
        foreach (var url in Urls(12))
            await cache.GetOrLoadAsync(url, [loader], Token);

        cache.Capacity = Ten;

        Assert.Equal(Ten, cache.Capacity);
        Assert.Equal(10, cache.Count);
        Assert.Equal(Ten, cache.Bytes);
        await cache.GetOrLoadAsync("u11", [loader], Token);
        await cache.GetOrLoadAsync("u2", [loader], Token);
        Assert.Equal(12, loader.Loads);
        await cache.GetOrLoadAsync("u1", [loader], Token);
        Assert.Equal(13, loader.Loads);
    }

    [Fact]
    public async Task Raising_the_capacity_keeps_every_image()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader();
        foreach (var url in Urls(10))
            await cache.GetOrLoadAsync(url, [loader], Token);

        cache.Capacity = 2 * Ten;
        await cache.GetOrLoadAsync("b", [loader], Token);

        Assert.Equal(11, cache.Count);
    }

    [Fact]
    public async Task Setting_the_capacity_to_zero_empties_the_cache()
    {
        var cache = new ImageCache(Ten);
        await cache.GetOrLoadAsync("a", [new CountingLoader()], Token);

        cache.Capacity = 0;

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Bytes);
    }

    [Fact]
    public void A_negative_capacity_is_rejected()
    {
        var cache = new ImageCache(Ten);

        Assert.Throws<ArgumentOutOfRangeException>(() => cache.Capacity = -1);
        Assert.Equal(Ten, cache.Capacity);
    }

    [Fact]
    public async Task Clear_removes_every_image()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader();
        await cache.GetOrLoadAsync("a", [loader], Token);
        await cache.GetOrLoadAsync("b", [loader], Token);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Bytes);
        await cache.GetOrLoadAsync("a", [loader], Token);
        await cache.GetOrLoadAsync("a", [loader], Token);
        Assert.Equal(3, loader.Loads);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task Images_cached_after_a_clear_are_evicted_least_recently_used_first()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader();
        foreach (var url in Urls(10))
            await cache.GetOrLoadAsync(url, [loader], Token);
        cache.Clear();

        foreach (var url in Urls(10))
            await cache.GetOrLoadAsync(url, [loader], Token);
        await cache.GetOrLoadAsync("u0", [loader], Token); // "u1" is now the least recently used
        await cache.GetOrLoadAsync("d", [loader], Token);
        await cache.GetOrLoadAsync("u0", [loader], Token);

        Assert.Equal([.. Urls(10), .. Urls(10), "d"], loader.Urls);
        Assert.Equal(Ten, cache.Bytes);
    }

    [Fact]
    public async Task A_load_that_returns_nothing_is_not_cached()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader { Load = (_, _) => Task.FromResult<IImage?>(null) };

        Assert.Null(await cache.GetOrLoadAsync("a", [loader], Token));
        Assert.Null(await cache.GetOrLoadAsync("a", [loader], Token));

        Assert.Equal(2, loader.Loads);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task A_load_that_throws_is_not_cached()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader { Load = (_, _) => throw new HttpRequestException("404") };

        await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetOrLoadAsync("a", [loader], Token));
        await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetOrLoadAsync("a", [loader], Token));

        Assert.Equal(2, loader.Loads);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task A_load_that_throws_asynchronously_is_not_cached()
    {
        var cache = new ImageCache(Ten);
        var loader = new CountingLoader
        {
            Load = async (_, _) =>
            {
                await Task.Yield();
                throw new IOException("bad data");
            },
        };

        await Assert.ThrowsAsync<IOException>(() => cache.GetOrLoadAsync("a", [loader], Token));
        await Assert.ThrowsAsync<IOException>(() => cache.GetOrLoadAsync("a", [loader], Token));

        Assert.Equal(2, loader.Loads);
    }

    [Fact]
    public async Task Concurrent_loads_of_the_same_image_share_one_load()
    {
        var cache = new ImageCache(Ten);
        var gate = new TaskCompletionSource<IImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new CountingLoader { Load = (_, _) => gate.Task };

        var first = cache.GetOrLoadAsync("a", [loader], Token);
        var second = cache.GetOrLoadAsync("a", [loader], Token);
        Assert.False(first.IsCompleted);
        var image = new FakeImage();
        gate.SetResult(image);

        Assert.Same(image, await first);
        Assert.Same(image, await second);
        Assert.Equal(1, loader.Loads);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task Concurrent_loads_share_a_failure_and_a_later_load_tries_again()
    {
        var cache = new ImageCache(Ten);
        var gate = new TaskCompletionSource<IImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new CountingLoader { Load = (_, _) => gate.Task };

        var first = cache.GetOrLoadAsync("a", [loader], Token);
        var second = cache.GetOrLoadAsync("a", [loader], Token);
        gate.SetException(new HttpRequestException("404"));

        await Assert.ThrowsAsync<HttpRequestException>(() => first);
        await Assert.ThrowsAsync<HttpRequestException>(() => second);
        Assert.Equal(1, loader.Loads);

        // The failed load is no longer joined.
        Assert.Null(await cache.GetOrLoadAsync("a", [new CountingLoader { Load = (_, _) => Task.FromResult<IImage?>(null) }], Token));
        await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetOrLoadAsync("a", [loader], Token));
        Assert.Equal(2, loader.Loads);
    }

    [Fact]
    public async Task Cancelling_one_of_two_concurrent_loads_lets_the_other_finish()
    {
        var cache = new ImageCache(Ten);
        var gate = new TaskCompletionSource<IImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken loadToken = default;
        var loader = new CountingLoader { Load = (_, ct) => { loadToken = ct; return gate.Task; } };
        using var cancelled = new CancellationTokenSource();

        var first = cache.GetOrLoadAsync("a", [loader], cancelled.Token);
        var second = cache.GetOrLoadAsync("a", [loader], Token);
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(loadToken.IsCancellationRequested);
        var image = new FakeImage();
        gate.SetResult(image);
        Assert.Same(image, await second);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task Cancelling_every_concurrent_load_cancels_the_shared_load()
    {
        var cache = new ImageCache(Ten);
        CancellationToken loadToken = default;
        var loader = new CountingLoader
        {
            Load = (_, ct) =>
            {
                loadToken = ct;
                return Task.Delay(Timeout.Infinite, ct).ContinueWith<IImage?>(_ => null, TaskScheduler.Default);
            },
        };
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();

        var a = cache.GetOrLoadAsync("a", [loader], first.Token);
        var b = cache.GetOrLoadAsync("a", [loader], second.Token);
        first.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => a);
        Assert.False(loadToken.IsCancellationRequested);
        second.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => b);

        Assert.True(loadToken.IsCancellationRequested);
        Assert.Equal(1, loader.Loads);
    }

    [Fact]
    public async Task A_load_every_waiter_gave_up_on_is_not_cached_and_does_not_end_a_newer_load()
    {
        var cache = new ImageCache(Ten);
        var gates = new List<TaskCompletionSource<IImage?>>();
        // Ignores cancellation, so the abandoned load still completes.
        var loader = new CountingLoader
        {
            Load = (_, _) =>
            {
                var gate = new TaskCompletionSource<IImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
                gates.Add(gate);
                return gate.Task;
            },
        };
        using var abandoned = new CancellationTokenSource();
        var old = cache.GetOrLoadAsync("a", [loader], abandoned.Token);
        abandoned.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => old);

        var newer = cache.GetOrLoadAsync("a", [loader], Token);
        var joined = cache.GetOrLoadAsync("a", [loader], Token);
        gates[0].SetResult(new FakeImage());
        await Task.Delay(50, Token);
        Assert.Equal(0, cache.Count);
        Assert.False(newer.IsCompleted);

        var image = new FakeImage();
        gates[1].SetResult(image);
        Assert.Same(image, await newer);
        Assert.Same(image, await joined);
        Assert.Equal(2, loader.Loads);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task A_failed_load_every_waiter_gave_up_on_does_not_end_a_newer_load()
    {
        var cache = new ImageCache(Ten);
        var gates = new List<TaskCompletionSource<IImage?>>();
        var loader = new CountingLoader
        {
            Load = (_, _) =>
            {
                var gate = new TaskCompletionSource<IImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
                gates.Add(gate);
                return gate.Task;
            },
        };
        using var abandoned = new CancellationTokenSource();
        var old = cache.GetOrLoadAsync("a", [loader], abandoned.Token);
        abandoned.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => old);
        var newer = cache.GetOrLoadAsync("a", [loader], Token);

        gates[0].SetException(new IOException());
        await Task.Delay(50, Token);
        var joined = cache.GetOrLoadAsync("a", [loader], Token);
        gates[1].SetResult(new FakeImage());

        Assert.Same(await newer, await joined);
        Assert.Equal(2, loader.Loads);
    }

    [Fact]
    public async Task Results_of_loads_in_flight_are_cached_after_a_clear()
    {
        var cache = new ImageCache(Ten);
        var gate = new TaskCompletionSource<IImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var load = cache.GetOrLoadAsync("a", [new CountingLoader { Load = (_, _) => gate.Task }], Token);

        cache.Clear();
        gate.SetResult(new FakeImage());
        await load;

        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task A_load_finishing_after_the_cache_was_disabled_is_not_cached()
    {
        var cache = new ImageCache(Ten);
        var gate = new TaskCompletionSource<IImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var load = cache.GetOrLoadAsync("a", [new CountingLoader { Load = (_, _) => gate.Task }], Token);

        cache.Capacity = 0;
        gate.SetResult(new FakeImage());
        Assert.NotNull(await load);

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task LoadAsync_tries_the_loaders_in_order_until_one_returns_an_image()
    {
        var empty = new CountingLoader { Load = (_, _) => Task.FromResult<IImage?>(null) };
        var image = new FakeImage();
        var found = new CountingLoader { Load = (_, _) => Task.FromResult<IImage?>(image) };
        var last = new CountingLoader();

        Assert.Same(image, await ImageCache.LoadAsync("a", [new DecliningLoader(), empty, found, last], Token));
        Assert.Equal(1, empty.Loads);
        Assert.Equal(0, last.Loads);
    }

    [Fact]
    public async Task LoadAsync_returns_null_when_no_loader_accepts_the_url()
    {
        Assert.Null(await ImageCache.LoadAsync("a", [new DecliningLoader()], Token));
    }
}
