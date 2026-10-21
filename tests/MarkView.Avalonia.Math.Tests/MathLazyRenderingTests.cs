// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

/// <summary>Formulas with <see cref="AvaloniaRenderer.IsLazyRenderingEnabled"/>.</summary>
public class MathLazyRenderingTests
{
    private static StackPanel Render(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseMathematics().Build();
        var renderer = new AvaloniaRenderer { IsLazyRenderingEnabled = true };
        renderer.ObjectRenderers.Insert(0, new MathBlockRenderer());
        renderer.ObjectRenderers.Add(new MathInlineRenderer());
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    // The document 1000 px below the top of a 300 px window, so more than a viewport height away
    // until the window scrolls.
    private sealed class FarBelow : IDisposable
    {
        private readonly ScrollViewer _scrollViewer;

        public Window Window { get; }

        public FarBelow(Control document)
        {
            _scrollViewer = new ScrollViewer { Content = new StackPanel { Children = { new Border { Height = 1000 }, document } } };
            Window = new Window { Width = 400, Height = 300, Content = _scrollViewer };
            Window.Show();
        }

        public void ScrollToEnd()
        {
            _scrollViewer.ScrollToEnd();
            Window.UpdateLayout();
        }

        public void Dispose() => Window.Close();
    }

    private static Image BlockImage(StackPanel root) =>
        Assert.IsType<Image>(MathBlockAssert.MathBorder(Assert.Single(root.Children)).Child);

    private static Image InlineImage(StackPanel root) =>
        Assert.IsType<Image>(Assert.Single(Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children))
            .Inlines!.OfType<InlineUIContainer>()).Child);

    [AvaloniaFact]
    public void Block_far_below_the_visible_area_is_baked_once_scrolled_near()
    {
        var root = Render("$$\nx^2\n$$");
        var image = BlockImage(root);
        Assert.Null(image.Source);
        using var shown = new FarBelow(root);
        Assert.Null(image.Source);

        shown.ScrollToEnd();

        Assert.NotNull(image.Source);
    }

    [AvaloniaFact]
    public void Block_in_view_is_baked_before_it_is_first_rendered()
    {
        var root = Render("$$\nx^2\n$$");
        var image = BlockImage(root);
        var window = new Window { Width = 400, Height = 300, Content = root };
        try
        {
            window.Show();

            Assert.NotNull(image.Source);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Block_is_not_baked_by_a_theme_switch_before_it_came_near_but_follows_the_theme_after()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render("$$\nx^2\n$$");
        var image = BlockImage(root);
        using var shown = new FarBelow(root);

        theme.Switch(ThemeVariant.Dark);
        Assert.Null(image.Source);

        shown.ScrollToEnd();
        var baked = image.Source;
        Assert.NotNull(baked);

        theme.Switch(ThemeVariant.Light);
        Assert.NotSame(baked, image.Source);
    }

    [AvaloniaFact]
    public void Block_over_the_safety_limits_shows_its_fallback_once_near()
    {
        var root = Render($"$$\n{new string('{', 60)}x{new string('}', 60)}\n$$");
        var border = MathBlockAssert.MathBorder(Assert.Single(root.Children));
        Assert.IsType<Image>(border.Child);
        using var shown = new FarBelow(root);

        shown.ScrollToEnd();

        Assert.Contains("markdown-math-fallback", border.Classes);
    }

    [AvaloniaFact]
    public void Inline_formula_far_below_the_visible_area_is_baked_once_scrolled_near()
    {
        var root = Render("Einstein said $E=mc^2$ once.");
        var image = InlineImage(root);
        using var shown = new FarBelow(root);
        Assert.Null(image.Source);

        shown.ScrollToEnd();

        Assert.NotNull(image.Source);
    }

    [AvaloniaFact]
    public void Inline_formula_is_not_baked_by_a_theme_switch_before_it_came_near_but_follows_the_theme_after()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var root = Render("Einstein said $E=mc^2$ once.");
        var image = InlineImage(root);
        using var shown = new FarBelow(root);

        theme.Switch(ThemeVariant.Dark);
        Assert.Null(image.Source);

        shown.ScrollToEnd();
        var baked = image.Source;
        Assert.NotNull(baked);

        theme.Switch(ThemeVariant.Light);
        Assert.NotSame(baked, image.Source);
    }

    [AvaloniaFact]
    public void Inline_formula_over_the_safety_limits_becomes_plain_text_at_once()
    {
        var source = $"{new string('{', 60)}x{new string('}', 60)}";
        var root = Render($"Before ${source}$ after.");

        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(root.Children));
        Assert.Empty(paragraph.Inlines!.OfType<InlineUIContainer>());
        Assert.Contains(paragraph.Inlines!.OfType<Run>(), r => r.Text == source);
    }
}
