// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

public class MathBlockRendererTests
{
    private static StackPanel Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseMathematics().Build();
        var document = Markdown.Parse(markdown, pipeline);
        var renderer = new AvaloniaRenderer();
        renderer.ObjectRenderers.Insert(0, new MathBlockRenderer());
        pipeline.Setup(renderer);
        renderer.Render(document);
        return renderer.RootPanel;
    }

    [AvaloniaFact]
    public void Math_block_renders_border_with_math_block_class()
    {
        var result = Render("$$\nx^2\n$$");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-math-block", border.Classes);
    }

    [AvaloniaFact]
    public void Math_block_border_contains_image()
    {
        var result = Render("$$\nx^2\n$$");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.IsType<Image>(border.Child);
    }

    [AvaloniaFact]
    public void Math_block_image_has_non_null_bitmap_source()
    {
        var result = Render("$$\nx^2\n$$");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        var image = Assert.IsType<Image>(border.Child);
        Assert.NotNull(image.Source);
    }

    [AvaloniaFact]
    public void Math_block_with_pathologically_nested_braces_falls_back_instead_of_crashing()
    {
        var nested = new string('{', 60) + "x" + new string('}', 60); // 60 > MaxBraceNestingDepth (50) — throws before ever touching CSharpMath
        var result = Render($"$$\n{nested}\n$$");
        var border = Assert.IsType<Border>(Assert.Single(result.Children));
        Assert.Contains("markdown-math-fallback", border.Classes);
    }

    private const string FormulaMarkdown = """
        $$
        x^2
        $$
        """;

    private static Image BlockImage(StackPanel root) =>
        Assert.IsType<Image>(Assert.IsType<Border>(Assert.Single(root.Children)).Child);

    [AvaloniaFact]
    public void Theme_switch_re_bakes_the_formula_image()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(FormulaMarkdown);
        var image = BlockImage(root);
        var window = new Window { Content = root };
        try
        {
            window.Show();
            var lightBitmap = image.Source;
            Assert.Equal(Stretch.None, image.Stretch);
            Assert.Equal(HorizontalAlignment.Center, image.HorizontalAlignment);

            theme.Switch(ThemeVariant.Dark);

            Assert.NotNull(image.Source);
            Assert.NotSame(lightBitmap, image.Source);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Never_attached_block_does_not_re_bake_on_theme_change()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var image = BlockImage(Render(FormulaMarkdown));
        var before = image.Source;

        theme.Switch(ThemeVariant.Dark);

        Assert.Same(before, image.Source);
    }

    [AvaloniaFact]
    public void Empty_math_block_renders_an_image_without_a_fallback()
    {
        var border = Assert.IsType<Border>(Assert.Single(Render("""
            $$
            $$
            """).Children));

        Assert.IsType<Image>(border.Child);
        Assert.Contains("markdown-math-block", border.Classes);
    }

    [AvaloniaFact]
    public void Unrelated_application_property_change_does_not_re_bake()
    {
        var app = Application.Current!;
        var image = BlockImage(Render(FormulaMarkdown));
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
    public async Task Block_detached_during_a_theme_switch_re_bakes_on_reattach()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render(FormulaMarkdown);
        var image = BlockImage(root);
        var window = new Window { Content = root };
        try
        {
            window.Show();
            var before = image.Source;

            window.Content = null;
            theme.Switch(ThemeVariant.Dark);
            await AsyncTestHelpers.PumpAsync();
            Assert.Same(before, image.Source);

            window.Content = root;
            Assert.NotSame(before, image.Source);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fallback_shows_the_error_message_and_the_full_multi_line_source()
    {
        var nested = new string('{', 60) + "x" + new string('}', 60);
        var markdown = $"""
            $$
            {nested}
            y
            $$
            """;
        var border = Assert.IsType<Border>(Assert.Single(Render(markdown).Children));

        var panel = Assert.IsType<StackPanel>(border.Child);
        Assert.Equal(4, panel.Spacing);
        var texts = panel.Children.Cast<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(
            ["Math render error: LaTeX source exceeds the 50-level brace-nesting safety limit.", $"{nested}\ny"],
            texts);
        Assert.Equal(["markdown-math-fallback"], border.Classes.Where(c => !c.StartsWith(':')).ToArray());
    }
}
