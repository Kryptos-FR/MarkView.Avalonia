// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class DefinitionListTests : RenderTestBase
{
    private static MarkdownPipeline DefinitionListPipeline() =>
        new MarkdownPipelineBuilder().UseDefinitionLists().Build();

    private static StackPanel RenderList(string markdown)
    {
        var list = Assert.IsType<StackPanel>(Assert.Single(Render(markdown, DefinitionListPipeline()).Children));
        Assert.Contains("markdown-definition-list", list.Classes);
        return list;
    }

    private static string Text(Control control) =>
        string.Concat(Assert.IsAssignableFrom<TextBlock>(control).Inlines!.Select(i => i switch
        {
            Run run => run.Text,
            Span span => string.Concat(span.Inlines.OfType<Run>().Select(r => r.Text)),
            _ => "",
        }));

    [AvaloniaFact]
    public void Term_and_definition_render_in_order()
    {
        var list = RenderList("""
            Apple
            :   A fruit
            """);

        Assert.Equal(2, list.Children.Count);
        var term = Assert.IsType<MarkdownSelectableTextBlock>(list.Children[0]);
        Assert.Contains("markdown-definition-term", term.Classes);
        Assert.Equal("Apple", Text(term));

        var definition = Assert.IsType<StackPanel>(list.Children[1]);
        Assert.Contains("markdown-definition", definition.Classes);
        Assert.Equal("A fruit", Text(Assert.Single(definition.Children)));
    }

    [AvaloniaFact]
    public void Term_keeps_its_inline_formatting()
    {
        var list = RenderList("""
            **Bold** term
            :   Definition
            """);

        var term = Assert.IsType<MarkdownSelectableTextBlock>(list.Children[0]);
        Assert.IsType<Bold>(term.Inlines![0]);
        Assert.Equal("Bold term", Text(term));
    }

    [AvaloniaFact]
    public void Each_definition_of_a_term_gets_its_own_panel()
    {
        var list = RenderList("""
            Term
            :   First
            :   Second
            """);

        Assert.Equal(3, list.Children.Count);
        Assert.Equal("First", Text(Assert.Single(Assert.IsType<StackPanel>(list.Children[1]).Children)));
        Assert.Equal("Second", Text(Assert.Single(Assert.IsType<StackPanel>(list.Children[2]).Children)));
    }

    [AvaloniaFact]
    public void Several_terms_share_one_definition()
    {
        var list = RenderList("""
            One
            Two
            :   Shared
            """);

        Assert.Equal(["One", "Two"], list.Children.Take(2).Select(Text));
        Assert.Contains("markdown-definition", list.Children[2].Classes);
    }

    [AvaloniaFact]
    public void Definition_holds_all_of_its_blocks()
    {
        var list = RenderList("""
            Term
            :   First paragraph

                Second paragraph
            """);

        var definition = Assert.IsType<StackPanel>(list.Children[1]);
        Assert.Equal(["First paragraph", "Second paragraph"], definition.Children.Select(Text));
    }

    [AvaloniaFact]
    public void Terms_after_a_definition_start_a_new_entry()
    {
        var list = RenderList("""
            First
            :   One

            Second
            :   Two
            """);

        Assert.Equal(4, list.Children.Count);
        Assert.Equal("Second", Text(list.Children[2]));
        Assert.Equal("Two", Text(Assert.Single(Assert.IsType<StackPanel>(list.Children[3]).Children)));
    }
}
