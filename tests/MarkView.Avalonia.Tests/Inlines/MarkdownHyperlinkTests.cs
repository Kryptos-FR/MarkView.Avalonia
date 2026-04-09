using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Rendering.Inlines;
using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class MarkdownHyperlinkTests
{
    [AvaloniaFact]
    public void MarkdownHyperlink_stores_NavigateUri()
    {
        var hyperlink = new MarkdownHyperlink
        {
            NavigateUri = new Uri("https://example.com"),
            Title = "Example"
        };
        Assert.Equal(new Uri("https://example.com"), hyperlink.NavigateUri);
        Assert.Equal("Example", hyperlink.Title);
    }

    [AvaloniaFact]
    public void MarkdownHyperlink_is_a_Span()
    {
        var hyperlink = new MarkdownHyperlink();
        Assert.IsAssignableFrom<Span>(hyperlink);
    }
}
