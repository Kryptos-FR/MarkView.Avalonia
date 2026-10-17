// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerFenceRendererTests
{
    private const string ChartDocument = """
        before

        ```chart
        1 2 3
        ```

        after
        """;

    private static Control Block(MarkdownViewer viewer, int index) =>
        Assert.IsType<Grid>(viewer.Content).Children.OfType<StackPanel>().First().Children[index];

    private static Control FenceControl(MarkdownViewer viewer)
    {
        var border = Assert.IsType<Border>(Block(viewer, 1));
        Assert.Contains("markdown-custom-fence", border.Classes);
        return border.Child!;
    }

    [AvaloniaFact]
    public void UseFenceRenderer_renders_the_language_with_the_factory_control()
    {
        var viewer = new MarkdownViewer().UseFenceRenderer("chart", (code, _) => new TextBlock { Text = code });
        viewer.Markdown = ChartDocument;

        Assert.Equal("1 2 3", Assert.IsType<TextBlock>(FenceControl(viewer)).Text);
    }

    [AvaloniaFact]
    public void UseFenceRenderer_returns_the_viewer()
    {
        var viewer = new MarkdownViewer();

        Assert.Same(viewer, viewer.UseFenceRenderer("chart", (_, _) => null));
    }

    [AvaloniaFact]
    public void UseFenceRenderer_re_renders_a_rendered_document()
    {
        var viewer = new MarkdownViewer { Markdown = ChartDocument };
        CodeBlockAssert.CodeBorder(Block(viewer, 1));

        viewer.UseFenceRenderer("chart", (_, _) => new Button());

        Assert.IsType<Button>(FenceControl(viewer));
    }

    [AvaloniaFact]
    public void UseFenceRenderer_replaces_the_viewer_registration_of_the_same_language_only()
    {
        var viewer = new MarkdownViewer()
            .UseFenceRenderer("chart", (_, _) => new Button())
            .UseFenceRenderer("map", (_, _) => null)
            .UseFenceRenderer("CHART", (_, _) => new CheckBox());
        viewer.Markdown = ChartDocument;

        Assert.IsType<CheckBox>(FenceControl(viewer));
        Assert.Equal(["map", "CHART"], viewer.Extensions.Cast<FenceRendererExtension>().Select(e => e.Language));
    }

    [AvaloniaFact]
    public void AddFenceRenderer_adds_a_fence_renderer_extension()
    {
        FenceRendererFactory factory = (_, _) => null;
        var extensions = new List<IMarkViewExtension>();

        extensions.AddFenceRenderer("chart", factory);

        var extension = Assert.IsType<FenceRendererExtension>(Assert.Single(extensions));
        Assert.Equal("chart", extension.Language);
        Assert.Same(factory, extension.Factory);
    }

    [AvaloniaFact]
    public void Global_fence_renderer_applies_to_every_viewer()
    {
        using var _ = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.Extensions.AddFenceRenderer("chart", (_, _) => new Button());

        var viewer = new MarkdownViewer { Markdown = ChartDocument };

        Assert.IsType<Button>(FenceControl(viewer));
    }

    [AvaloniaFact]
    public void Viewer_fence_renderer_overrides_the_global_one_for_its_language()
    {
        using var _ = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.Extensions.AddFenceRenderer("chart", (_, _) => new Button());

        var viewer = new MarkdownViewer().UseFenceRenderer("chart", (_, _) => new CheckBox());
        viewer.Markdown = ChartDocument;

        Assert.IsType<CheckBox>(FenceControl(viewer));
    }

    [AvaloniaFact]
    public void Custom_fence_is_copied_as_its_fenced_source()
    {
        var viewer = new MarkdownViewer().UseFenceRenderer("chart", (_, _) => new Button());
        viewer.Markdown = ChartDocument;

        viewer.SelectAll();

        Assert.Equal("before\n```chart\n1 2 3\n```\nafter", viewer.GetSelectedText());
    }
}
