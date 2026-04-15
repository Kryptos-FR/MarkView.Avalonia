// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Markdig;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Inlines;
using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownExtensionsTests : RenderTestBase
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

    [AvaloniaFact]
    public void UseSupportedExtensions_enables_task_lists()
    {
        var result = Render("- [x] done\n- [ ] todo", _pipeline);
        var listPanel = Assert.IsType<StackPanel>(Assert.Single(result.Children));
        Assert.Contains("markdown-list", listPanel.Classes);
        Assert.Equal(2, listPanel.Children.Count);
        // Task list items use unicode ☐/☑ TextBlock markers with class markdown-task-list
        var marker = FindTaskMarker(listPanel);
        Assert.NotNull(marker);
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

    [AvaloniaFact]
    public void UseSupportedExtensions_enables_pipe_tables()
    {
        var result = Render("| A | B |\n|---|---|\n| 1 | 2 |", _pipeline);
        var grid = Assert.IsType<Grid>(Assert.Single(result.Children));
        Assert.Contains("markdown-table", grid.Classes);
    }

    [AvaloniaFact]
    public void UseSupportedExtensions_enables_autolinks()
    {
        // UseAutoLinks turns bare URLs into MarkdownHyperlink spans
        var result = Render("Visit https://example.com today", _pipeline);
        var textBlock = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(result.Children));
        var inlines = textBlock.Inlines!.ToList();
        Assert.Contains(inlines, i => i is MarkdownHyperlink);
    }

    private static T? FindFirst<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        if (root is Panel p) foreach (var child in p.Children) { var f = FindFirst<T>(child); if (f != null) return f; }
        if (root is ContentControl cc && cc.Content is Control c) return FindFirst<T>(c);
        if (root is Decorator d && d.Child is Control dc) return FindFirst<T>(dc);
        if (root is TextBlock tb && tb.Inlines != null)
        {
            foreach (var inline in tb.Inlines)
            {
                if (inline is InlineUIContainer iuc && iuc.Child is Control iucChild)
                {
                    var f = FindFirst<T>(iucChild);
                    if (f != null) return f;
                }
            }
        }
        return null;
    }
}
