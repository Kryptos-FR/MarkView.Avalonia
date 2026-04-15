// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Documents;

using Markdig.Extensions.TaskLists;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// Renders a Markdig <see cref="TaskList"/> as a disabled <see cref="CheckBox"/>.
/// </summary>
public class TaskListRenderer : AvaloniaObjectRenderer<TaskList>
{
    protected override void Write(AvaloniaRenderer renderer, TaskList obj)
    {
        // ListRenderer places the marker in column 0 of the list-item grid and sets
        // this flag so we don't emit a duplicate inline.
        if (renderer.SkipNextTaskList)
        {
            renderer.SkipNextTaskList = false;
            return;
        }

        // Fallback: render inline (outside a list context).
        var run = new Run
        {
            Text = obj.Checked ? "\u2611" : "\u2610",
        };
        run.Classes.Add("markdown-task-list");
        renderer.WriteInline(run);
    }
}
