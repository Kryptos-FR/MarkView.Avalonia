using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

        var checkbox = FindFirst<CheckBox>(result);
        Assert.NotNull(checkbox);
        Assert.False(checkbox.IsChecked);
        Assert.False(checkbox.IsEnabled);
    }

    [AvaloniaFact]
    public void Checked_task_renders_as_checked_checkbox()
    {
        var pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();
        var result = Render("- [x] Done item", pipeline);

        var checkbox = FindFirst<CheckBox>(result);
        Assert.NotNull(checkbox);
        Assert.True(checkbox.IsChecked);
    }

    private static T? FindFirst<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                var found = FindFirst<T>(child);
                if (found != null) return found;
            }
        }
        if (root is ContentControl cc && cc.Content is Control content)
            return FindFirst<T>(content);
        if (root is Decorator dec && dec.Child is Control decChild)
            return FindFirst<T>(decChild);
        // Traverse TextBlock inlines for InlineUIContainer
        if (root is TextBlock tb && tb.Inlines != null)
        {
            foreach (var inline in tb.Inlines)
            {
                if (inline is InlineUIContainer iuc && iuc.Child is Control iucChild)
                {
                    var found = FindFirst<T>(iucChild);
                    if (found != null) return found;
                }
            }
        }
        return null;
    }
}
