// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Extensions;

public class DefaultResourceResolverTests
{
    private static readonly DefaultResourceResolver Resolver = DefaultResourceResolver.Instance;

    private sealed class MapResolver(Dictionary<string, string> resources) : IResourceResolver
    {
        public List<Uri> Requests { get; } = [];

        public Task<Stream?> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            Requests.Add(uri);
            return Task.FromResult<Stream?>(resources.TryGetValue(uri.AbsoluteUri, out var text)
                ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text))
                : null);
        }
    }

    private static async Task<string?> ReadAsync(Task<Stream?> opening)
    {
        using var stream = await opening;
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [AvaloniaFact]
    public async Task An_avares_asset_opens_synchronously()
    {
        var opening = Resolver.OpenAsync(new Uri("avares://MarkView.Avalonia.Tests/TestAssets/test.md"), TestContext.Current.CancellationToken);

        Assert.True(opening.IsCompletedSuccessfully);
        Assert.Contains("# ", await ReadAsync(opening));
    }

    [AvaloniaFact]
    public async Task A_missing_avares_asset_fails_the_task_instead_of_throwing()
    {
        var opening = Resolver.OpenAsync(new Uri("avares://MarkView.Avalonia.Tests/TestAssets/missing.md"), TestContext.Current.CancellationToken);

        Assert.True(opening.IsFaulted);
        await Assert.ThrowsAnyAsync<Exception>(() => opening);
    }

    [Fact]
    public async Task A_file_opens_as_a_seekable_stream()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "from a file", TestContext.Current.CancellationToken);

            using var stream = await Resolver.OpenAsync(new Uri(path), TestContext.Current.CancellationToken);

            Assert.NotNull(stream);
            Assert.True(stream.CanSeek);
            Assert.Equal("from a file", await new StreamReader(stream).ReadToEndAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_missing_file_fails()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".md");

        await Assert.ThrowsAnyAsync<IOException>(() => Resolver.OpenAsync(new Uri(path), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_http_url_is_downloaded()
    {
        using var server = new LoopbackHttpServer(new Dictionary<string, Action<System.Net.HttpListenerResponse>>
        {
            ["/doc.md"] = LoopbackHttpServer.Content("text/markdown", "from the web"u8.ToArray()),
        });

        Assert.Equal("from the web", await ReadAsync(Resolver.OpenAsync(new Uri(server.Url("doc.md")), TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("zip://manual/index.md")]
    [InlineData("ftp://example.com/index.md")]
    public async Task Any_other_scheme_is_left_to_someone_else(string uri) =>
        Assert.Null(await Resolver.OpenAsync(new Uri(uri), TestContext.Current.CancellationToken));

    // ── FallbackResourceResolver ──────────────────────────────────────────────

    [Fact]
    public async Task The_custom_resolver_is_asked_first()
    {
        var custom = new MapResolver(new() { ["zip://manual/index.md"] = "from the zip" });

        Assert.Equal("from the zip", await ReadAsync(new FallbackResourceResolver(custom).OpenAsync(new Uri("zip://manual/index.md"), TestContext.Current.CancellationToken)));
    }

    [AvaloniaFact]
    public async Task What_the_custom_resolver_leaves_is_opened_by_the_viewer()
    {
        var custom = new MapResolver([]);
        var uri = new Uri("avares://MarkView.Avalonia.Tests/TestAssets/test.md");

        var opening = new FallbackResourceResolver(custom).OpenAsync(uri, TestContext.Current.CancellationToken);

        Assert.True(opening.IsCompletedSuccessfully);
        Assert.Contains("# ", await ReadAsync(opening));
        Assert.Equal([uri], custom.Requests);
    }

    [Fact]
    public void Wrappers_of_the_same_resolver_are_equal()
    {
        var custom = new MapResolver([]);

        Assert.Equal(new FallbackResourceResolver(custom), new FallbackResourceResolver(custom));
        Assert.NotEqual(new FallbackResourceResolver(custom), new FallbackResourceResolver(new MapResolver([])));
    }
}
