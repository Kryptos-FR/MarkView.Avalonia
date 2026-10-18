// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class ContainerRendererTests
{
    private sealed class LogSink : ILogSink
    {
        public List<(LogEventLevel Level, string Area, object?[] Values)> Events { get; } = [];

        public bool IsEnabled(LogEventLevel level, string area) => true;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
            Events.Add((level, area, []));

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
            Events.Add((level, area, propertyValues));
    }

    private static StackPanel Render(string markdown, params IMarkViewExtension[] extensions) =>
        Render(markdown, new MarkdownPipelineBuilder().UseCustomContainers().Build(), extensions);

    private static StackPanel Render(string markdown, MarkdownPipeline pipeline, params IMarkViewExtension[] extensions)
    {
        var document = Markdown.Parse(markdown, pipeline);
        // As MarkdownViewer does: Markdig also gives a container its name as a class.
        var renderer = new AvaloniaRenderer { ApplyAttributeClasses = pipeline.Extensions.Any(e => e is Markdig.Extensions.GenericAttributes.GenericAttributesExtension) };
        foreach (var extension in extensions)
            extension.Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    private static StackPanel RenderLogging(string markdown, LogSink sink, params IMarkViewExtension[] extensions)
    {
        var previous = Logger.Sink;
        Logger.Sink = sink;
        try
        {
            return Render(markdown, extensions);
        }
        finally
        {
            Logger.Sink = previous;
        }
    }

    private static ContainerRendererExtension Card(ContainerRendererFactory factory) => new("card", factory);

    private static Border Host(StackPanel root)
    {
        var border = Assert.IsType<ComponentHost>(Assert.Single(root.Children));
        Assert.Contains("markdown-container-control", border.Classes);
        return border;
    }

    private static StackPanel DefaultContent(Control block)
    {
        var border = Assert.IsType<Border>(block);
        Assert.Contains("markdown-custom-container", border.Classes);
        return Assert.IsType<StackPanel>(border.Child);
    }

    private static string? TextOf(Control control) => (control as TextBlock)?.Inlines?.Text ?? (control as TextBlock)?.Text;

    private const string CardDocument = """
        :::card Read this first
        First

        Second
        :::
        """;

    [AvaloniaFact]
    public void Registered_name_renders_the_factory_control_around_the_rendered_blocks()
    {
        Control? content = null;
        string? arguments = null;
        var control = new ContentControl();

        var root = Render(CardDocument, Card((c, a) =>
        {
            (content, arguments) = (c, a);
            control.Content = c;
            return control;
        }));

        var host = Host(root);
        Assert.Same(control, host.Child);
        Assert.Equal(["markdown-container-control", "markdown-container-card"], host.Classes.Where(c => !c.StartsWith(':')));
        Assert.Equal("Read this first", arguments);
        var panel = Assert.IsType<StackPanel>(content);
        Assert.Equal(["First", "Second"], panel.Children.Select(TextOf));
        Assert.All(panel.Children, c => Assert.Contains("markdown-paragraph", c.Classes));
        Assert.Same(panel, ((ComponentHost)host).ContentPanel);
    }

    [AvaloniaTheory]
    [InlineData(":::card", "")]
    [InlineData(":::card   Padded title  ", "Padded title")]
    public void Arguments_are_trimmed_and_empty_without_text(string opening, string expected)
    {
        string? arguments = null;

        Render($"{opening}\nx\n:::", Card((c, a) =>
        {
            arguments = a;
            return new ContentControl { Content = c };
        }));

        Assert.Equal(expected, arguments);
    }

    [AvaloniaTheory]
    [InlineData("card", "CARD")]
    [InlineData("Card", "card")]
    public void Name_matches_case_insensitively_and_its_class_is_lower_case(string registered, string written)
    {
        var root = Render($":::{written}\nx\n:::", new ContainerRendererExtension(registered, (c, _) => new ContentControl { Content = c }));

        Assert.Contains("markdown-container-card", Host(root).Classes);
    }

    [AvaloniaFact]
    public void Other_names_render_as_default_containers()
    {
        var calls = 0;

        var root = Render(":::note Title\nx\n:::\n\n:::\ny\n:::", Card((c, _) =>
        {
            calls++;
            return new ContentControl { Content = c };
        }));

        Assert.Equal(0, calls);
        Assert.Equal(2, root.Children.Count);
        Assert.Equal(["Title", "x"], DefaultContent(root.Children[0]).Children.Select(TextOf));
        Assert.Equal(["y"], DefaultContent(root.Children[1]).Children.Select(TextOf));
    }

    [AvaloniaFact]
    public void Last_registration_for_a_name_wins()
    {
        var root = Render(CardDocument,
            Card((c, _) => new ContentControl { Content = c, Tag = "first" }),
            new ContainerRendererExtension("CARD", (c, _) => new ContentControl { Content = c, Tag = "second" }));

        Assert.Equal("second", Assert.IsType<ContentControl>(Host(root).Child).Tag);
    }

    [AvaloniaFact]
    public void Factory_returning_null_renders_the_default_container_with_its_title()
    {
        var root = Render(CardDocument, Card((_, _) => null));

        var border = Assert.IsType<Border>(Assert.Single(root.Children));
        Assert.Equal(["markdown-custom-container", "card"], border.Classes.Where(c => !c.StartsWith(':')));
        var content = DefaultContent(border);
        Assert.Equal(["Read this first", "First", "Second"], content.Children.Select(TextOf));
        Assert.Contains("markdown-custom-container-title", content.Children[0].Classes);
    }

    public static TheoryData<string> Parents => ["panel", "content control", "decorator", "presenter"];

    [AvaloniaTheory]
    [MemberData(nameof(Parents))]
    public void Content_placed_in_a_dropped_control_is_taken_back_when_the_factory_returns_null(string parent)
    {
        var root = Render(CardDocument, Card((c, _) =>
        {
            Control dropped = parent switch
            {
                "panel" => new StackPanel { Children = { c } },
                "content control" => new ContentControl { Content = c },
                "decorator" => new Border { Child = c },
                _ => new ContentPresenter { Content = c },
            };
            Assert.Same(dropped, c.Parent);
            return null;
        }));

        Assert.Equal(["Read this first", "First", "Second"], DefaultContent(Assert.Single(root.Children)).Children.Select(TextOf));
    }

    [AvaloniaFact]
    public void Content_left_in_an_unknown_control_skips_the_container_and_logs_a_warning()
    {
        var sink = new LogSink();

        var root = RenderLogging(CardDocument + "\n\nafter", sink, Card((c, _) =>
        {
            GC.KeepAlive(new Viewbox { Child = c });
            return null;
        }));

        Assert.Equal(["after"], root.Children.Select(TextOf));
        var warning = Assert.Single(sink.Events, e => e.Level == LogEventLevel.Warning);
        Assert.Equal(LogArea.Control, warning.Area);
        Assert.Equal(nameof(Viewbox), warning.Values[0]);
    }

    [AvaloniaFact]
    public void Factory_throwing_renders_the_default_container_and_logs_a_warning()
    {
        var sink = new LogSink();

        var root = RenderLogging(CardDocument + "\n\nafter", sink, Card((c, _) =>
        {
            GC.KeepAlive(new ContentControl { Content = c });
            throw new InvalidOperationException("boom");
        }));

        Assert.Equal(2, root.Children.Count);
        Assert.Equal(["Read this first", "First", "Second"], DefaultContent(root.Children[0]).Children.Select(TextOf));
        var warning = Assert.Single(sink.Events, e => e.Level == LogEventLevel.Warning);
        Assert.Equal(LogArea.Control, warning.Area);
        Assert.Equal("card", warning.Values[0]);
        Assert.Contains(warning.Values, v => v is InvalidOperationException { Message: "boom" });
    }

    [AvaloniaFact]
    public void Factory_returning_a_control_that_already_has_a_parent_renders_the_default_container()
    {
        var owned = new ContentControl();
        _ = new Border { Child = owned };

        var root = RenderLogging(CardDocument, new LogSink(), Card((c, _) =>
        {
            owned.Content = c;
            return owned;
        }));

        Assert.Equal(["Read this first", "First", "Second"], DefaultContent(Assert.Single(root.Children)).Children.Select(TextOf));
    }

    [AvaloniaFact]
    public void Copy_text_set_on_the_control_is_given_to_the_host()
    {
        var root = Render(CardDocument, Card((c, _) =>
        {
            var control = new ContentControl { Content = c };
            MarkdownSelection.SetCopyText(control, "card");
            return control;
        }));

        Assert.Equal("card", MarkdownSelection.GetCopyText(Host(root)));
    }

    [AvaloniaFact]
    public void Host_has_no_copy_text_of_its_own()
    {
        var root = Render(CardDocument, Card((c, _) => new ContentControl { Content = c }));

        Assert.False(Host(root).IsSet(MarkdownSelection.CopyTextProperty));
    }

    [AvaloniaFact]
    public void Generic_attribute_classes_go_to_the_host()
    {
        var pipeline = new MarkdownPipelineBuilder().UseCustomContainers().UseGenericAttributes().Build();

        var root = Render(":::card {.wide}\nx\n:::", pipeline, Card((c, _) => new ContentControl { Content = c }));

        Assert.Contains("wide", Host(root).Classes);
    }

    [AvaloniaFact]
    public void Nested_containers_render_with_their_own_factories()
    {
        var root = Render("::::card Outer\n:::card Inner\nx\n:::\n::::", Card((c, a) => new ContentControl { Content = c, Tag = a }));

        var outer = Assert.IsType<ContentControl>(Host(root).Child);
        Assert.Equal("Outer", outer.Tag);
        var inner = Assert.IsType<ComponentHost>(Assert.Single(Assert.IsType<StackPanel>(outer.Content).Children));
        Assert.Equal("Inner", Assert.IsType<ContentControl>(inner.Child).Tag);
    }

    [AvaloniaFact]
    public void Constructor_rejects_a_blank_name_or_a_null_factory()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ContainerRendererExtension(" ", (c, _) => null));
        Assert.Throws<ArgumentNullException>(() => new ContainerRendererExtension("card", null!));
    }
}
