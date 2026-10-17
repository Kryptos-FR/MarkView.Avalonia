// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.SyntaxHighlighting.Tests;

/// <summary>
/// The line-number, wrapping and maximum-height options of the code block toolbar applied to
/// highlighted code blocks.
/// </summary>
public class TextMateCodeBlockOptionsTests
{
    private const string Code = "```csharp\npublic class Foo { }\nvar x = 1;\n```";

    private static (StackPanel Root, Border Border) Render(bool lineNumbers, bool wrap, double maxHeight)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer
        {
            ShowCodeBlockLineNumbers = lineNumbers,
            WrapCodeBlockLines = wrap,
            CodeBlockMaxHeight = maxHeight,
        };
        new TextMateExtension().Register(renderer);
        pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(Code, pipeline));
        return (renderer.RootPanel, CodeBlockAssert.CodeBorder(Assert.Single(renderer.RootPanel.Children)));
    }

    private static string[] Colours(TextBlock text) =>
        text.Inlines!.OfType<Run>()
            .Select(r => r.IsSet(TextElement.ForegroundProperty) ? r.Foreground!.ToString()! : "")
            .ToArray();

    [AvaloniaTheory]
    [InlineData(true, false, double.PositiveInfinity)]
    [InlineData(false, true, double.PositiveInfinity)]
    [InlineData(false, false, 60)]
    [InlineData(true, true, 60)]
    public void Highlighted_code_gets_the_code_block_options(bool lineNumbers, bool wrap, double maxHeight)
    {
        var (_, border) = Render(lineNumbers, wrap, maxHeight);

        var (scroll, numbers, code) = CodeBlockAssert.Layout(border);
        Assert.Equal(!double.IsPositiveInfinity(maxHeight), scroll is not null);
        Assert.Equal(maxHeight, border.MaxHeight);
        Assert.Equal(lineNumbers ? "1\n2" : null, numbers?.Text);
        Assert.Equal(wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, code.TextWrapping);
        Assert.Contains(code.Inlines!.OfType<Run>(), r => r.IsSet(TextElement.ForegroundProperty));
        Assert.Contains("language-csharp", border.Classes);
    }

    [AvaloniaFact]
    public void Theme_switch_recolours_line_numbered_scrolling_code_in_place()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var (root, border) = Render(lineNumbers: true, wrap: true, maxHeight: 60);
        var (_, numbers, code) = CodeBlockAssert.Layout(border);
        var window = new Window { Content = root };
        try
        {
            window.Show();
            var light = Colours(code);
            var firstRun = code.Inlines![0];

            theme.Switch(ThemeVariant.Dark);

            Assert.Same(firstRun, code.Inlines![0]);
            Assert.NotEqual(light, Colours(code));
            Assert.Equal("1\n2", numbers!.Text);
            Assert.Same(code, CodeBlockAssert.Layout(border).Code);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_everything_copies_highlighted_code_without_its_line_numbers()
    {
        var viewer = new MarkdownViewer
        {
            ShowCodeBlockLineNumbers = true,
            WrapCodeBlockLines = true,
            CodeBlockMaxHeight = 40,
        }.UseTextMateHighlighting();
        viewer.Markdown = "Intro\n\n" + Code + "\n\nOutro";
        var code = CodeBlockAssert.Layout(viewer.GetLogicalDescendants().OfType<Border>()
            .Single(b => b.Classes.Contains("markdown-code-block"))).Code;
        Assert.Contains(code.Inlines!.OfType<Run>(), r => r.IsSet(TextElement.ForegroundProperty));

        viewer.SelectAll();

        Assert.Equal("Intro\npublic class Foo { }\nvar x = 1;\nOutro", viewer.GetSelectedText());
    }
}
