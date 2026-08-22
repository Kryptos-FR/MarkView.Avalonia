// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Xml.Linq;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class MarkdownThemeTests
{
    /// <summary>
    /// Walks up from the test assembly's output directory to the repo root (identified
    /// by Directory.Build.props, which exists only at the root) rather than hardcoding a
    /// fixed number of ".." segments, since that count depends on the build configuration
    /// and target framework folder names.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("Could not locate repo root (Directory.Build.props not found in any ancestor).");
        return dir.FullName;
    }

    [Fact]
    public void Image_markdown_image_style_has_no_static_MaxWidth()
    {
        var themePath = Path.Combine(FindRepoRoot(), "src", "MarkView.Avalonia", "Themes", "MarkdownTheme.axaml");
        var doc = XDocument.Load(themePath);
        XNamespace avalonia = "https://github.com/avaloniaui";

        var style = doc.Descendants(avalonia + "Style")
            .FirstOrDefault(s => (string?)s.Attribute("Selector") == "Image.markdown-image");

        Assert.NotNull(style);
        var maxWidthSetter = style!.Elements(avalonia + "Setter")
            .FirstOrDefault(s => (string?)s.Attribute("Property") == "MaxWidth");
        Assert.Null(maxWidthSetter);
    }

    [Fact]
    public void MarkdownViewer_style_defines_a_template_with_named_PART_ScrollViewer()
    {
        var themePath = Path.Combine(FindRepoRoot(), "src", "MarkView.Avalonia", "Themes", "MarkdownTheme.axaml");
        var doc = XDocument.Load(themePath);
        XNamespace avalonia = "https://github.com/avaloniaui";

        var style = doc.Descendants(avalonia + "Style")
            .FirstOrDefault(s => (string?)s.Attribute("Selector") == ":is(mv|MarkdownViewer)");
        Assert.NotNull(style);

        var templateSetter = style!.Elements(avalonia + "Setter")
            .FirstOrDefault(s => (string?)s.Attribute("Property") == "Template");
        Assert.NotNull(templateSetter);

        var controlTemplate = templateSetter!.Descendants(avalonia + "ControlTemplate").FirstOrDefault();
        Assert.NotNull(controlTemplate);

        var scrollViewer = controlTemplate!.Descendants(avalonia + "ScrollViewer").FirstOrDefault();
        Assert.NotNull(scrollViewer);
        Assert.Equal("PART_ScrollViewer", (string?)scrollViewer!.Attribute("Name"));

        var contentPresenter = controlTemplate.Descendants(avalonia + "ContentPresenter").FirstOrDefault();
        Assert.NotNull(contentPresenter);
    }
}
