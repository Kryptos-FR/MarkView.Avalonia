// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;

using Markdig;
using Markdig.Syntax;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class CustomFenceTests
{
    // Stands in for an extension renderer of fenced code blocks (Mermaid's), which every fenced
    // block reaches first unless a fence renderer takes it.
    private sealed class OtherFencedRenderer : AvaloniaObjectRenderer<FencedCodeBlock>
    {
        protected override void Write(AvaloniaRenderer renderer, FencedCodeBlock obj)
        {
            var other = new TextBlock { Text = $"other:{obj.Info}" };
            renderer.WriteBlock(other);
        }
    }

    private sealed class OtherFencedExtension : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) =>
            renderer.ObjectRenderers.Insert(renderer.ObjectRenderers.FindIndex(r => r is CodeBlockRenderer), new OtherFencedRenderer());
    }

    private sealed class LogSink : ILogSink
    {
        public List<(LogEventLevel Level, string Area, object?[] Values)> Events { get; } = [];

        public bool IsEnabled(LogEventLevel level, string area) => true;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
            Events.Add((level, area, []));

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
            Events.Add((level, area, propertyValues));
    }

    private static AvaloniaRenderer Renderer(string markdown, MarkdownPipeline? pipeline, params IMarkViewExtension[] extensions)
    {
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer { ApplyAttributeClasses = pipeline is not null };
        foreach (var extension in extensions)
            extension.Register(renderer);
        pipeline?.Setup(renderer);
        renderer.Render(document);
        return renderer;
    }

    private static StackPanel Render(string markdown, params IMarkViewExtension[] extensions) =>
        Renderer(markdown, null, extensions).RootPanel;

    private static FenceRendererExtension Chart(FenceRendererFactory factory) => new("chart", factory);

    private static Border CustomFence(StackPanel root)
    {
        var border = Assert.IsType<Border>(Assert.Single(root.Children), exactMatch: false);
        Assert.Contains("markdown-custom-fence", border.Classes);
        return border;
    }

    [AvaloniaFact]
    public void Registered_language_renders_the_factory_control_in_a_classed_border()
    {
        string? code = null, info = null;
        var control = new Button();

        var root = Render("```chart\n1 2\n3 4\n```", Chart((c, i) =>
        {
            (code, info) = (c, i);
            return control;
        }));

        var border = CustomFence(root);
        Assert.Same(control, border.Child);
        Assert.Equal(["markdown-custom-fence", "markdown-fence-chart"], border.Classes.Where(c => !c.StartsWith(':')));
        Assert.Equal("1 2\n3 4", code);
        Assert.Equal("chart", info);
    }

    [AvaloniaTheory]
    [InlineData("chart", "CHART")]
    [InlineData("Chart", "chart")]
    public void Language_matches_case_insensitively_and_its_class_is_lower_case(string registered, string fenced)
    {
        string? info = null;

        var root = Render($"```{fenced}\nx\n```", new FenceRendererExtension(registered, (_, i) =>
        {
            info = i;
            return new Button();
        }));

        Assert.Contains("markdown-fence-chart", CustomFence(root).Classes);
        Assert.Equal(fenced, info);
    }

    [AvaloniaFact]
    public void Info_string_after_the_language_is_passed_through()
    {
        string? info = null;

        Render("```chart {height=200} bars\nx\n```", Chart((_, i) =>
        {
            info = i;
            return new Button();
        }));

        Assert.Equal("chart {height=200} bars", info);
    }

    [AvaloniaFact]
    public void Empty_block_passes_empty_code()
    {
        string? code = null;

        Render("```chart\n```", Chart((c, _) =>
        {
            code = c;
            return new Button();
        }));

        Assert.Equal(string.Empty, code);
    }

    [AvaloniaTheory]
    [InlineData("```python\nx\n```", "language-python")]
    [InlineData("```\nx\n```", "markdown-code-block")]
    [InlineData("    indented", "markdown-code-block")]
    public void Other_code_blocks_render_as_code(string markdown, string expectedClass)
    {
        var root = Render(markdown, Chart((_, _) => new Button()));

        Assert.Contains(expectedClass, CodeBlockAssert.CodeBorder(Assert.Single(root.Children)).Classes);
    }

    [AvaloniaFact]
    public void Factory_returning_null_renders_the_block_as_code()
    {
        var root = Render("```chart\nx\n```", Chart((_, _) => null));

        Assert.Contains("language-chart", CodeBlockAssert.CodeBorder(Assert.Single(root.Children)).Classes);
    }

    [AvaloniaFact]
    public void Factory_throwing_renders_the_block_as_code_and_logs_a_warning()
    {
        var sink = new LogSink();
        var previous = Logger.Sink;
        Logger.Sink = sink;
        StackPanel root;
        try
        {
            root = Render("```chart\nx\n```\n\nafter", Chart((_, _) => throw new InvalidOperationException("boom")));
        }
        finally
        {
            Logger.Sink = previous;
        }

        Assert.Contains("language-chart", CodeBlockAssert.CodeBorder(root.Children[0]).Classes);
        Assert.Equal(2, root.Children.Count);
        var warning = Assert.Single(sink.Events, e => e.Level == LogEventLevel.Warning);
        Assert.Equal(LogArea.Control, warning.Area);
        Assert.Equal("chart", warning.Values[0]);
        Assert.Contains(warning.Values, v => v is InvalidOperationException { Message: "boom" });
    }

    [AvaloniaFact]
    public void Factory_returning_a_control_that_already_has_a_parent_renders_the_block_as_code()
    {
        var shared = new Button();
        _ = new Border { Child = shared };

        var root = Render("```chart\nx\n```", Chart((_, _) => shared));

        Assert.Contains("language-chart", CodeBlockAssert.CodeBorder(Assert.Single(root.Children)).Classes);
    }

    [AvaloniaTheory]
    [InlineData("```chart\n1\n2\n```", "```chart\n1\n2\n```")]
    [InlineData("~~~~chart  wide\n```\n~~~~", "~~~~chart wide\n```\n~~~~")]
    public void Copy_text_is_the_fenced_source(string markdown, string expected)
    {
        var root = Render(markdown, Chart((_, _) => new Button()));

        Assert.Equal(expected, MarkdownSelection.GetCopyText(CustomFence(root)));
    }

    [AvaloniaTheory]
    [InlineData("custom")]
    [InlineData(null)]
    public void Copy_text_set_by_the_factory_is_kept(string? copyText)
    {
        var root = Render("```chart\nx\n```", Chart((_, _) =>
        {
            var control = new Button();
            MarkdownSelection.SetCopyText(control, copyText);
            return control;
        }));

        Assert.Equal(copyText, MarkdownSelection.GetCopyText(CustomFence(root)));
    }

    [AvaloniaFact]
    public void Fence_renderer_wins_over_another_fenced_block_renderer_registered_after_it()
    {
        var root = Render("```chart\nx\n```", Chart((_, _) => new Button()), new OtherFencedExtension());

        Assert.IsType<Button>(CustomFence(root).Child);
    }

    [AvaloniaFact]
    public void Other_languages_go_to_the_next_fenced_block_renderer_only()
    {
        var root = Render("```python\nx\n```", Chart((_, _) => new Button()), new OtherFencedExtension());

        Assert.Equal("other:python", Assert.IsType<TextBlock>(Assert.Single(root.Children)).Text);
    }

    [AvaloniaFact]
    public void Last_registration_of_a_language_wins()
    {
        var root = Render("```chart\nx\n```",
            Chart((_, _) => new Button()), new FenceRendererExtension("CHART", (_, _) => new CheckBox()));

        Assert.IsType<CheckBox>(CustomFence(root).Child);
    }

    [AvaloniaFact]
    public void Several_languages_can_be_registered()
    {
        var root = Render("```chart\nx\n```\n\n```map\ny\n```",
            Chart((_, _) => new Button()), new FenceRendererExtension("map", (_, _) => new CheckBox()));

        Assert.IsType<Button>(Assert.IsType<Border>(root.Children[0], exactMatch: false).Child);
        Assert.IsType<CheckBox>(Assert.IsType<Border>(root.Children[1], exactMatch: false).Child);
        Assert.Contains("markdown-fence-map", root.Children[1].Classes);
    }

    [AvaloniaFact]
    public void Generic_attribute_classes_go_to_the_border()
    {
        string? info = null;
        var pipeline = new MarkdownPipelineBuilder().UseGenericAttributes().Build();

        var root = Renderer("```chart {.wide}\nx\n```", pipeline, Chart((_, i) =>
        {
            info = i;
            return new Button();
        })).RootPanel;

        Assert.Contains("wide", CustomFence(root).Classes);
        Assert.Equal("chart", info);
    }

    [AvaloniaFact]
    public void Fence_renderer_is_placed_before_the_first_renderer_of_fenced_blocks()
    {
        var renderer = Renderer("text", null, new OtherFencedExtension(), Chart((_, _) => null));

        var index = renderer.ObjectRenderers.FindIndex(r => r is CustomFenceRenderer);
        Assert.IsType<OtherFencedRenderer>(renderer.ObjectRenderers[index + 1]);
        Assert.Single(renderer.ObjectRenderers, r => r is CustomFenceRenderer);
    }

    [AvaloniaFact]
    public void Fence_renderer_is_appended_when_no_renderer_accepts_fenced_blocks()
    {
        var document = Markdown.Parse("```chart\nx\n```\n\n```python\ny\n```");
        var renderer = new AvaloniaRenderer();
        renderer.ObjectRenderers.RemoveAll(r => r is CodeBlockRenderer);
        Chart((_, _) => new Button()).Register(renderer);

        renderer.Render(document);

        Assert.IsType<CustomFenceRenderer>(renderer.ObjectRenderers[^1]);
        Assert.IsType<Button>(CustomFence(renderer.RootPanel).Child);
    }

    [AvaloniaFact]
    public void Other_blocks_go_to_the_next_renderer_that_accepts_them_even_when_it_is_not_adjacent()
    {
        var document = Markdown.Parse("```python\nx\n```");
        var renderer = new AvaloniaRenderer();
        renderer.ObjectRenderers.Insert(0, new CustomFenceRenderer());

        renderer.Render(document);

        Assert.Contains("language-python", CodeBlockAssert.CodeBorder(Assert.Single(renderer.RootPanel.Children)).Classes);
    }

    [AvaloniaFact]
    public void Without_registrations_no_fence_renderer_is_installed()
    {
        var renderer = Renderer("```chart\nx\n```", null);

        Assert.DoesNotContain(renderer.ObjectRenderers, r => r is CustomFenceRenderer);
    }

    [Fact]
    public void Extension_exposes_its_language_and_factory()
    {
        FenceRendererFactory factory = (_, _) => null;

        var extension = new FenceRendererExtension("chart", factory);

        Assert.Equal("chart", extension.Language);
        Assert.Same(factory, extension.Factory);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Extension_rejects_a_blank_language(string language) =>
        Assert.Throws<ArgumentException>(() => new FenceRendererExtension(language, (_, _) => null));

    [Fact]
    public void Extension_rejects_a_null_language_or_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new FenceRendererExtension(null!, (_, _) => null));
        Assert.Throws<ArgumentNullException>(() => new FenceRendererExtension("chart", null!));
    }
}
