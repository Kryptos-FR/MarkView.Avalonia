using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

// Tests non-rendering behaviour directly on AvaloniaRenderer; RenderTestBase not needed.
public class AvaloniaRendererTests
{
    [AvaloniaFact]
    public void ResolveUrl_relative_with_BaseUri_returns_absolute_url()
    {
        var renderer = new AvaloniaRenderer { BaseUri = new Uri("https://example.com/docs/") };
        var result = renderer.ResolveUrl("images/pic.png");
        Assert.Equal("https://example.com/docs/images/pic.png", result);
    }

    [AvaloniaFact]
    public void ResolveUrl_absolute_url_is_returned_unchanged()
    {
        var renderer = new AvaloniaRenderer { BaseUri = new Uri("https://example.com/docs/") };
        var result = renderer.ResolveUrl("https://other.com/file.png");
        Assert.Equal("https://other.com/file.png", result);
    }

    [AvaloniaFact]
    public void ResolveUrl_relative_url_without_BaseUri_is_returned_unchanged()
    {
        var renderer = new AvaloniaRenderer();
        var result = renderer.ResolveUrl("images/pic.png");
        Assert.Equal("images/pic.png", result);
    }
}
