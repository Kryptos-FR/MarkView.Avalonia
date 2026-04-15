// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using Markdig;

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
