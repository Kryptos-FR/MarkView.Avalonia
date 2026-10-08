// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;

using Markdig;
using Markdig.Syntax;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Inlines;

public class TaskListTests : RenderTestBase
{
    [AvaloniaFact]
    public void Unchecked_task_renders_as_unchecked_checkbox()
    {
        var pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
        var result = Render("- [ ] Todo item", pipeline);

        var marker = FindTaskMarker(result);
        Assert.NotNull(marker);
        Assert.Equal("\u2610", marker!.Text);
    }

    [AvaloniaFact]
    public void Checked_task_renders_as_checked_checkbox()
    {
        var pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
        var result = Render("- [x] Done item", pipeline);

        var marker = FindTaskMarker(result);
        Assert.NotNull(marker);
        Assert.Equal("\u2611", marker!.Text);
    }

    [AvaloniaFact]
    public void Task_list_skip_request_suppresses_the_glyph_only_once()
    {
        var pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
        var paragraph = Markdown.Parse("- [x] done", pipeline).Descendants<ParagraphBlock>().Single();
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);
        renderer.SkipNextTaskList = true;

        renderer.Render(paragraph);
        renderer.Render(paragraph);

        Assert.False(renderer.SkipNextTaskList);
        var texts = renderer.RootPanel.Children.OfType<MarkdownSelectableTextBlock>().ToArray();
        Assert.Equal(2, texts.Length);
        Assert.DoesNotContain(texts[0].Inlines!.OfType<Run>(), r => r.Classes.Contains("markdown-task-list"));
        Assert.Single(texts[1].Inlines!.OfType<Run>(), r => r.Classes.Contains("markdown-task-list"));
    }

    [AvaloniaTheory]
    [InlineData("- [x] done", "\u2611")]
    [InlineData("- [ ] todo", "\u2610")]
    public void Task_list_rendered_outside_a_list_falls_back_to_an_inline_glyph(string markdown, string glyph)
    {
        var pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
        var paragraph = Markdown.Parse(markdown, pipeline).Descendants<ParagraphBlock>().Single();
        var renderer = new AvaloniaRenderer();
        pipeline.Setup(renderer);

        renderer.Render(paragraph);

        var text = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(renderer.RootPanel.Children));
        var run = text.Inlines!.OfType<Run>().First();
        Assert.Equal(glyph, run.Text);
        Assert.Contains("markdown-task-list", run.Classes);
    }

    private static TextBlock? FindTaskMarker(Control root)
    {
        if (root is TextBlock tb && tb.Classes.Contains("markdown-task-list"))
            return tb;
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                var found = FindTaskMarker(child);
                if (found != null) return found;
            }
        }
        if (root is ContentControl cc && cc.Content is Control content)
            return FindTaskMarker(content);
        if (root is Decorator dec && dec.Child is Control decChild)
            return FindTaskMarker(decChild);
        return null;
    }
}
