// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using Markdig.Extensions.SmartyPants;
using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class SmartyPantsTests : RenderTestBase
{
    private static string Text(Panel root) =>
        string.Concat(Assert.IsAssignableFrom<TextBlock>(Assert.Single(root.Children)).Inlines!.OfType<Run>().Select(r => r.Text));

    [AvaloniaTheory]
    [InlineData("\"Quoted\"", "“Quoted”")]
    [InlineData("'single'", "‘single’")]
    [InlineData("a -- b", "a – b")]
    [InlineData("a --- b", "a — b")]
    [InlineData("wait...", "wait…")]
    [InlineData("<<guillemets>>", "«guillemets»")]
    public void Punctuation_renders_as_typographic_characters(string markdown, string expected)
    {
        var root = Render(markdown, new MarkdownPipelineBuilder().UseSmartyPants().Build());

        Assert.Equal(expected, Text(root));
    }

    [AvaloniaFact]
    public void Viewer_uses_the_replacements_of_the_pipeline()
    {
        var options = new SmartyPantOptions();
        options.Mapping[SmartyPantType.LeftDoubleQuote] = "&laquo; ";
        options.Mapping[SmartyPantType.RightDoubleQuote] = " &raquo;";
        var viewer = new MarkdownViewer
        {
            Pipeline = new MarkdownPipelineBuilder().UseSmartyPants(options).Build(),
            Markdown = "\"Bonjour\"",
        };

        var root = Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);
        Assert.Equal("« Bonjour »", Text(root));
    }

    [AvaloniaFact]
    public void Punctuation_without_a_replacement_keeps_its_source_text()
    {
        var options = new SmartyPantOptions();
        options.Mapping.Remove(SmartyPantType.Dash2);
        var viewer = new MarkdownViewer
        {
            Pipeline = new MarkdownPipelineBuilder().UseSmartyPants(options).Build(),
            Markdown = "a -- b",
        };

        var root = Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);
        Assert.Equal("a -- b", Text(root));
    }
}
