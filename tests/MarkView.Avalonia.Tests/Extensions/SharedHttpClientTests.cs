// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Net;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Extensions;

/// <summary><see cref="SharedHttpClient.DownloadAsync"/> against a loopback server. Constructs no Avalonia object.</summary>
public class SharedHttpClientTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly byte[] Body = [1, 2, 3, 4, 5, 6, 7, 8];

    // The body without a Content-Length, so its size is only known once read.
    private static Action<HttpListenerResponse> Chunked(byte[] body) => response =>
    {
        response.SendChunked = true;
        response.OutputStream.Write(body);
        response.Close();
    };

    private static LoopbackHttpServer Server() => new(new Dictionary<string, Action<HttpListenerResponse>>
    {
        ["/body"] = LoopbackHttpServer.Content("application/octet-stream", Body),
        ["/chunked"] = Chunked(Body),
    });

    [Fact]
    public async Task Downloads_the_body_positioned_at_its_start()
    {
        using var server = Server();

        using var buffer = await SharedHttpClient.DownloadAsync(new Uri(server.Url("/body")), Token);

        Assert.Equal(0, buffer.Position);
        Assert.Equal(Body, buffer.ToArray());
    }

    [Theory]
    [InlineData("/body")]
    [InlineData("/chunked")]
    public async Task Reads_a_body_of_exactly_the_largest_size(string path)
    {
        using var server = Server();

        using var buffer = await SharedHttpClient.DownloadAsync(new Uri(server.Url(path)), Token, maxSize: Body.Length);

        Assert.Equal(Body, buffer.ToArray());
    }

    [Theory]
    [InlineData("/body")]
    [InlineData("/chunked")]
    public async Task Rejects_a_body_larger_than_the_largest_size(string path)
    {
        using var server = Server();

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => SharedHttpClient.DownloadAsync(new Uri(server.Url(path)), Token, maxSize: Body.Length - 1));
        Assert.Contains("larger than 7 bytes", error.Message);
    }

    [Fact]
    public async Task Rejects_a_body_declared_larger_than_the_largest_size_before_reading_it()
    {
        // Declares 1,008 bytes, sends 8, then drops the connection: only the declared length can tell.
        using var server = new LoopbackHttpServer(new Dictionary<string, Action<HttpListenerResponse>>
        {
            ["/declared"] = LoopbackHttpServer.Truncated(Body),
        });

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => SharedHttpClient.DownloadAsync(new Uri(server.Url("/declared")), Token, maxSize: 100));
        Assert.Contains("larger than 100 bytes", error.Message);
    }

    [Fact]
    public async Task Rejects_an_error_response()
    {
        using var server = Server();

        await Assert.ThrowsAsync<HttpRequestException>(() => SharedHttpClient.DownloadAsync(new Uri(server.Url("/missing")), Token));
    }

    [Fact]
    public void Allows_64_MB_by_default() =>
        Assert.Equal(64 * 1024 * 1024, SharedHttpClient.MaxResponseSize);
}
