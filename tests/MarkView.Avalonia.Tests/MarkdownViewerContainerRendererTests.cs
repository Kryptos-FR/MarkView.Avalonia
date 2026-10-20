// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerContainerRendererTests
{
    private const string SpoilerDocument = """
        before

        :::spoiler Answer
        The [answer](https://example.com/a) is 42.
        :::

        after [end](https://example.com/e)
        """;

    private static readonly MarkdownPipeline ContainerPipeline =
        new MarkdownPipelineBuilder().UseGitHubExtensions().UseCustomContainers().Build();

    private static Expander Spoiler(Control content, string title) =>
        new() { Header = title, Content = content, IsExpanded = true };

    private static Control Block(MarkdownViewer viewer, int index) =>
        Assert.IsType<Grid>(viewer.Content).Children.OfType<StackPanel>().First().Children[index];

    private static MarkdownViewer Viewer(string markdown = SpoilerDocument)
    {
        var viewer = new MarkdownViewer { Pipeline = ContainerPipeline }.UseContainerRenderer("spoiler", Spoiler);
        viewer.Markdown = markdown;
        return viewer;
    }

    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

        public string? FocusedLink => (Focused as LinkFocusTarget)?.AccessibleName;

        public void Tab(bool shift = false)
        {
            Window.KeyPress(Key.Tab, shift ? RawInputModifiers.Shift : RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(MarkdownViewer viewer)
    {
        var window = new Window { Width = 400, Height = 600, Content = viewer };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Fixture(window, viewer);
    }

    [AvaloniaFact]
    public void UseContainerRenderer_renders_the_container_with_the_factory_control()
    {
        var viewer = Viewer();

        var host = Assert.IsType<Border>(Block(viewer, 1), exactMatch: false);
        Assert.Contains("markdown-container-spoiler", host.Classes);
        var expander = Assert.IsType<Expander>(host.Child);
        Assert.Equal("Answer", expander.Header);
    }

    [AvaloniaFact]
    public void UseContainerRenderer_returns_the_viewer_and_rerenders()
    {
        var viewer = new MarkdownViewer { Pipeline = ContainerPipeline, Markdown = SpoilerDocument };
        Assert.Contains("markdown-custom-container", Block(viewer, 1).Classes);

        Assert.Same(viewer, viewer.UseContainerRenderer("spoiler", Spoiler));

        Assert.Contains("markdown-container-control", Block(viewer, 1).Classes);
    }

    [AvaloniaFact]
    public void UseContainerRenderer_replaces_the_viewers_earlier_renderer_for_the_name()
    {
        var viewer = Viewer();
        viewer.Extensions.Add(new ContainerRendererExtension("other", (c, _) => null));

        viewer.UseContainerRenderer("SPOILER", (c, _) => new ContentControl { Content = c });

        Assert.Single(viewer.Extensions.OfType<ContainerRendererExtension>(), e => e.Name.Equals("spoiler", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, viewer.Extensions.Count);
        Assert.IsType<ContentControl>(Assert.IsType<Border>(Block(viewer, 1), exactMatch: false).Child);
    }

    [AvaloniaFact]
    public void AddContainerRenderer_on_the_defaults_applies_to_every_viewer()
    {
        using var scope = new MarkdownViewerDefaultsScope();
        MarkdownViewerDefaults.Extensions.AddContainerRenderer("spoiler", Spoiler);

        var viewer = new MarkdownViewer { Pipeline = ContainerPipeline, Markdown = SpoilerDocument };

        Assert.IsType<Expander>(Assert.IsType<Border>(Block(viewer, 1), exactMatch: false).Child);
    }

    [AvaloniaFact]
    public void Selection_copies_the_markdown_inside_the_control()
    {
        var viewer = Viewer();

        viewer.SelectAll();

        Assert.Equal("before\nThe answer is 42.\nafter end", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Selection_copies_the_controls_own_copy_text_instead_when_it_has_one()
    {
        var viewer = new MarkdownViewer { Pipeline = ContainerPipeline }.UseContainerRenderer("spoiler", (c, _) =>
        {
            var control = new ContentControl { Content = c };
            MarkdownSelection.SetCopyText(control, "[spoiler]");
            return control;
        });
        viewer.Markdown = SpoilerDocument;

        viewer.SelectAll();

        Assert.Equal("before\n[spoiler]\nafter end", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Find_searches_the_markdown_inside_the_control()
    {
        var viewer = Viewer();

        Assert.Equal(1, viewer.Find("42"));
    }

    [AvaloniaFact]
    public void Tab_reaches_the_controls_header_then_the_links_inside_it_in_reading_order()
    {
        using var f = Show(Viewer());
        f.Viewer.Focus();

        f.Tab();
        var header = Assert.IsType<ToggleButton>(f.Focused);
        f.Tab();
        Assert.Equal("answer", f.FocusedLink);
        f.Tab();
        Assert.Equal("end", f.FocusedLink);
        f.Tab(shift: true);
        f.Tab(shift: true);
        Assert.Same(header, f.Focused);
    }

    [AvaloniaFact]
    public void Collapsed_control_hides_its_links_from_Tab_and_its_text_from_selection()
    {
        using var f = Show(Viewer());
        var expander = Assert.IsType<Expander>(Assert.IsType<Border>(Block(f.Viewer, 1), exactMatch: false).Child);
        expander.IsExpanded = false;
        Dispatcher.UIThread.RunJobs();
        f.Viewer.Focus();

        f.Tab();
        Assert.IsType<ToggleButton>(f.Focused);
        f.Tab();
        Assert.Equal("end", f.FocusedLink);

        f.Viewer.SelectAll();
        Assert.Equal("before\nafter end", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Disabled_or_hidden_parts_of_the_control_are_not_stops()
    {
        var viewer = new MarkdownViewer { Pipeline = ContainerPipeline }.UseContainerRenderer("spoiler", (c, _) => new StackPanel
        {
            Children =
            {
                new Button { Content = "disabled", IsEnabled = false },
                new Button { Content = "hidden", IsVisible = false },
                new Button { Content = "shown" },
                c,
            },
        });
        viewer.Markdown = SpoilerDocument;
        using var f = Show(viewer);
        f.Viewer.Focus();

        f.Tab();
        Assert.Equal("shown", Assert.IsType<Button>(f.Focused).Content);
        f.Tab();
        Assert.Equal("answer", f.FocusedLink);
    }

    [AvaloniaFact]
    public void A_focusable_part_is_one_stop_and_the_content_stops_are_not_repeated()
    {
        var viewer = new MarkdownViewer { Pipeline = ContainerPipeline }.UseContainerRenderer("spoiler", (c, _) => new StackPanel
        {
            Children = { new Button { Content = new Button { Content = "inner" } }, c },
        });
        viewer.Markdown = """
            :::spoiler
            ```
            code
            ```
            :::
            """;
        using var f = Show(viewer);
        f.Viewer.Focus();

        f.Tab();
        Assert.IsType<Button>(Assert.IsType<Button>(f.Focused).Content);
        f.Tab();
        Assert.Contains("markdown-code-copy", Assert.IsType<Button>(f.Focused).Classes);
        var copy = f.Focused;
        f.Tab();
        Assert.NotSame(copy, f.Focused);
    }

    [AvaloniaFact]
    public void Parts_of_a_control_inside_collapsed_content_are_not_stops()
    {
        var pipeline = new MarkdownPipelineBuilder().UseGitHubExtensions().UseExtendedAlertBlocks().UseCustomContainers().Build();
        var viewer = new MarkdownViewer { Pipeline = pipeline }.UseContainerRenderer("spoiler", (c, _) => new StackPanel
        {
            Children = { new Button { Content = "part" }, c },
        });
        viewer.Markdown = """
            > [!NOTE]-
            > :::spoiler Hidden
            > inside
            > :::

            [end](https://example.com/e)
            """;
        using var f = Show(viewer);
        f.Viewer.Focus();

        f.Tab();
        Assert.Contains("markdown-alert-header", Assert.IsAssignableFrom<Control>(f.Focused).Classes);
        f.Tab();
        Assert.Equal("end", f.FocusedLink);
    }
}
