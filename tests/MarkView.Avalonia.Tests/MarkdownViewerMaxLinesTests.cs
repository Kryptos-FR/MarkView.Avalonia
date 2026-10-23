// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// <see cref="MarkdownViewer.MaxLines"/>: a viewer as tall as the document's first lines, the expand
/// button, and what the hidden lines do with find, anchors, Tab and streaming.
/// </summary>
public class MarkdownViewerMaxLinesTests
{
    private static string Paragraphs(int count) =>
        string.Join("\n\n", Enumerable.Range(1, count).Select(i => $"Paragraph {i}"));

    private sealed record Fixture(Window Window, MarkdownViewer Viewer, Button Before, Button After) : IDisposable
    {
        public Grid Grid => Assert.IsType<Grid>(Viewer.Content);

        public Panel Root => Assert.IsAssignableFrom<Panel>(Grid.Children[0]);

        public Button ExpandButton => Viewer.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_ExpandButton");

        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

        /// <summary>The bottom of the text line of the root's <paramref name="index"/>-th block, in the grid.</summary>
        public double BlockBottom(int index)
        {
            var block = Assert.IsAssignableFrom<TextBlock>(Root.Children[index]);
            var origin = block.TranslatePoint(new Point(0, block.Padding.Top), Grid)!.Value;
            return origin.Y + block.TextLayout.TextLines.Sum(l => l.Height);
        }

        /// <summary>Runs one frame: the layout passes, until there is none left, then the render.</summary>
        public void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        public void Tab(bool shift = false)
        {
            Window.KeyPress(Key.Tab, shift ? RawInputModifiers.Shift : RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }

    // Mutation testing compiles the library without its AXAML, so the viewer gets the theme's template
    // structure built in code: the expand button docked below the scroll viewer.
    private static FuncControlTemplate ExpandingTemplate() => new((_, scope) =>
    {
        var presenter = new ContentPresenter { Name = "PART_ContentPresenter" };
        presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty));
        var button = new Button { Name = "PART_ExpandButton", IsVisible = false, Content = "Show more" };
        DockPanel.SetDock(button, Dock.Bottom);
        return new DockPanel
        {
            Children =
            {
                button.RegisterInNameScope(scope),
                new ScrollViewer { Name = "PART_ScrollViewer", Content = presenter }.RegisterInNameScope(scope),
            },
        };
    });

    private static Fixture Show(string markdown, int maxLines, Action<MarkdownViewer>? configure = null, double width = 400)
    {
        var viewer = new MarkdownViewer { Template = ExpandingTemplate(), MaxLines = maxLines };
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var before = new Button { Content = "Before" };
        var after = new Button { Content = "After" };
        var window = new Window
        {
            Width = width,
            Height = 600,
            Content = new StackPanel { Children = { before, viewer, after } },
        };
        window.Show();
        var fixture = new Fixture(window, viewer, before, after);
        fixture.Layout();
        return fixture;
    }

    // Layout rounding rounds the grid's height up to a whole pixel.
    private static void AssertShownHeight(double expected, double actual) =>
        Assert.InRange(actual, expected - 0.01, Math.Ceiling(expected) + 0.01);

    // ── Cutting ───────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void A_document_longer_than_MaxLines_is_cut_below_its_last_line_shown()
    {
        using var f = Show(Paragraphs(5), maxLines: 2);

        Assert.True(f.Viewer.ExceedsMaxLines);
        AssertShownHeight(f.BlockBottom(1), f.Grid.Bounds.Height);
        AssertShownHeight(f.BlockBottom(1), f.Grid.MaxHeight);
        Assert.True(f.Grid.ClipToBounds);
        Assert.Contains(":truncated", f.Viewer.Classes);
        Assert.DoesNotContain(":expanded", f.Viewer.Classes);
    }

    [AvaloniaFact]
    public void The_lines_of_a_wrapped_paragraph_count_one_by_one()
    {
        var words = string.Join(' ', Enumerable.Repeat("word", 60));
        using var f = Show(words, maxLines: 2, width: 200);

        var paragraph = Assert.IsAssignableFrom<TextBlock>(f.Root.Children[0]);
        Assert.True(paragraph.TextLayout.TextLines.Count > 2);
        var top = paragraph.TranslatePoint(new Point(0, paragraph.Padding.Top), f.Grid)!.Value.Y;
        var lines = paragraph.TextLayout.TextLines;
        Assert.True(f.Viewer.ExceedsMaxLines);
        AssertShownHeight(top + lines[0].Height + lines[1].Height, f.Grid.Bounds.Height);
    }

    [AvaloniaFact]
    public void A_narrower_viewer_moves_the_cut_up_as_the_text_wraps_more()
    {
        var words = string.Join(' ', Enumerable.Repeat("word", 30));
        using var f = Show($"{words}\n\n{words}", maxLines: 3, width: 1200);
        var wide = f.Grid.Bounds.Height;

        f.Window.Width = 200;
        f.Layout();

        Assert.True(f.Viewer.ExceedsMaxLines);
        Assert.True(f.Grid.Bounds.Height < wide, $"{f.Grid.Bounds.Height} < {wide}");
        var paragraph = Assert.IsAssignableFrom<TextBlock>(f.Root.Children[0]);
        var top = paragraph.TranslatePoint(new Point(0, paragraph.Padding.Top), f.Grid)!.Value.Y;
        AssertShownHeight(top + paragraph.TextLayout.TextLines.Take(3).Sum(l => l.Height), f.Grid.Bounds.Height);
    }

    [AvaloniaTheory]
    [InlineData(3)]
    [InlineData(4)]
    public void A_document_with_at_most_MaxLines_lines_shows_whole(int maxLines)
    {
        using var f = Show(Paragraphs(3), maxLines);

        Assert.False(f.Viewer.ExceedsMaxLines);
        AssertShownHeight(f.Root.Bounds.Height, f.Grid.Bounds.Height);
        Assert.True(double.IsPositiveInfinity(f.Grid.MaxHeight));
        Assert.False(f.Grid.ClipToBounds);
        Assert.DoesNotContain(":truncated", f.Viewer.Classes);
        Assert.False(f.ExpandButton.IsVisible);
    }

    [AvaloniaFact]
    public void MaxLines_0_shows_the_whole_document()
    {
        using var f = Show(Paragraphs(5), maxLines: 0);

        Assert.Equal(0, new MarkdownViewer().MaxLines);
        Assert.False(f.Viewer.ExceedsMaxLines);
        AssertShownHeight(f.Root.Bounds.Height, f.Grid.Bounds.Height);
    }

    [AvaloniaFact]
    public void MaxLines_rejects_a_negative_value()
    {
        var viewer = new MarkdownViewer();

        Assert.Throws<ArgumentException>(() => viewer.MaxLines = -1);
    }

    [AvaloniaFact]
    public void Changing_MaxLines_cuts_again_without_rendering_again()
    {
        using var f = Show(Paragraphs(5), maxLines: 2);
        var root = f.Root;

        f.Viewer.MaxLines = 3;
        f.Layout();

        Assert.Same(root, f.Root);
        AssertShownHeight(f.BlockBottom(2), f.Grid.Bounds.Height);

        f.Viewer.MaxLines = 0;
        f.Layout();

        Assert.False(f.Viewer.ExceedsMaxLines);
        AssertShownHeight(f.Root.Bounds.Height, f.Grid.Bounds.Height);
    }

    [AvaloniaFact]
    public void Content_that_grows_above_the_cut_moves_the_cut_down()
    {
        using var f = Show(Paragraphs(5), maxLines: 2);
        var first = Assert.IsAssignableFrom<TextBlock>(f.Root.Children[0]);

        first.Margin = new Thickness(0, 50, 0, 0);
        f.Layout();

        AssertShownHeight(f.BlockBottom(1), f.Grid.Bounds.Height);
        Assert.True(f.BlockBottom(1) > 50);
    }

    [AvaloniaFact]
    public void Setting_MaxLines_before_the_document_is_shown_cuts_it_at_its_first_layout()
    {
        var viewer = new MarkdownViewer { Template = ExpandingTemplate(), Markdown = Paragraphs(4), MaxLines = 1 };

        Assert.False(viewer.ExceedsMaxLines);

        var window = new Window { Width = 400, Height = 600, Content = new StackPanel { Children = { viewer } } };
        window.Show();
        using var f = new Fixture(window, viewer, new Button(), new Button());
        f.Layout();

        Assert.True(viewer.ExceedsMaxLines);
        AssertShownHeight(f.BlockBottom(0), f.Grid.Bounds.Height);
    }

    [AvaloniaFact]
    public void An_empty_document_does_not_exceed_MaxLines()
    {
        using var f = Show(Paragraphs(5), maxLines: 1);
        Assert.True(f.Viewer.ExceedsMaxLines);

        f.Viewer.Markdown = "";
        f.Layout();

        Assert.Null(f.Viewer.Content);
        Assert.False(f.Viewer.ExceedsMaxLines);
        Assert.False(f.ExpandButton.IsVisible);
        Assert.DoesNotContain(":truncated", f.Viewer.Classes);
    }

    [AvaloniaFact]
    public void A_new_document_is_cut_with_the_same_MaxLines()
    {
        using var f = Show(Paragraphs(2), maxLines: 2);
        Assert.False(f.Viewer.ExceedsMaxLines);

        f.Viewer.Markdown = Paragraphs(6);
        f.Layout();

        Assert.True(f.Viewer.ExceedsMaxLines);
        AssertShownHeight(f.BlockBottom(1), f.Grid.Bounds.Height);
    }

    // ── Expanding ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void IsExpanded_shows_the_whole_document_and_still_reports_the_overflow()
    {
        using var f = Show(Paragraphs(5), maxLines: 2);

        f.Viewer.IsExpanded = true;
        f.Layout();

        Assert.True(f.Viewer.ExceedsMaxLines);
        Assert.True(double.IsPositiveInfinity(f.Grid.MaxHeight));
        Assert.True(f.Grid.Bounds.Height >= f.BlockBottom(4), $"{f.Grid.Bounds.Height} >= {f.BlockBottom(4)}");
        Assert.False(f.Grid.ClipToBounds);
        Assert.DoesNotContain(":truncated", f.Viewer.Classes);
        Assert.Contains(":expanded", f.Viewer.Classes);
        Assert.True(f.ExpandButton.IsVisible);

        f.Viewer.IsExpanded = false;
        f.Layout();

        AssertShownHeight(f.BlockBottom(1), f.Grid.Bounds.Height);
        Assert.Contains(":truncated", f.Viewer.Classes);
    }

    [AvaloniaFact]
    public void A_viewer_expanded_before_its_first_layout_shows_whole()
    {
        using var f = Show(Paragraphs(5), maxLines: 2, v => v.IsExpanded = true);

        Assert.True(f.Viewer.ExceedsMaxLines);
        AssertShownHeight(f.Root.Bounds.Height, f.Grid.Bounds.Height);
    }

    [AvaloniaFact]
    public void The_expand_button_shows_only_while_the_document_exceeds_MaxLines()
    {
        using var f = Show(Paragraphs(2), maxLines: 2);
        Assert.False(f.ExpandButton.IsVisible);

        f.Viewer.MaxLines = 1;
        f.Layout();

        Assert.True(f.ExpandButton.IsVisible);
    }

    [AvaloniaFact]
    public void The_expand_button_toggles_IsExpanded_and_keeps_a_binding()
    {
        using var f = Show(Paragraphs(5), maxLines: 2);
        var source = new ExpandedSource();
        f.Viewer.Bind(MarkdownViewer.IsExpandedProperty, new Binding(nameof(ExpandedSource.IsExpanded)) { Source = source });

        f.ExpandButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        f.Layout();

        Assert.True(f.Viewer.IsExpanded);
        Assert.True(source.IsExpanded);
        AssertShownHeight(f.Root.Bounds.Height, f.Grid.Bounds.Height);

        f.ExpandButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        f.Layout();

        Assert.False(f.Viewer.IsExpanded);
        Assert.False(source.IsExpanded);
        AssertShownHeight(f.BlockBottom(1), f.Grid.Bounds.Height);

        source.IsExpanded = true;
        Assert.True(f.Viewer.IsExpanded);
    }

    private sealed class ExpandedSource : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public bool IsExpanded
        {
            get;
            set
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(IsExpanded)));
            }
        }
    }

    [AvaloniaFact]
    public void ShowExpandButton_false_hides_the_button()
    {
        using var f = Show(Paragraphs(5), maxLines: 2, v => v.ShowExpandButton = false);

        Assert.True(new MarkdownViewer().ShowExpandButton);
        Assert.True(f.Viewer.ExceedsMaxLines);
        Assert.False(f.ExpandButton.IsVisible);

        f.Viewer.ShowExpandButton = true;

        Assert.True(f.ExpandButton.IsVisible);
    }

    [AvaloniaFact]
    public void A_new_template_takes_over_the_expand_button()
    {
        using var f = Show(Paragraphs(5), maxLines: 2);
        var old = f.ExpandButton;

        f.Viewer.Template = ExpandingTemplate();
        f.Layout();

        Assert.NotSame(old, f.ExpandButton);
        Assert.True(f.ExpandButton.IsVisible);

        var changes = 0;
        f.Viewer.PropertyChanged += (_, e) => changes += e.Property == MarkdownViewer.IsExpandedProperty ? 1 : 0;
        old.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(0, changes);

        f.ExpandButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(f.Viewer.IsExpanded);
    }

    // ── Hidden lines ──────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Select_all_copies_the_hidden_lines_too()
    {
        using var f = Show(Paragraphs(3), maxLines: 1);

        f.Viewer.SelectAll();

        Assert.Equal("Paragraph 1\nParagraph 2\nParagraph 3", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Moving_to_a_find_match_in_the_hidden_lines_expands_the_viewer()
    {
        using var f = Show(Paragraphs(5), maxLines: 2);

        Assert.Equal(1, f.Viewer.Find("Paragraph 1"));
        Assert.False(f.Viewer.IsExpanded);

        Assert.Equal(1, f.Viewer.Find("Paragraph 4"));
        f.Viewer.FindNext();

        Assert.True(f.Viewer.IsExpanded);
    }

    [AvaloniaFact]
    public void Moving_to_a_find_match_in_the_lines_shown_does_not_expand_the_viewer()
    {
        using var f = Show(Paragraphs(5), maxLines: 2);

        f.Viewer.Find("Paragraph 2");
        f.Viewer.FindNext();

        Assert.False(f.Viewer.IsExpanded);
    }

    [AvaloniaFact]
    public void Scrolling_to_a_heading_in_the_hidden_lines_expands_the_viewer()
    {
        using var f = Show("# Top\n\nText\n\n# Hidden\n\nMore", maxLines: 2);

        f.Viewer.ScrollToAnchor("top");
        Assert.False(f.Viewer.IsExpanded);

        f.Viewer.ScrollToAnchor("hidden");
        Assert.True(f.Viewer.IsExpanded);
    }

    [AvaloniaFact]
    public void Scrolling_to_the_heading_that_is_the_last_line_shown_does_not_expand_the_viewer()
    {
        using var f = Show("Text\n\n# Last\n\nMore", maxLines: 2);

        f.Viewer.ScrollToAnchor("last");

        Assert.False(f.Viewer.IsExpanded);
    }

    [AvaloniaFact]
    public void A_find_match_in_the_hidden_lines_of_a_scrolling_viewer_is_scrolled_into_view()
    {
        using var f = Show(Paragraphs(40), maxLines: 2, v => v.Height = 120);
        var scrollViewer = f.Viewer.GetVisualDescendants().OfType<ScrollViewer>().First();

        f.Viewer.Find("Paragraph 35");
        f.Viewer.FindNext();

        Assert.True(f.Viewer.IsExpanded);
        Assert.True(scrollViewer.Offset.Y > 0, $"offset {scrollViewer.Offset.Y}");
    }

    [AvaloniaFact]
    public void A_heading_in_the_hidden_lines_of_a_scrolling_viewer_is_scrolled_to()
    {
        using var f = Show(Paragraphs(40) + "\n\n# End", maxLines: 2, v => v.Height = 120);
        var scrollViewer = f.Viewer.GetVisualDescendants().OfType<ScrollViewer>().First();

        f.Viewer.ScrollToAnchor("end");

        Assert.True(f.Viewer.IsExpanded);
        Assert.True(scrollViewer.Offset.Y > 0, $"offset {scrollViewer.Offset.Y}");
    }

    [AvaloniaFact]
    public void Tab_skips_a_link_that_starts_the_first_hidden_line()
    {
        // The paragraph wraps after "first"; the link starts its second line, right at the cut.
        using var f = Show("first [second](https://example.com/2)", maxLines: 1, v => v.ShowExpandButton = false, width: 60);
        var paragraph = Assert.IsAssignableFrom<TextBlock>(f.Root.Children[0]);
        Assert.True(paragraph.TextLayout.TextLines.Count >= 2);
        f.Viewer.Focus();

        f.Tab();

        Assert.Same(f.After, f.Focused);
    }

    [AvaloniaFact]
    public void Tab_skips_the_links_in_the_hidden_lines_and_ends_on_the_expand_button()
    {
        using var f = Show("[one](https://example.com/1)\n\n[two](https://example.com/2)\n\n[three](https://example.com/3)", maxLines: 2);
        f.Before.Focus();

        f.Tab();
        Assert.Same(f.Viewer, f.Focused);
        f.Tab();
        Assert.Equal("one", (f.Focused as LinkFocusTarget)?.AccessibleName);
        f.Tab();
        Assert.Equal("two", (f.Focused as LinkFocusTarget)?.AccessibleName);
        f.Tab();
        Assert.Same(f.ExpandButton, f.Focused);
        f.Tab();
        Assert.Same(f.After, f.Focused);

        f.Tab(shift: true);
        Assert.Same(f.ExpandButton, f.Focused);
        f.Tab(shift: true);
        Assert.Equal("two", (f.Focused as LinkFocusTarget)?.AccessibleName);
    }

    [AvaloniaFact]
    public void Tab_reaches_every_link_once_expanded()
    {
        using var f = Show("[one](https://example.com/1)\n\n[two](https://example.com/2)\n\n[three](https://example.com/3)", maxLines: 1,
            v => v.IsExpanded = true);
        f.Viewer.Focus();

        var visited = new List<string?>();
        for (var i = 0; i < 3; i++)
        {
            f.Tab();
            visited.Add((f.Focused as LinkFocusTarget)?.AccessibleName);
        }

        Assert.Equal(["one", "two", "three"], visited);
        f.Tab();
        Assert.Same(f.ExpandButton, f.Focused);
    }

    [AvaloniaFact]
    public void Tab_skips_a_focusable_control_in_the_hidden_lines()
    {
        // The copy button of the code block is the only stop, past the first line.
        using var f = Show("Intro\n\n```\ncode\n```", maxLines: 1, v => v.ShowExpandButton = false);
        f.Viewer.Focus();

        f.Tab();

        Assert.Same(f.After, f.Focused);
    }

    [AvaloniaFact]
    public void Streaming_cuts_the_document_once_it_grows_past_MaxLines()
    {
        using var f = Show("", maxLines: 2);
        var session = f.Viewer.BeginStreaming();

        session.Append("One\n\nTwo");
        session.ApplyPendingUpdate();
        f.Layout();

        Assert.False(f.Viewer.ExceedsMaxLines);

        session.Append("\n\nThree\n\nFour");
        session.ApplyPendingUpdate();
        f.Layout();

        Assert.True(f.Viewer.ExceedsMaxLines);
        AssertShownHeight(f.BlockBottom(1), f.Grid.Bounds.Height);
        Assert.True(f.ExpandButton.IsVisible);
    }

    [AvaloniaFact]
    public void The_lines_of_a_folded_section_do_not_count()
    {
        using var f = Show("# One\n\nA\n\nB\n\n# Two\n\nC", maxLines: 3, viewer => viewer.AreHeadingsFoldable = true);
        Assert.True(f.Viewer.ExceedsMaxLines);

        var one = f.Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().First();
        HeadingFold.Get(one)!.Toggle();
        f.Layout();

        // "One", "Two" and "C" are left.
        Assert.False(f.Viewer.ExceedsMaxLines);
        Assert.False(f.Grid.ClipToBounds);
    }

    private sealed class RecordingLoader : IImageLoader
    {
        public List<string> Loaded { get; } = [];

        public bool CanLoad(string url) => true;

        public Task<IImage?> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Loaded.Add(url);
            return Task.FromResult<IImage?>(null);
        }
    }

    private sealed class LoaderExtension(IImageLoader loader) : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) => renderer.ImageLoaders.Insert(0, loader);
    }

    [AvaloniaFact]
    public async Task An_image_streamed_in_below_the_cut_loads_once_the_viewer_expands()
    {
        var loader = new RecordingLoader();
        var url = $"fake://{Guid.NewGuid()}.png";
        using var f = Show("", maxLines: 2, viewer => viewer.Extensions.Add(new LoaderExtension(loader)));
        var session = f.Viewer.BeginStreaming();
        session.Append(Paragraphs(30));
        session.ApplyPendingUpdate();
        f.Layout();
        Assert.True(f.Viewer.ExceedsMaxLines);

        session.Append($"\n\n![picture]({url})\n\nAfter");
        session.ApplyPendingUpdate();
        f.Layout();
        f.Viewer.IsExpanded = true;
        f.Layout();

        await AsyncTestHelpers.WaitUntilAsync(() => loader.Loaded.Contains(url));
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void The_theme_template_has_a_Show_more_button_that_turns_into_Show_less()
    {
        var viewer = new MarkdownViewer { MaxLines = 1, Markdown = Paragraphs(3) };
        viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        var window = new Window { Width = 400, Height = 600, Content = new StackPanel { Children = { viewer } } };
        window.Show();
        using var f = new Fixture(window, viewer, new Button(), new Button());
        f.Layout();

        Assert.True(f.ExpandButton.IsVisible);
        Assert.Equal("Show more", f.ExpandButton.Content);
        // The viewer is the document's first line and the button below it.
        AssertShownHeight(f.BlockBottom(0) + f.ExpandButton.Bounds.Height + f.ExpandButton.Margin.Top, viewer.Bounds.Height);

        f.ExpandButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        f.Layout();

        Assert.Equal("Show less", f.ExpandButton.Content);
    }

    [AvaloniaFact]
    public void The_button_text_comes_from_string_resources()
    {
        var viewer = new MarkdownViewer { MaxLines = 1, Markdown = Paragraphs(3) };
        viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        viewer.Resources["MarkdownStringShowMore"] = "Afficher plus";
        viewer.Resources["MarkdownStringShowLess"] = "Afficher moins";
        var window = new Window { Width = 400, Height = 600, Content = new StackPanel { Children = { viewer } } };
        window.Show();
        using var f = new Fixture(window, viewer, new Button(), new Button());
        f.Layout();

        Assert.Equal("Afficher plus", f.ExpandButton.Content);

        viewer.IsExpanded = true;
        f.Layout();

        Assert.Equal("Afficher moins", f.ExpandButton.Content);
    }

    [AvaloniaFact]
    public void In_high_contrast_the_button_takes_the_high_contrast_link_colour()
    {
        var viewer = new MarkdownViewer { MaxLines = 1, Markdown = Paragraphs(3), HighContrastMode = HighContrastMode.Enabled };
        viewer.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        var window = new Window { Width = 400, Height = 600, Content = new StackPanel { Children = { viewer } } };
        window.Show();
        using var f = new Fixture(window, viewer, new Button(), new Button());
        f.Layout();

        Assert.True(viewer.TryFindResource("MarkdownHighContrastLink", viewer.ActualThemeVariant, out var link));
        Assert.Same(link, f.ExpandButton.Foreground);
    }
}
