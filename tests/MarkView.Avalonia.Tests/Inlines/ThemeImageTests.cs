// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Parsing;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

/// <summary>
/// An image whose address ends with <c>#gh-dark-mode-only</c> or <c>#gh-light-mode-only</c> shows only in that
/// theme, and a pair of them shows as one image that follows the theme.
/// </summary>
public class ThemeImageTests
{
    private const string Dark = "#gh-dark-mode-only";
    private const string Light = "#gh-light-mode-only";

    private static MarkdownPipeline Pipeline => new MarkdownPipelineBuilder().UseHtmlSubset().Build();

    // Unique addresses, so that the shared image cache never answers for another test.
    private static string Url(string name) => $"fake://{Guid.NewGuid():N}/{name}";

    private static List<LinkInline> Images(string markdown) =>
        Markdown.Parse(markdown, Pipeline).Descendants<LinkInline>().Where(l => l.IsImage).ToList();

    [AvaloniaTheory]
    [InlineData("a.png" + Dark, true)]
    [InlineData("a.png" + Light, false)]
    [InlineData("a.png", null)]
    [InlineData("a.png#gh-DARK-mode-only", null)]
    [InlineData("a.png#gh-dark-mode-only-not", null)]
    public void An_address_marks_the_theme_of_its_image(string url, bool? dark)
    {
        var image = Images($"![a]({url})").Single();

        Assert.Equal(dark, ThemeImages.MarkOf(image));
        Assert.Equal(dark is null ? url : "a.png", ThemeImages.Unmarked(image));
    }

    [AvaloniaTheory]
    [InlineData($"![a](l.png{Light})![a](d.png{Dark})")]
    [InlineData($"![a](d.png{Dark}) ![a](l.png{Light})")]
    [InlineData($"![a](l.png{Light})\n![a](d.png{Dark})")]
    [InlineData($"[![a](l.png{Light})](https://example.com) [![a](d.png{Dark})](https://example.com)")]
    [InlineData($"<p align=\"center\"><img src=\"l.png{Light}\" alt=\"a\">\n<img src=\"d.png{Dark}\" alt=\"a\"></p>")]
    [InlineData($"Logo: <img src=\"l.png{Light}\"> <img src=\"d.png{Dark}\">")]
    public void Two_images_for_each_theme_next_to_each_other_are_a_pair(string markdown)
    {
        var images = Images(markdown);
        var (first, second) = (images[0], images[1]);

        Assert.Same(second, ThemeImages.PartnerOf(first));
        Assert.True(ThemeImages.IsSecond(second));
        Assert.False(ThemeImages.IsSecond(first));
        Assert.Null(ThemeImages.PartnerOf(second));
    }

    [AvaloniaTheory]
    [InlineData($"![a](l.png{Light}) and ![a](d.png{Dark})")]
    [InlineData($"![a](l.png{Light})![a](d.png{Light})")]
    [InlineData($"![a](l.png{Light})![a](d.png)")]
    [InlineData($"![a](l.png{Light})\n\n![a](d.png{Dark})")]
    [InlineData($"[![a](l.png{Light})](https://example.com/1) [![a](d.png{Dark})](https://example.com/2)")]
    [InlineData($"[![a](l.png{Light})](https://example.com) ![a](d.png{Dark})")]
    [InlineData($"[See ![a](l.png{Light})](https://example.com) [![a](d.png{Dark})](https://example.com)")]
    [InlineData($"![a](l.png{Light})  \n![a](d.png{Dark})")]
    public void Images_apart_or_for_the_same_theme_are_not_a_pair(string markdown)
    {
        foreach (var image in Images(markdown))
        {
            Assert.Null(ThemeImages.PartnerOf(image));
            Assert.False(ThemeImages.IsSecond(image));
        }
    }

    [AvaloniaFact]
    public void Alternating_images_pair_from_the_first()
    {
        var images = Images($"![a](1.png{Light})![a](2.png{Dark})![a](3.png{Light})![a](4.png{Dark})![a](5.png{Light})");

        Assert.Same(images[1], ThemeImages.PartnerOf(images[0]));
        Assert.Same(images[3], ThemeImages.PartnerOf(images[2]));
        Assert.Null(ThemeImages.PartnerOf(images[4]));
        Assert.False(ThemeImages.IsSecond(images[4]));
    }

    [AvaloniaFact]
    public void The_img_of_a_picture_is_never_marked()
    {
        var images = Images($"""<picture><source media="(prefers-color-scheme: dark)" srcset="d.png"><img src="l.png{Light}"></picture>""");

        Assert.Null(ThemeImages.MarkOf(images.Single()));
    }

    private sealed class RecordingLoader(params string[] failing) : IImageLoader
    {
        public List<string> Requests { get; } = [];

        public bool CanLoad(string url) => url.StartsWith("fake://", StringComparison.Ordinal);

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Requests.Add(url);
            return Task.FromResult<IImage?>(failing.Contains(url)
                ? null
                : new WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul));
        }
    }

    private static Panel RenderWith(string markdown, IImageLoader loader, bool lazy = false)
    {
        var pipeline = Pipeline;
        var renderer = new AvaloniaRenderer { IsLazyRenderingEnabled = lazy };
        renderer.ImageLoaders.Clear();
        renderer.ImageLoaders.Add(loader);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    private static List<Control> Embedded(Control root) =>
        root.GetLogicalDescendants().OfType<InlineUIContainer>().Select(c => c.Child).OfType<Control>().ToList();

    private static async Task Shown(Window window, Panel root)
    {
        window.Content = root;
        window.Show();
        await Task.Yield();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_pair_shows_one_image_that_follows_the_theme(bool lazy)
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (light, dark) = (Url("chart.light.png"), Url("chart.dark.png"));
        var loader = new RecordingLoader();
        var root = RenderWith($"Compile time:\n![Chart]({light}{Light})\n![Chart]({dark}{Dark})", loader, lazy);
        var window = new Window { Width = 400, Height = 300 };
        try
        {
            await Shown(window, root);
            var image = Assert.IsType<Image>(Assert.Single(Embedded(root)));
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
            Assert.Equal([light], loader.Requests);
            Assert.Equal(light, image.Tag);
            Assert.Equal("chart.light", ImageExport.GetFileName(image));
            var lightImage = image.Source;

            theme.Switch(ThemeVariant.Dark);
            await AsyncTestHelpers.WaitUntilAsync(() => !ReferenceEquals(image.Source, lightImage));

            Assert.Equal([light, dark], loader.Requests);
            Assert.Equal(dark, image.Tag);
            Assert.Equal("chart.dark", ImageExport.GetFileName(image));
            Assert.Equal("Chart", MarkdownSelection.GetCopyText(image));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_pair_of_links_shows_one_link()
    {
        var root = RenderWith(
            $"[![Chart]({Url("l.png")}{Light})](https://example.com) [![Chart]({Url("d.png")}{Dark})](https://example.com)",
            new RecordingLoader());

        Assert.Single(root.GetLogicalDescendants().OfType<MarkdownHyperlink>());
        Assert.Single(Embedded(root));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_image_for_the_other_theme_shows_nothing_until_its_theme(bool lazy)
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var dark = Url("moon.png");
        var loader = new RecordingLoader();
        var root = RenderWith($"Night: ![Moon]({dark}{Dark})", loader, lazy);
        var window = new Window { Width = 400, Height = 300 };
        try
        {
            await Shown(window, root);
            var image = Assert.IsType<Image>(Assert.Single(Embedded(root)));
            window.UpdateLayout();
            Assert.Null(image.Source);
            Assert.Empty(loader.Requests);
            Assert.Equal(default, image.Bounds.Size);
            Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(image));

            theme.Switch(ThemeVariant.Dark);
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
            Assert.Equal([dark], loader.Requests);
            Assert.NotEqual(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(image));

            theme.Switch(ThemeVariant.Light);
            window.UpdateLayout();
            Assert.Null(image.Source);
            Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(image));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task An_image_that_fails_in_one_theme_comes_back_in_the_other()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (light, dark) = (Url("l.png"), Url("d.png"));
        var loader = new RecordingLoader(light);
        var root = RenderWith($"![Chart]({light}{Light})![Chart]({dark}{Dark})", loader);
        var window = new Window { Width = 400, Height = 300 };
        try
        {
            await Shown(window, root);
            await AsyncTestHelpers.WaitUntilAsync(() => Embedded(root).Single() is Border);
            Assert.Contains("markdown-image-failed", Embedded(root).Single().Classes);

            theme.Switch(ThemeVariant.Dark);
            await AsyncTestHelpers.WaitUntilAsync(() => Embedded(root).Single() is Image { Source: not null });
            Assert.Equal([light, dark], loader.Requests);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Loaders_get_the_address_without_its_mark()
    {
        using var theme = new ThemeScope(ThemeVariant.Dark);
        var dark = Url("d.png");
        var loader = new RecordingLoader();
        var root = RenderWith($"<img src=\"{dark}{Dark}\" alt=\"Logo\">", loader);
        var window = new Window { Width = 400, Height = 300 };
        try
        {
            await Shown(window, root);
            var image = Assert.IsType<Image>(Assert.Single(Embedded(root)));
            await AsyncTestHelpers.WaitUntilAsync(() => image.Source is not null);
            Assert.Equal([dark], loader.Requests);
        }
        finally
        {
            window.Close();
        }
    }
}
