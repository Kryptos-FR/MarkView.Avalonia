// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Parsing;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

/// <summary>An HTML <c>&lt;picture&gt;</c> shows the source for the application's theme.</summary>
public class HtmlPictureTests
{
    private static MarkdownPipeline Pipeline => new MarkdownPipelineBuilder().UseHtmlSubset().Build();

    // Unique addresses, so that the shared image cache never answers for another test.
    private static string Url(string name) => $"fake://{Guid.NewGuid():N}/{name}";

    private static string Picture(string light, string dark, string fallback) => $"""
        <picture>
          <source media="(prefers-color-scheme: dark)" srcset="{dark}">
          <source media="(prefers-color-scheme: light)" srcset="{light}">
          <img alt="Logo" src="{fallback}" width="200">
        </picture>
        """;

    private static LinkInline Image(string markdown) =>
        Markdown.Parse(markdown, Pipeline).Descendants<LinkInline>().Single(l => l.IsImage);

    private static HtmlPicture? Sources(string markdown) => Image(markdown).GetData(typeof(HtmlPicture)) as HtmlPicture;

    [AvaloniaFact]
    public void Picture_sources_go_with_the_image_of_its_img()
    {
        var picture = Sources(Picture("light.png", "dark.png", "logo.png"));

        Assert.NotNull(picture);
        Assert.Equal("light.png", picture.UrlFor(dark: false));
        Assert.Equal("dark.png", picture.UrlFor(dark: true));
        Assert.Equal("logo.png", Image(Picture("light.png", "dark.png", "logo.png")).Url);
    }

    [AvaloniaFact]
    public void Picture_inline_in_a_paragraph_has_its_sources_too()
    {
        var picture = Sources("""Logo: <picture><source media="(prefers-color-scheme: dark)" srcset="dark.png"><img src="logo.png"></picture>""");

        Assert.Equal("dark.png", picture!.UrlFor(dark: true));
        Assert.Null(picture.UrlFor(dark: false));
    }

    [AvaloniaTheory]
    [InlineData("dark.png 1x, dark@2x.png 2x", "dark.png")]
    [InlineData("  dark.png  ", "dark.png")]
    [InlineData("dark.png 200w", "dark.png")]
    public void A_srcset_gives_its_first_candidate(string srcset, string expected)
    {
        var picture = Sources($"""<picture><source media="(prefers-color-scheme: dark)" srcset="{srcset}"><img src="logo.png"></picture>""");

        Assert.Equal(expected, picture!.UrlFor(dark: true));
    }

    [AvaloniaFact]
    public void The_first_source_that_applies_wins_and_one_without_media_applies_to_both()
    {
        var picture = Sources("""
            <picture>
              <source media="(min-width: 600px)" srcset="wide.png">
              <source media="( PREFERS-COLOR-SCHEME : DARK )" srcset="dark.png">
              <source srcset="any.png">
              <source media="(prefers-color-scheme: light)" srcset="light.png">
              <img src="logo.png">
            </picture>
            """);

        Assert.Equal("dark.png", picture!.UrlFor(dark: true));
        Assert.Equal("any.png", picture.UrlFor(dark: false));
    }

    [AvaloniaTheory]
    [InlineData("""<picture><source media="print" srcset="print.png"><img src="logo.png"></picture>""")]
    [InlineData("""<picture><source media="(prefers-color-scheme: dark)"><img src="logo.png"></picture>""")]
    [InlineData("""<picture><img src="logo.png"></picture>""")]
    [InlineData("""<source media="(prefers-color-scheme: dark)" srcset="dark.png"><img src="logo.png">""")]
    [InlineData("""<picture><source media="(prefers-color-scheme: dark)" srcset="dark.png"><img src="logo.png">""")]
    public void Without_a_source_that_applies_the_img_is_a_plain_image(string markdown)
    {
        Assert.Null(Sources(markdown));
        Assert.Equal("logo.png", Image(markdown).Url);
    }

    [AvaloniaFact]
    public void A_picture_without_img_shows_nothing()
    {
        var root = RenderWith("""<picture><source media="(prefers-color-scheme: dark)" srcset="dark.png"></picture>""", new RecordingLoader()).RootPanel;

        Assert.Empty(root.GetVisualDescendants().OfType<Image>());
    }

    private sealed class RecordingLoader : IImageLoader
    {
        public List<string> Requests { get; } = [];

        public bool CanLoad(string url) => url.StartsWith("fake://", StringComparison.Ordinal);

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Requests.Add(url);
            return Task.FromResult<IImage?>(new WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul));
        }
    }

    private static AvaloniaRenderer RenderWith(string markdown, IImageLoader loader, bool lazy = false)
    {
        var pipeline = Pipeline;
        var renderer = new AvaloniaRenderer { IsLazyRenderingEnabled = lazy };
        renderer.ImageLoaders.Clear();
        renderer.ImageLoaders.Add(loader);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer;
    }

    private static Image ImageControl(Control root) =>
        root.GetLogicalDescendants().OfType<InlineUIContainer>().Select(c => c.Child).OfType<Image>().Single();

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shows_the_source_for_the_theme_and_switches_with_it(bool lazy)
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (light, dark, fallback) = (Url("light.png"), Url("dark.png"), Url("logo.png"));
        var loader = new RecordingLoader();
        var root = RenderWith(Picture(light, dark, fallback), loader, lazy).RootPanel;
        var window = new Window { Width = 400, Height = 300, Content = root };
        try
        {
            window.Show();
            var image = ImageControl(root);
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
            Assert.Equal([light], loader.Requests);
            Assert.Equal(light, image.Tag);
            Assert.Equal("light", ImageExport.GetFileName(image));
            var lightImage = image.Source;

            theme.Switch(ThemeVariant.Dark);
            await AsyncTestHelpers.WaitUntilAsync(() => !ReferenceEquals(image.Source, lightImage));

            Assert.Equal([light, dark], loader.Requests);
            Assert.Equal(dark, image.Tag);
            Assert.Equal("dark", ImageExport.GetFileName(image));
            Assert.Equal(200, image.MaxWidth);
            Assert.Equal("Logo", MarkdownSelection.GetCopyText(image));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_theme_switch_before_the_image_loads_loads_only_the_new_source()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (light, dark) = (Url("light.png"), Url("dark.png"));
        var loader = new RecordingLoader();
        // Lazy: nothing loads until the image is near the viewport, here once it is shown.
        var root = RenderWith(Picture(light, dark, Url("logo.png")), loader, lazy: true).RootPanel;
        var window = new Window { Width = 400, Height = 300 };
        try
        {
            window.Show();
            theme.Switch(ThemeVariant.Dark);
            window.Content = root;
            var image = ImageControl(root);
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);

            Assert.Equal([dark], loader.Requests);
        }
        finally
        {
            window.Close();
        }
    }
}
