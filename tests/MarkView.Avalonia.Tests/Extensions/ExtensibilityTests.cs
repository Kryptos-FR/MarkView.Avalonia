// tests/MarkView.Avalonia.Tests/Extensions/ExtensibilityTests.cs
using Avalonia.Media;
using MarkView.Avalonia.Extensions;
using Xunit;

namespace MarkView.Avalonia.Tests.Extensions;

public class ExtensibilityTests
{
    // ── ICodeHighlighter ──────────────────────────────────────────────────────

    [Fact]
    public void ICodeHighlighter_returns_null_for_unsupported_language()
    {
        ICodeHighlighter highlighter = new StubHighlighter();
        var result = highlighter.Highlight("var x = 1;", "unsupported");
        Assert.Null(result);
    }

    [Fact]
    public void ICodeHighlighter_returns_tokens_for_supported_language()
    {
        ICodeHighlighter highlighter = new StubHighlighter();
        var result = highlighter.Highlight("hello", "stub");
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("hello", result[0].Text);
        Assert.Null(result[0].Foreground);
    }

    [Fact]
    public void ICodeHighlighter_returns_empty_list_for_blank_line()
    {
        ICodeHighlighter highlighter = new StubHighlighter();
        var result = highlighter.Highlight("", "stub");
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── IImageLoader ──────────────────────────────────────────────────────────

    [Fact]
    public void IImageLoader_CanLoad_returns_true_for_matching_url()
    {
        IImageLoader loader = new StubImageLoader("https://example.com/img.png");
        Assert.True(loader.CanLoad("https://example.com/img.png"));
    }

    [Fact]
    public void IImageLoader_CanLoad_returns_false_for_non_matching_url()
    {
        IImageLoader loader = new StubImageLoader("https://example.com/img.png");
        Assert.False(loader.CanLoad("https://other.com/img.png"));
    }

    [Fact]
    public async Task IImageLoader_LoadAsync_returns_null_when_not_found()
    {
        IImageLoader loader = new StubImageLoader("none");
        var result = await loader.LoadAsync("https://example.com/any.png");
        Assert.Null(result);
    }

    // ── Stubs ─────────────────────────────────────────────────────────────────

    private sealed class StubHighlighter : ICodeHighlighter
    {
        public IReadOnlyList<(string Text, IBrush? Foreground)>? Highlight(string line, string? language)
        {
            if (language != "stub") return null;
            if (string.IsNullOrEmpty(line)) return [];
            return [(line, null)];
        }
    }

    private sealed class StubImageLoader(string matchUrl) : IImageLoader
    {
        public bool CanLoad(string url) => url == matchUrl;
        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult<IImage?>(null);
    }
}
