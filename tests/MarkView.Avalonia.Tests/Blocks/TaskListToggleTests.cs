// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class TaskListToggleTests
{
    private const string TwoTasks = """
        - [x] done
        - [ ] todo
        """;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseTaskLists().Build();

    private static (StackPanel List, AvaloniaRenderer Renderer) Render(string markdown, bool interactive)
    {
        var renderer = new AvaloniaRenderer { AreTaskListsInteractive = interactive };
        Pipeline.Setup(renderer);
        renderer.Render(Markdown.Parse(markdown, Pipeline));
        return (Assert.IsType<StackPanel>(Assert.Single(renderer.RootPanel.Children)), renderer);
    }

    private static Control[] Markers(StackPanel list) =>
        list.Children.Cast<Grid>().Select(g => g.Children.Single(c => Grid.GetColumn(c) == 0)).ToArray();

    private static ToggleButton[] Toggles(StackPanel list) =>
        Markers(list).Select(m => Assert.IsType<ToggleButton>(m)).ToArray();

    private static TaskCheckGlyph Glyph(ToggleButton toggle) => Assert.IsType<TaskCheckGlyph>(toggle.Content);

    [AvaloniaFact]
    public void Renderer_task_lists_are_not_interactive_by_default()
    {
        Assert.False(new AvaloniaRenderer().AreTaskListsInteractive);
    }

    [AvaloniaFact]
    public void Non_interactive_task_marker_is_a_top_aligned_glyph_with_the_marker_margin()
    {
        var (list, _) = Render(TwoTasks, interactive: false);

        var markers = Markers(list).Select(m => Assert.IsType<TaskCheckGlyph>(m)).ToArray();
        Assert.Equal(["☑", "☐"], markers.Select(m => m.Text));
        Assert.All(markers, m =>
        {
            Assert.Equal(new Thickness(0, 0, 8, 0), m.Margin);
            Assert.Equal(VerticalAlignment.Top, m.VerticalAlignment);
        });
    }

    [AvaloniaFact]
    public void Interactive_task_marker_is_a_toggle_in_the_marker_column_with_the_marker_layout()
    {
        var (list, _) = Render(TwoTasks, interactive: true);

        var toggles = Toggles(list);
        Assert.Equal([true, false], toggles.Select(t => t.IsChecked));
        Assert.All(toggles, t =>
        {
            Assert.Contains("markdown-task-toggle", t.Classes);
            Assert.Equal(new Thickness(0, 0, 8, 0), t.Margin);
            Assert.Equal(VerticalAlignment.Top, t.VerticalAlignment);
            Assert.False(t.IsThreeState);
        });
    }

    [AvaloniaFact]
    public void Interactive_task_marker_shows_the_check_glyph_with_the_marker_classes()
    {
        var (list, _) = Render(TwoTasks, interactive: true);

        var glyphs = Toggles(list).Select(Glyph).ToArray();
        Assert.Equal(["☑", "☐"], glyphs.Select(g => g.Text));
        Assert.All(glyphs, g =>
        {
            Assert.Contains("markdown-list-marker", g.Classes);
            Assert.Contains("markdown-task-list", g.Classes);
            Assert.Equal(default, g.Margin);
        });
    }

    [AvaloniaFact]
    public void Interactive_task_item_text_holds_no_glyph()
    {
        var (list, _) = Render(TwoTasks, interactive: true);

        var texts = list.Children.Cast<Grid>()
            .Select(g => g.Children.OfType<StackPanel>().Single().Children.OfType<MarkdownSelectableTextBlock>().Single())
            .ToArray();
        Assert.Equal(["done", "todo"], texts.Select(t => MarkdownSelectableTextBlock.ExtractPlainText(t.Inlines!)));
        Assert.All(texts, t => Assert.DoesNotContain(t.Inlines!.OfType<Run>(), r => r.Classes.Contains("markdown-task-list")));
    }

    [AvaloniaFact]
    public void Interactive_task_marker_has_its_own_theme_with_a_hand_cursor_and_hit_testable_background()
    {
        var (list, renderer) = Render(TwoTasks, interactive: true);
        var window = new Window { Content = renderer.RootPanel };
        window.Show();
        try
        {
            var toggle = Toggles(list)[0];

            Assert.NotNull(toggle.Theme);
            Assert.Same(Toggles(list)[1].Theme, toggle.Theme);
            Assert.NotNull(toggle.Cursor);
            Assert.Same(Brushes.Transparent, toggle.Background);
            var presenter = Assert.IsType<global::Avalonia.Controls.Presenters.ContentPresenter>(Assert.Single(toggle.GetVisualChildren()));
            Assert.Same(Brushes.Transparent, presenter.Background);
            Assert.Same(Glyph(toggle), presenter.Child);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toggling_updates_the_glyph_and_raises_TaskToggled_with_the_marker_span()
    {
        var (list, renderer) = Render(TwoTasks, interactive: true);
        var events = new List<TaskToggledEventArgs>();
        renderer.TaskToggled += (_, e) => events.Add(e);
        var toggle = Toggles(list)[1];

        toggle.IsChecked = true;

        Assert.Equal("☑", Glyph(toggle).Text);
        var e = Assert.Single(events);
        Assert.True(e.IsChecked);
        Assert.Equal(TwoTasks.IndexOf("[ ]", StringComparison.Ordinal), e.SourceStart);
        Assert.Equal(3, e.SourceLength);
        Assert.Equal(1, e.Line);
    }

    [AvaloniaFact]
    public void Toggling_twice_restores_the_glyph_and_reports_each_state()
    {
        var (list, renderer) = Render(TwoTasks, interactive: true);
        var events = new List<TaskToggledEventArgs>();
        renderer.TaskToggled += (_, e) => events.Add(e);
        var toggle = Toggles(list)[0];

        toggle.IsChecked = false;
        Assert.Equal("☐", Glyph(toggle).Text);
        toggle.IsChecked = true;

        Assert.Equal("☑", Glyph(toggle).Text);
        Assert.Equal([false, true], events.Select(e => e.IsChecked));
        Assert.All(events, e => Assert.Equal(TwoTasks.IndexOf("[x]", StringComparison.Ordinal), e.SourceStart));
        Assert.All(events, e => Assert.Equal(0, e.Line));
    }

    [AvaloniaFact]
    public void Toggling_without_a_subscriber_still_updates_the_glyph()
    {
        var (list, _) = Render(TwoTasks, interactive: true);
        var toggle = Toggles(list)[1];

        toggle.IsChecked = true;

        Assert.Equal("☑", Glyph(toggle).Text);
    }

    [AvaloniaFact]
    public void Nested_task_items_report_their_own_span()
    {
        const string markdown = """
            - [ ] parent
              - [X] child
            """;
        var (list, renderer) = Render(markdown, interactive: true);
        var events = new List<TaskToggledEventArgs>();
        renderer.TaskToggled += (_, e) => events.Add(e);
        var nested = list.Children.Cast<Grid>().Single().Children.OfType<StackPanel>().Single()
            .Children.OfType<StackPanel>().Single();

        Toggles(nested)[0].IsChecked = false;

        var e = Assert.Single(events);
        Assert.False(e.IsChecked);
        Assert.Equal(markdown.IndexOf("[X]", StringComparison.Ordinal), e.SourceStart);
        Assert.Equal(1, e.Line);
    }
}
