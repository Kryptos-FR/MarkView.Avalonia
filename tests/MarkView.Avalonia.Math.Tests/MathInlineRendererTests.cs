// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class MathInlineRendererTests
{
    private static StackPanel Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseMathematics().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer();
        renderer.ObjectRenderers.Add(new MathInlineRenderer());
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    [AvaloniaFact]
    public void Inline_math_renders_inside_InlineUIContainer()
    {
        var result = Render("Einstein said $E=mc^2$ once.");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var container = textBlock.Inlines!.OfType<InlineUIContainer>().Single();
        Assert.IsType<Image>(container.Child);
    }

    [AvaloniaFact]
    public void Inline_math_image_has_math_inline_class()
    {
        var result = Render("$x^2$");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var container = textBlock.Inlines!.OfType<InlineUIContainer>().Single();
        var image = Assert.IsType<Image>(container.Child);
        Assert.Contains("markdown-math-inline", image.Classes);
    }

    [AvaloniaFact]
    public void Inline_math_image_has_non_null_bitmap_source()
    {
        var result = Render("$x^2$");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var container = textBlock.Inlines!.OfType<InlineUIContainer>().Single();
        var image = Assert.IsType<Image>(container.Child);
        Assert.NotNull(image.Source);
    }

    [AvaloniaFact]
    public void Inline_math_first_render_failure_falls_back_to_source_text()
    {
        var nested = new string('{', 60) + "x" + new string('}', 60);
        var result = Render($"${nested}$");
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var run = Assert.IsType<Run>(Assert.Single(textBlock.Inlines!));
        Assert.Equal(nested, run.Text);
    }

    private static Image InlineImage(StackPanel root) =>
        Assert.IsType<Image>(Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children))
            .Inlines!.OfType<InlineUIContainer>().Single().Child);

    [AvaloniaFact]
    public void Theme_switch_re_bakes_inline_formula()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var image = InlineImage(Render("$x^2$"));
        var lightBitmap = image.Source;
        Assert.Equal(Stretch.None, image.Stretch);

        theme.Switch(ThemeVariant.Dark);

        Assert.NotSame(lightBitmap, image.Source);
    }

    [AvaloniaFact]
    public void Unrelated_application_property_change_does_not_re_bake_inline_formula()
    {
        var app = Application.Current!;
        var image = InlineImage(Render("$x^2$"));
        var before = image.Source;
        var savedName = app.Name;
        try
        {
            app.Name = "renamed-for-test";
            Assert.Same(before, image.Source);
        }
        finally
        {
            app.Name = savedName;
        }
    }

    [AvaloniaFact]
    public async Task Inline_formula_removed_from_tree_stops_re_baking()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render("$x^2$");
        var image = InlineImage(root);
        var window = new Window { Content = root };
        try
        {
            window.Show();
            var before = image.Source;

            window.Content = null;
            theme.Switch(ThemeVariant.Dark);
            await AsyncTestHelpers.PumpAsync();

            Assert.Same(before, image.Source);
        }
        finally
        {
            window.Close();
        }
    }
}
