// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests;

public class MarkdownViewerTaskListTests
{
    private const string TwoTasks = """
        - [x] done
        - [ ] todo
        """;

    private static ToggleButton[] Toggles(MarkdownViewer viewer) =>
        viewer.Content is Control content ? content.GetLogicalDescendants().OfType<ToggleButton>().ToArray() : [];

    private static TextBlock[] GlyphMarkers(MarkdownViewer viewer) =>
        viewer.Content is Control content
            ? content.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("markdown-task-list")).ToArray()
            : [];

    private static List<TaskToggledEventArgs> RecordToggles(MarkdownViewer viewer)
    {
        var events = new List<TaskToggledEventArgs>();
        viewer.TaskToggled += (_, e) => events.Add(e);
        return events;
    }

    // ── Property ──────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Task_lists_are_not_interactive_by_default()
    {
        var viewer = new MarkdownViewer { Markdown = TwoTasks };

        Assert.False(viewer.AreTaskListsInteractive);
        Assert.Empty(Toggles(viewer));
        Assert.All(GlyphMarkers(viewer), g => Assert.IsType<Grid>(g.Parent));
        Assert.Equal(["☑", "☐"], GlyphMarkers(viewer).Select(g => g.Text));
    }

    [AvaloniaFact]
    public void Turning_interactivity_on_and_off_re_renders_the_markers()
    {
        var viewer = new MarkdownViewer { Markdown = TwoTasks };
        var staticContent = viewer.Content;

        viewer.AreTaskListsInteractive = true;
        var interactiveContent = viewer.Content;

        Assert.NotSame(staticContent, interactiveContent);
        Assert.Equal([true, false], Toggles(viewer).Select(t => t.IsChecked));

        viewer.AreTaskListsInteractive = false;

        Assert.NotSame(interactiveContent, viewer.Content);
        Assert.Empty(Toggles(viewer));
        Assert.Equal(["☑", "☐"], GlyphMarkers(viewer).Select(g => g.Text));
    }

    [AvaloniaFact]
    public void Interactivity_can_be_set_from_a_style()
    {
        var viewer = new MarkdownViewer { Markdown = TwoTasks };
        var window = new Window { Content = viewer };
        window.Styles.Add(new Style(x => x.OfType<MarkdownViewer>())
        {
            Setters = { new Setter(MarkdownViewer.AreTaskListsInteractiveProperty, true) },
        });
        window.Show();
        try
        {
            Assert.Equal(2, Toggles(viewer).Length);
        }
        finally
        {
            window.Close();
        }
    }

    // ── Event args ────────────────────────────────────────────────────────────

    public static TheoryData<string> TargetDocuments => new()
    {
        "- [ ] target",
        "intro\n\n- [x] first\n- [ ] target",
        // Each image size hint is rewritten one character longer per quote before parsing.
        "![](x =100x100)\n\n- [ ] target",
        "![a](x =100x100) ![b](y =20x30)\n\n- [ ] target",
        "- [ ] ![](x =1x1) first\n- [ ] target",
        "![](x =100x100)\n\n- [ ] first\n\n![](y =2x2)\n\n- [ ] target",
        // The rewrite collapses the whitespace before "=", line breaks included.
        "![](x\n=100x100)\n\n- [ ] target",
        "![](x \r\n  =100x100)\r\n\r\n- [ ] target",
        // Line breaks in the alt text are kept.
        "![a\nb](x =100x100)\n\n- [ ] target",
        // An image after the item does not shift it.
        "- [ ] target\n\n![](x =100x100)",
        // Code spans are left as they are.
        "`![](x =1x1)`\n\n- [ ] target",
        "> - [ ] quoted\n>   - [ ] target",
    };

    [AvaloniaTheory]
    [MemberData(nameof(TargetDocuments))]
    public void TaskToggled_locates_the_marker_in_the_markdown_as_set(string markdown)
    {
        var viewer = new MarkdownViewer { Markdown = markdown, AreTaskListsInteractive = true };
        var events = RecordToggles(viewer);

        Toggles(viewer)[^1].IsChecked = true;

        var e = Assert.Single(events);
        var expectedStart = markdown.IndexOf("[ ] target", StringComparison.Ordinal);
        Assert.True(e.IsChecked);
        Assert.Equal(expectedStart, e.SourceStart);
        Assert.Equal(3, e.SourceLength);
        Assert.Equal("[ ]", markdown.Substring(e.SourceStart, e.SourceLength));
        Assert.Equal(markdown.AsSpan(0, expectedStart).Count('\n'), e.Line);
    }

    [AvaloniaFact]
    public void TaskToggled_is_a_bubbling_routed_event_raised_by_the_viewer()
    {
        var viewer = new MarkdownViewer { Markdown = TwoTasks, AreTaskListsInteractive = true };
        var window = new Window { Content = viewer };
        var bubbled = new List<TaskToggledEventArgs>();
        window.AddHandler(MarkdownViewer.TaskToggledEvent, (_, e) => bubbled.Add(e));
        window.Show();
        try
        {
            Toggles(viewer)[0].IsChecked = false;

            var e = Assert.Single(bubbled);
            Assert.Same(MarkdownViewer.TaskToggledEvent, e.RoutedEvent);
            Assert.Same(viewer, e.Source);
            Assert.False(e.IsChecked);
            Assert.Equal(TwoTasks.IndexOf("[x]", StringComparison.Ordinal), e.SourceStart);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unsubscribed_handler_is_not_called()
    {
        var viewer = new MarkdownViewer { Markdown = TwoTasks, AreTaskListsInteractive = true };
        var calls = 0;
        EventHandler<TaskToggledEventArgs> handler = (_, _) => calls++;
        viewer.TaskToggled += handler;
        viewer.TaskToggled -= handler;

        Toggles(viewer)[1].IsChecked = true;

        Assert.Equal(0, calls);
    }

    [AvaloniaFact]
    public void Toggling_does_not_change_the_Markdown_property()
    {
        var viewer = new MarkdownViewer { Markdown = TwoTasks, AreTaskListsInteractive = true };
        var content = viewer.Content;

        Toggles(viewer)[1].IsChecked = true;

        Assert.Equal(TwoTasks, viewer.Markdown);
        Assert.Same(content, viewer.Content);
    }

    [AvaloniaFact]
    public void Host_can_write_each_toggle_back_to_the_source_with_the_reported_span()
    {
        const string markdown = "![](x =100x100)\n\n- [ ] one\n- [X] two\n- [ ] three";
        var viewer = new MarkdownViewer { Markdown = markdown, AreTaskListsInteractive = true };
        viewer.TaskToggled += (_, e) =>
        {
            var source = viewer.Markdown!;
            viewer.Markdown = string.Concat(
                source.AsSpan(0, e.SourceStart),
                e.IsChecked ? "[x]" : "[ ]",
                source.AsSpan(e.SourceStart + e.SourceLength));
        };

        Toggles(viewer)[0].IsChecked = true;
        Toggles(viewer)[1].IsChecked = false;

        Assert.Equal("![](x =100x100)\n\n- [x] one\n- [ ] two\n- [ ] three", viewer.Markdown);
        Assert.Equal([true, false, false], Toggles(viewer).Select(t => t.IsChecked));
    }

    [AvaloniaFact]
    public void Writing_a_toggle_back_keeps_the_rendered_document_and_its_scroll_position()
    {
        var markdown = string.Concat(Enumerable.Range(0, 60).Select(i => $"- [ ] task {i}\n"));
        var viewer = new MarkdownViewer { Markdown = markdown, AreTaskListsInteractive = true };
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        viewer.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml"),
        });
        window.Show();
        try
        {
            var scrollViewer = viewer.GetVisualDescendants().OfType<ScrollViewer>().First();
            scrollViewer.Offset = new Vector(0, 200);
            var content = viewer.Content;
            viewer.TaskToggled += (_, e) =>
            {
                var source = viewer.Markdown!;
                viewer.Markdown = string.Concat(
                    source.AsSpan(0, e.SourceStart),
                    e.IsChecked ? "[x]" : "[ ]",
                    source.AsSpan(e.SourceStart + e.SourceLength));
            };

            Toggles(viewer)[30].IsChecked = true;
            Toggles(viewer)[31].IsChecked = true;
            Toggles(viewer)[30].IsChecked = false;

            Assert.Same(content, viewer.Content);
            Assert.Equal(200, scrollViewer.Offset.Y);
            Assert.StartsWith("- [ ] task 30\n- [x] task 31\n", viewer.Markdown![viewer.Markdown.IndexOf("- [ ] task 30", StringComparison.Ordinal)..]);

            // Any other text still re-renders, and the toggled states then come from the markdown.
            viewer.Markdown += "- [ ] extra\n";
            Assert.NotSame(content, viewer.Content);
            Assert.True(Toggles(viewer)[31].IsChecked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Writing_each_toggle_back_keeps_the_rendered_document()
    {
        // The image size hint shifts every marker after it; [X] is written back as [x] once re-checked.
        const string markdown = "![](x =100x100)\n\n- [ ] one\n- [X] two\n- [ ] three";
        var viewer = new MarkdownViewer { Markdown = markdown, AreTaskListsInteractive = true };
        var content = viewer.Content;
        viewer.TaskToggled += (_, e) =>
        {
            var source = viewer.Markdown!;
            viewer.Markdown = string.Concat(
                source.AsSpan(0, e.SourceStart),
                e.IsChecked ? "[x]" : "[ ]",
                source.AsSpan(e.SourceStart + e.SourceLength));
        };

        Toggles(viewer)[0].IsChecked = true;
        Assert.Same(content, viewer.Content);
        Toggles(viewer)[1].IsChecked = false;
        Assert.Same(content, viewer.Content);
        Toggles(viewer)[1].IsChecked = true;
        Assert.Same(content, viewer.Content);
        Toggles(viewer)[2].IsChecked = true;

        Assert.Same(content, viewer.Content);
        Assert.Equal("![](x =100x100)\n\n- [x] one\n- [x] two\n- [x] three", viewer.Markdown);
        Assert.Equal([true, true, true], Toggles(viewer).Select(t => t.IsChecked));
    }

    [AvaloniaFact]
    public void Toggles_not_written_back_are_lost_when_another_text_is_assigned()
    {
        var viewer = new MarkdownViewer { Markdown = TwoTasks, AreTaskListsInteractive = true };

        Toggles(viewer)[1].IsChecked = true;
        viewer.Markdown = TwoTasks + "\n";

        Assert.Equal([true, false], Toggles(viewer).Select(t => t.IsChecked));
    }

    // ── Source documents ──────────────────────────────────────────────────────

    // "![](pixel.png =1x1)" followed by two unchecked tasks; read back to get its line endings as checked out.
    private static readonly Uri TasksDoc = new("avares://MarkView.Avalonia.Tests/TestAssets/tasks.md");

    private static string ReadTasksDoc()
    {
        using var reader = new StreamReader(AssetLoader.Open(TasksDoc));
        return reader.ReadToEnd();
    }

    [AvaloniaFact]
    public void TaskToggled_locates_the_marker_in_the_text_loaded_from_Source()
    {
        var loaded = ReadTasksDoc();
        var viewer = new MarkdownViewer { Source = TasksDoc, AreTaskListsInteractive = true };
        var events = RecordToggles(viewer);

        Toggles(viewer)[1].IsChecked = true;

        var e = Assert.Single(events);
        var expectedStart = loaded.IndexOf("[ ] second", StringComparison.Ordinal);
        Assert.Equal(expectedStart, e.SourceStart);
        Assert.Equal("[ ]", loaded.Substring(e.SourceStart, e.SourceLength));
        Assert.Equal(loaded.AsSpan(0, expectedStart).Count('\n'), e.Line);
    }

    [AvaloniaFact]
    public void Assigning_Markdown_while_Source_is_shown_re_renders_the_Source_document()
    {
        // Matching the toggled text is no write-back while a Source document is shown: Markdown is not what is rendered.
        var loaded = ReadTasksDoc();
        var viewer = new MarkdownViewer { Source = TasksDoc, AreTaskListsInteractive = true };
        viewer.TaskToggled += (_, e) => viewer.Markdown = string.Concat(
            loaded.AsSpan(0, e.SourceStart), "[x]", loaded.AsSpan(e.SourceStart + e.SourceLength));
        var content = viewer.Content;

        Toggles(viewer)[0].IsChecked = true;

        Assert.NotSame(content, viewer.Content);
        Assert.Equal([false, false], Toggles(viewer).Select(t => t.IsChecked));
    }

    // ── Pointer, keyboard and selection ───────────────────────────────────────

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, List<TaskToggledEventArgs> Events) : IDisposable
    {
        public ToggleButton[] Toggles => MarkdownViewerTaskListTests.Toggles(Viewer);

        public MarkdownSelectableTextBlock[] Blocks =>
            Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().ToArray();

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown = TwoTasks)
    {
        var viewer = new MarkdownViewer { Markdown = markdown, AreTaskListsInteractive = true };
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        // Headless text has no hit-testable glyph geometry; a transparent background stands in for it.
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        window.Show();
        return new Fixture(window, viewer, RecordToggles(viewer));
    }

    private static Point CenterOf(Window window, Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static Point At(Window window, TextBlock block, int index)
    {
        var caret = block.TextLayout.HitTestTextPosition(index);
        var origin = block.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(origin.X + caret.X + 0.5, origin.Y + caret.Y + caret.Height / 2);
    }

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public void Clicking_a_task_marker_toggles_it_and_raises_TaskToggled_without_selecting()
    {
        using var f = Show();
        var toggle = f.Toggles[1];

        Click(f.Window, CenterOf(f.Window, toggle));

        Assert.True(toggle.IsChecked);
        Assert.Equal("☑", Assert.IsType<TaskCheckGlyph>(toggle.Content).Text);
        var e = Assert.Single(f.Events);
        Assert.True(e.IsChecked);
        Assert.Equal(TwoTasks.IndexOf("[ ]", StringComparison.Ordinal), e.SourceStart);
        Assert.Equal(string.Empty, f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Clicking_a_task_marker_twice_toggles_it_back()
    {
        using var f = Show();
        var toggle = f.Toggles[0];

        Click(f.Window, CenterOf(f.Window, toggle));
        Click(f.Window, CenterOf(f.Window, toggle));

        Assert.True(toggle.IsChecked);
        Assert.Equal("☑", Assert.IsType<TaskCheckGlyph>(toggle.Content).Text);
        Assert.Equal([false, true], f.Events.Select(e => e.IsChecked));
    }

    [AvaloniaFact]
    public void Clicking_a_task_marker_focuses_it()
    {
        using var f = Show();
        var toggle = f.Toggles[0];

        Click(f.Window, CenterOf(f.Window, toggle));

        Assert.True(toggle.IsFocused);
    }

    [AvaloniaFact]
    public void Space_toggles_the_focused_task_marker()
    {
        using var f = Show();
        var toggle = f.Toggles[1];
        toggle.Focus();

        f.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        f.Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");

        Assert.True(toggle.IsChecked);
        Assert.Equal([true], f.Events.Select(e => e.IsChecked));
    }

    [AvaloniaFact]
    public void Dragging_across_task_items_selects_their_text_without_toggling()
    {
        using var f = Show();

        f.Window.MouseDown(At(f.Window, f.Blocks[0], 1), MouseButton.Left);
        f.Window.MouseMove(At(f.Window, f.Blocks[1], 2), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(At(f.Window, f.Blocks[1], 2), MouseButton.Left);

        Assert.Equal("one\n☐ to", f.Viewer.GetSelectedText());
        Assert.Empty(f.Events);
        Assert.Equal([true, false], f.Toggles.Select(t => t.IsChecked));
    }

    [AvaloniaFact]
    public void Dragging_from_a_task_marker_selects_instead_of_toggling()
    {
        using var f = Show();
        var toggle = f.Toggles[0];

        f.Window.MouseDown(CenterOf(f.Window, toggle), MouseButton.Left);
        f.Window.MouseMove(At(f.Window, f.Blocks[0], 3), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(At(f.Window, f.Blocks[0], 3), MouseButton.Left);

        Assert.True(toggle.IsChecked);
        Assert.False(toggle.IsPressed);
        Assert.Empty(f.Events);
        Assert.StartsWith("☑", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Hovering_a_task_marker_shows_the_hand_cursor()
    {
        using var f = Show();
        var toggle = f.Toggles[0];

        f.Window.MouseMove(CenterOf(f.Window, toggle));

        Assert.NotNull(toggle.Cursor);
        Assert.NotSame(Cursor.Default, toggle.Cursor);
    }

    [AvaloniaFact]
    public void Copying_after_a_toggle_copies_the_new_check_glyph()
    {
        using var f = Show();

        Click(f.Window, CenterOf(f.Window, f.Toggles[1]));
        f.Viewer.SelectAll();

        Assert.Equal("☑ done\n☑ todo", f.Viewer.GetSelectedText());
    }
}
