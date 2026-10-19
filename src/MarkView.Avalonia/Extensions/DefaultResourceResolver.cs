// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Platform;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Opens the resources the viewer reads by itself: <c>avares://</c> assets (synchronously, since they are
/// already in memory), <c>file://</c> paths and <c>http://</c>/<c>https://</c> URLs (through the shared
/// <see cref="HttpClient"/>, up to <see cref="SharedHttpClient.MaxResponseSize"/>). Returns <c>null</c> for any other scheme.
/// </summary>
internal sealed class DefaultResourceResolver : IResourceResolver
{
    public static DefaultResourceResolver Instance { get; } = new();

    private DefaultResourceResolver() { }

    public Task<Stream?> OpenAsync(Uri uri, CancellationToken cancellationToken = default) => uri.Scheme switch
    {
        "avares" => OpenAsset(uri),
        "file" => OpenFileAsync(uri.LocalPath, cancellationToken),
        "http" or "https" => OpenHttpAsync(uri, cancellationToken),
        _ => Task.FromResult<Stream?>(null),
    };

    // A missing asset fails the task rather than throwing, as the other schemes do.
    private static Task<Stream?> OpenAsset(Uri uri)
    {
        try
        {
            return Task.FromResult<Stream?>(AssetLoader.Open(uri));
        }
        catch (Exception e)
        {
            return Task.FromException<Stream?>(e);
        }
    }

    // Read whole, so that the content is seekable and the file is not held open while it is decoded. On the
    // thread pool, because Windows can complete an asynchronous read of a small file synchronously, which
    // would read it on the UI thread and render a file Source before its setter returns.
    private static async Task<Stream?> OpenFileAsync(string path, CancellationToken cancellationToken) =>
        new MemoryStream(await Task.Run(() => File.ReadAllBytesAsync(path, cancellationToken), cancellationToken));

    private static async Task<Stream?> OpenHttpAsync(Uri uri, CancellationToken cancellationToken) =>
        await SharedHttpClient.DownloadAsync(uri, cancellationToken);
}

/// <summary>
/// Tries <paramref name="Resolver"/> first, then <see cref="DefaultResourceResolver"/> when it returns
/// <c>null</c>. Equal to any other instance wrapping the same resolver, so that the
/// <see cref="ImageCache"/> shares images between renders and viewers that use it.
/// </summary>
internal sealed record FallbackResourceResolver(IResourceResolver Resolver) : IResourceResolver
{
    // Completes synchronously when both resolvers do, so that a document read from memory renders at once.
    public async Task<Stream?> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        await Resolver.OpenAsync(uri, cancellationToken) ?? await DefaultResourceResolver.Instance.OpenAsync(uri, cancellationToken);
}
