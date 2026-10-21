// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using Markdig;
using Markdig.Renderers.Html;
using LiteralInline = Markdig.Syntax.Inlines.LiteralInline;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

/// <summary>Plain text written in a row (literals, soft breaks, entities, SmartyPants, delimiters) shares one <see cref="Run"/>.</summary>
public class PlainTextRunTests : RenderTestBase
{
    private static List<Inline> Inlines(string markdown, MarkdownPipeline? pipeline = null)
    {
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Render(markdown, pipeline).Children));
        return [.. textBlock.Inlines!];
    }

    [AvaloniaFact]
    public void A_paragraph_of_literals_breaks_escapes_and_entities_is_one_run()
    {
        var run = Assert.IsType<Run>(Assert.Single(Inlines("one\\*two\nthree &amp; four [five")));

        Assert.Equal("one*two three & four [five", run.Text);
    }

    [AvaloniaFact]
    public void SmartyPants_replacements_join_the_text_around_them()
    {
        var pipeline = new MarkdownPipelineBuilder().UseSmartyPants().Build();

        var run = Assert.IsType<Run>(Assert.Single(Inlines("\"quoted\" -- text", pipeline)));

        Assert.Equal("“quoted” – text", run.Text);
    }

    [AvaloniaFact]
    public void Text_around_other_inlines_keeps_a_run_on_each_side()
    {
        var inlines = Inlines("before *middle* after\nnext");

        Assert.Equal(3, inlines.Count);
        Assert.Equal("before ", Assert.IsType<Run>(inlines[0]).Text);
        Assert.Equal("middle", Assert.IsType<Run>(Assert.Single(Assert.IsType<Italic>(inlines[1]).Inlines)).Text);
        Assert.Equal(" after next", Assert.IsType<Run>(inlines[2]).Text);
    }

    [AvaloniaFact]
    public void Text_after_a_hard_break_starts_a_new_run()
    {
        var inlines = Inlines("one  \ntwo");

        Assert.Equal(["one", "two"], inlines.OfType<Run>().Select(r => r.Text));
        Assert.IsType<LineBreak>(inlines[1]);
    }

    [AvaloniaFact]
    public void Text_with_attribute_classes_gets_a_run_of_its_own()
    {
        var renderer = new AvaloniaRenderer { ApplyAttributeClasses = true };
        new MarkdownPipelineBuilder().Build().Setup(renderer);
        var classed = new LiteralInline("two");
        classed.GetAttributes().AddClass("note");
        var span = new Span();

        renderer.Push(span.Inlines);
        renderer.Write(new LiteralInline("one "));
        renderer.Write(classed);
        renderer.Write(new LiteralInline(" three"));
        renderer.Pop();

        var runs = span.Inlines.Cast<Run>().ToList();
        Assert.Equal(["one ", "two", " three"], runs.Select(r => r.Text));
        Assert.Equal(["note"], runs[1].Classes);
        Assert.Empty(runs[2].Classes);
    }

    [AvaloniaFact]
    public void Text_after_an_inline_added_directly_starts_a_new_run()
    {
        var renderer = new AvaloniaRenderer();
        new MarkdownPipelineBuilder().Build().Setup(renderer);
        var span = new Span();

        renderer.Push(span.Inlines);
        renderer.Write(new LiteralInline("one"));
        span.Inlines.Add(new Run("two"));
        renderer.Write(new LiteralInline("three"));
        renderer.Pop();

        Assert.Equal(["one", "two", "three"], span.Inlines.Cast<Run>().Select(r => r.Text));
    }

    [AvaloniaFact]
    public void An_empty_literal_adds_no_run()
    {
        var renderer = new AvaloniaRenderer();
        new MarkdownPipelineBuilder().Build().Setup(renderer);
        var span = new Span();

        renderer.Push(span.Inlines);
        renderer.Write(new LiteralInline(""));
        renderer.Pop();

        Assert.Empty(span.Inlines);
    }

    [AvaloniaFact]
    public void Text_written_outside_an_inline_container_is_dropped()
    {
        var renderer = new AvaloniaRenderer();
        new MarkdownPipelineBuilder().Build().Setup(renderer);
        var panel = new StackPanel();

        renderer.Push(panel);
        renderer.Write(new LiteralInline("text"));
        renderer.Pop();

        Assert.Empty(panel.Children);
    }
}
