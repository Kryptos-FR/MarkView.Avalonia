// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using TextMateSharp.Grammars;

using Xunit;

namespace MarkView.Avalonia.SyntaxHighlighting.Tests;

public class TextMateThemeSwitchTests
{
    private const string Code = """
        ```csharp
        public class Foo { }
        ```
        """;

    private static (StackPanel Root, TextBlock Text) Render()
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer();
        new TextMateExtension().Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(Code, pipeline));
        var border = Assert.IsType<Border>(Assert.Single(renderer.RootPanel.Children));
        return (renderer.RootPanel, Assert.IsType<TextBlock>(border.Child));
    }

    // Only the brushes the highlighter set: a token without one inherits the TextBlock
    // foreground, which follows the theme on its own once the block is in a window.
    private static string[] Colours(TextBlock text) =>
        text.Inlines!.OfType<Run>()
            .Select(r => r.IsSet(TextElement.ForegroundProperty) ? r.Foreground!.ToString()! : "")
            .ToArray();

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initial_render_uses_the_current_theme_variant(bool dark)
    {
        using var theme = new ThemeScope(dark ? ThemeVariant.Dark : ThemeVariant.Light);
        var expected = new TextMateHighlighter(dark ? ThemeName.DarkPlus : ThemeName.LightPlus)
            .Highlight("public class Foo { }".AsMemory(), "csharp")!
            .Select(t => t.Foreground?.ToString() ?? "");

        Assert.Equal(expected, Colours(Render().Text));
    }

    [AvaloniaFact]
    public void Theme_switch_recolours_tokens_in_place()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (root, text) = Render();
        var window = new Window { Content = root };
        try
        {
            window.Show();
            var light = Colours(text);
            var inlineCount = text.Inlines!.Count;

            theme.Switch(ThemeVariant.Dark);

            Assert.Equal(inlineCount, text.Inlines!.Count);
            Assert.NotEqual(light, Colours(text));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Never_attached_code_block_does_not_follow_theme_changes()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, text) = Render();
        var light = Colours(text);

        theme.Switch(ThemeVariant.Dark);

        Assert.Equal(light, Colours(text));
    }

    [AvaloniaFact]
    public async Task Code_block_detached_during_a_theme_switch_recolours_on_reattach()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (root, text) = Render();
        var window = new Window { Content = root };
        try
        {
            window.Show();
            var light = Colours(text);

            window.Content = null;
            theme.Switch(ThemeVariant.Dark);
            await AsyncTestHelpers.PumpAsync();
            Assert.Equal(light, Colours(text));

            window.Content = root;
            Assert.NotEqual(light, Colours(text));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unrelated_application_property_change_leaves_tokens_untouched()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (_, text) = Render();
        var firstRun = text.Inlines![0];
        var savedName = Application.Current!.Name;
        try
        {
            Application.Current.Name = "UnrelatedPropertyChange";

            Assert.Same(firstRun, text.Inlines[0]);
        }
        finally
        {
            Application.Current.Name = savedName;
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DualTheme_Highlight_follows_the_application_theme(bool dark)
    {
        using var theme = new ThemeScope(dark ? ThemeVariant.Dark : ThemeVariant.Light);
        var highlighter = new DualThemeTextMateHighlighter(new TextMateHighlighter(ThemeName.DarkPlus), new TextMateHighlighter(ThemeName.LightPlus));
        var line = "public class Foo { }".AsMemory();

        var viaTheme = highlighter.Highlight(line, "csharp")!.Select(t => t.Foreground?.ToString());
        var explicitVariant = highlighter.HighlightVariant(line, "csharp", dark)!.Select(t => t.Foreground?.ToString());

        Assert.Equal(explicitVariant, viaTheme);
    }
}
