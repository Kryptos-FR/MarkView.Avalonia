// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Shared <see cref="HttpClient"/> instance for the image loaders and remote documents.
/// A single static instance is sufficient — and recommended — to allow connection pooling
/// across <see cref="BitmapImageLoader"/>, <c>SvgImageLoader</c>, and <see cref="MarkdownViewer.Source"/>.
/// </summary>
internal static class SharedHttpClient
{
    /// <summary>The largest response <see cref="DownloadAsync"/> reads: 64 MB.</summary>
    internal const long MaxResponseSize = 64L * 1024 * 1024;

    internal static readonly HttpClient Instance = Create();

    // Pooled connections are recycled after a while, so that a long-running application sees DNS changes.
    // The browser has no SocketsHttpHandler (fetch has no connection pool of its own to recycle).
    private static HttpClient Create() => SocketsHttpHandler.IsSupported
        ? new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        : new HttpClient();

    /// <summary>
    /// Downloads <paramref name="uri"/> into memory, positioned at its start. An untrusted document can point
    /// at anything, so a response larger than <paramref name="maxSize"/> is not read.
    /// </summary>
    /// <exception cref="HttpRequestException">
    /// The response is not a success, or it is larger than <paramref name="maxSize"/>.
    /// </exception>
    internal static async Task<MemoryStream> DownloadAsync(Uri uri, CancellationToken cancellationToken, long maxSize = MaxResponseSize)
    {
        using var response = await Instance.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maxSize)
            throw TooLarge(uri, maxSize);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxSize)
                throw TooLarge(uri, maxSize);
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        return buffer;
    }

    private static HttpRequestException TooLarge(Uri uri, long maxSize) =>
        new($"The response from {uri} is larger than {maxSize} bytes.");
}
