// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

using Markdig.Extensions.TaskLists;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="TaskList"/> as a disabled <see cref="CheckBox"/>.
/// </summary>
public class TaskListRenderer : AvaloniaObjectRenderer<TaskList>
{
    protected override void Write(AvaloniaRenderer renderer, TaskList obj)
    {
        var checkBox = new CheckBox
        {
            IsChecked = obj.Checked,
            IsEnabled = false,
        };
        checkBox.Classes.Add("markdown-task-list");

        renderer.WriteInline(checkBox);
    }
}
