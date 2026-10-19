// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// <see cref="MarkdownViewer.SelectionChanged"/>, <see cref="MarkdownViewer.HasSelection"/>,
/// <see cref="MarkdownViewer.SelectedText"/> and <see cref="MarkdownViewer.CopyingToClipboard"/>.
/// </summary>
public class MarkdownViewerSelectionEventsTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer, List<string> Changes, List<bool> Selecting) : IDisposable
    {
        public MarkdownSelectableTextBlock[] Blocks =>
            Viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().ToArray();

        public DocumentSelectionLayer Layer =>
            Assert.IsType<DocumentSelectionLayer>(Assert.IsType<Grid>(Viewer.Content).Children[1]);

        public void Dispose() => Window.Close();
    }

    private const string AlphaBeta = """
        Alpha

        Beta
        """;

    // Each SelectionChanged is recorded with the text selected when it was raised (Changes) and its
    // IsSelecting (Selecting).
    private static Fixture Show(string markdown, Action<MarkdownViewer>? configure = null)
    {
        var viewer = new MarkdownViewer();
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        // The headless text drawing has no hit-testable glyphs; a transparent background stands in.
        window.Styles.Add(new Style(x => x.OfType<MarkdownSelectableTextBlock>())
        {
            Setters = { new Setter(TextBlock.BackgroundProperty, Brushes.Transparent) },
        });
        window.Show();
        var changes = new List<string>();
        var selecting = new List<bool>();
        viewer.SelectionChanged += (_, e) =>
        {
            changes.Add(viewer.SelectedText);
            selecting.Add(e.IsSelecting);
        };
        return new Fixture(window, viewer, changes, selecting);
    }

    private static Point At(Window window, TextBlock block, int index)
    {
        var caret = block.TextLayout.HitTestTextPosition(index);
        var origin = block.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(origin.X + block.Padding.Left + caret.X + 0.5, origin.Y + block.Padding.Top + caret.Y + caret.Height / 2);
    }

    private static void PressCopy(Window window)
    {
        var copy = Application.Current!.PlatformSettings!.HotkeyConfiguration.Copy.First();
        window.KeyPress(copy.Key, (RawInputModifiers)copy.KeyModifiers, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    // ── SelectionChanged and HasSelection ─────────────────────────────────────

    [AvaloniaFact]
    public void Nothing_is_selected_at_first()
    {
        using var f = Show(AlphaBeta);

        Assert.False(f.Viewer.HasSelection);
        Assert.Equal(string.Empty, f.Viewer.SelectedText);
        Assert.Empty(f.Changes);
    }

    [AvaloniaFact]
    public void Dragging_raises_SelectionChanged_once_per_new_range_and_once_more_when_it_ends()
    {
        using var f = Show(AlphaBeta);
        var blocks = f.Blocks;

        f.Window.MouseDown(At(f.Window, blocks[0], 1), MouseButton.Left);
        Assert.Empty(f.Changes);
        f.Window.MouseMove(At(f.Window, blocks[0], 3), RawInputModifiers.LeftMouseButton);
        f.Window.MouseMove(At(f.Window, blocks[0], 3), RawInputModifiers.LeftMouseButton);
        f.Window.MouseMove(At(f.Window, blocks[1], 2), RawInputModifiers.LeftMouseButton);
        Assert.Equal([true, true], f.Selecting);
        f.Window.MouseUp(At(f.Window, blocks[1], 2), MouseButton.Left);

        Assert.Equal(["lp", "lpha\nBe", "lpha\nBe"], f.Changes);
        Assert.Equal([true, true, false], f.Selecting);
        Assert.True(f.Viewer.HasSelection);
    }

    [AvaloniaFact]
    public void A_drag_that_leaves_the_selection_as_it_was_raises_nothing_when_it_ends()
    {
        using var f = Show(AlphaBeta);
        var block = f.Blocks[0];
        f.Window.MouseDown(At(f.Window, block, 1), MouseButton.Left);
        f.Window.MouseMove(At(f.Window, block, 3), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(At(f.Window, block, 3), MouseButton.Left);
        f.Changes.Clear();
        f.Selecting.Clear();

        // Past the drag threshold, but still on the same character: the press cleared the selection, the
        // drag selects nothing.
        var start = At(f.Window, block, 2);
        f.Window.MouseDown(start, MouseButton.Left);
        f.Window.MouseMove(start + new Point(0, 4), RawInputModifiers.LeftMouseButton);
        f.Window.MouseUp(start + new Point(0, 4), MouseButton.Left);

        Assert.Equal([""], f.Changes);
        Assert.Equal([false], f.Selecting);
    }

    [AvaloniaFact]
    public void A_drag_ends_when_the_pointer_capture_is_lost()
    {
        using var f = Show(AlphaBeta);
        var block = f.Blocks[0];
        f.Window.MouseDown(At(f.Window, block, 1), MouseButton.Left);
        f.Window.MouseMove(At(f.Window, block, 3), RawInputModifiers.LeftMouseButton);

        var grid = Assert.IsType<Grid>(f.Viewer.Content);
        grid.RaiseEvent(new PointerCaptureLostEventArgs(grid, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)));
        f.Window.MouseUp(At(f.Window, block, 3), MouseButton.Left);

        Assert.Equal(["lp", "lp"], f.Changes);
        Assert.Equal([true, false], f.Selecting);
    }

    [AvaloniaFact]
    public void A_lost_pointer_capture_without_a_drag_raises_nothing()
    {
        using var f = Show(AlphaBeta);
        f.Viewer.SelectAll();
        f.Changes.Clear();

        var grid = Assert.IsType<Grid>(f.Viewer.Content);
        grid.RaiseEvent(new PointerCaptureLostEventArgs(grid, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)));

        Assert.Empty(f.Changes);
    }

    [AvaloniaFact]
    public void A_click_clears_the_selection_and_raises_SelectionChanged()
    {
        using var f = Show(AlphaBeta);
        f.Viewer.SelectAll();
        f.Changes.Clear();

        var point = At(f.Window, f.Blocks[0], 2);
        f.Window.MouseDown(point, MouseButton.Left);
        f.Window.MouseUp(point, MouseButton.Left);

        Assert.Equal([""], f.Changes);
        Assert.False(f.Viewer.HasSelection);
    }

    [AvaloniaFact]
    public void A_click_with_nothing_selected_raises_nothing()
    {
        using var f = Show(AlphaBeta);

        var point = At(f.Window, f.Blocks[0], 2);
        f.Window.MouseDown(point, MouseButton.Left);
        f.Window.MouseUp(point, MouseButton.Left);

        Assert.Empty(f.Changes);
    }

    [AvaloniaFact]
    public void SelectAll_and_ClearSelection_raise_SelectionChanged_only_when_the_range_changes()
    {
        using var f = Show(AlphaBeta);

        f.Viewer.SelectAll();
        f.Viewer.SelectAll();
        Assert.Equal(["Alpha\nBeta"], f.Changes);
        Assert.True(f.Viewer.HasSelection);

        f.Viewer.ClearSelection();
        f.Viewer.ClearSelection();
        Assert.Equal(["Alpha\nBeta", ""], f.Changes);
        Assert.Equal([false, false], f.Selecting);
        Assert.False(f.Viewer.HasSelection);
    }

    // ── SelectedText ──────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SelectedText_is_built_once_per_selection()
    {
        using var f = Show(AlphaBeta);
        f.Viewer.SelectAll();

        var text = f.Viewer.SelectedText;
        Assert.Same(text, f.Viewer.SelectedText);
        Assert.Same(text, f.Viewer.GetSelectedText());

        f.Layer.SetSelectionForTest(1, 3);
        Assert.Equal("lp", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void SelectedText_follows_an_alert_folded_or_unfolded_inside_the_selection()
    {
        using var f = Show("Before\n\n> [!NOTE]-\n> Body\n\nAfter",
            v => v.Pipeline = new MarkdownPipelineBuilder().UseExtendedAlertBlocks().Build());
        var alert = f.Viewer.GetVisualDescendants().OfType<Border>().Select(CollapsibleAlert.Get).Single(a => a is not null)!;
        f.Viewer.SelectAll();
        Assert.Equal("Before\nAfter", f.Viewer.SelectedText);

        alert.IsExpanded = true;
        Assert.Equal("Before\nBody\nAfter", f.Viewer.SelectedText);

        alert.IsExpanded = false;
        Assert.Equal("Before\nAfter", f.Viewer.SelectedText);
    }

    [AvaloniaFact]
    public void HasSelection_notifies_its_changes()
    {
        using var f = Show(AlphaBeta);
        var values = new List<bool>();
        f.Viewer.PropertyChanged += (_, e) =>
        {
            if (e.Property == MarkdownViewer.HasSelectionProperty)
                values.Add((bool)e.NewValue!);
        };

        f.Viewer.SelectAll();
        f.Viewer.ClearSelection();

        Assert.Equal([true, false], values);
    }

    [AvaloniaFact]
    public void A_new_document_clears_the_selection_and_raises_SelectionChanged()
    {
        using var f = Show(AlphaBeta);
        f.Viewer.SelectAll();
        f.Changes.Clear();

        f.Viewer.Markdown = "Gamma";

        Assert.Equal([""], f.Changes);
        Assert.False(f.Viewer.HasSelection);

        // The new document's selection is the one reported from now on.
        f.Viewer.SelectAll();
        Assert.Equal(["", "Gamma"], f.Changes);
    }

    [AvaloniaFact]
    public void An_empty_document_clears_the_selection_and_raises_SelectionChanged()
    {
        using var f = Show(AlphaBeta);
        f.Viewer.SelectAll();
        f.Changes.Clear();

        f.Viewer.Markdown = "";

        Assert.Equal([""], f.Changes);
        Assert.False(f.Viewer.HasSelection);
    }

    [AvaloniaFact]
    public void A_new_document_with_nothing_selected_raises_nothing()
    {
        using var f = Show(AlphaBeta);

        f.Viewer.Markdown = "Gamma";

        Assert.Empty(f.Changes);
    }

    [AvaloniaFact]
    public void The_replaced_document_no_longer_reports_its_selection()
    {
        using var f = Show(AlphaBeta);
        var oldLayer = f.Layer;
        f.Viewer.Markdown = "Gamma";
        f.Changes.Clear();

        oldLayer.SelectAll();

        Assert.Empty(f.Changes);
        Assert.False(f.Viewer.HasSelection);
    }

    [AvaloniaFact]
    public void SelectionChanged_bubbles()
    {
        using var f = Show(AlphaBeta);
        var sources = new List<object?>();
        f.Window.AddHandler(MarkdownViewer.SelectionChangedEvent, (_, e) => sources.Add(e.Source));

        f.Viewer.SelectAll();

        Assert.Equal([f.Viewer], sources);
    }

    [AvaloniaFact]
    public void A_streaming_update_that_shortens_the_selection_raises_SelectionChanged()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a\n\nb");
        session.ApplyPendingUpdate();
        f.Viewer.SelectAll();
        f.Changes.Clear();

        session.Append("c");
        session.ApplyPendingUpdate();

        Assert.Equal(["a"], f.Changes);
        Assert.True(f.Viewer.HasSelection);
    }

    [AvaloniaFact]
    public void A_streaming_update_that_clears_the_selection_raises_SelectionChanged()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a\n\nb");
        session.ApplyPendingUpdate();
        f.Layer.SetSelectionForTest(2, 3);
        f.Changes.Clear();

        session.Append("c");
        session.ApplyPendingUpdate();

        Assert.Equal([""], f.Changes);
        Assert.False(f.Viewer.HasSelection);
    }

    [AvaloniaFact]
    public void A_streaming_update_that_keeps_the_selection_raises_nothing()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("a\n\nb");
        session.ApplyPendingUpdate();
        f.Layer.SetSelectionForTest(0, 1);
        f.Changes.Clear();

        session.Append("c");
        session.ApplyPendingUpdate();

        Assert.Empty(f.Changes);
        Assert.True(f.Viewer.HasSelection);
    }

    [AvaloniaFact]
    public void Starting_a_stream_clears_the_selection_and_raises_SelectionChanged()
    {
        using var f = Show(AlphaBeta);
        f.Viewer.SelectAll();
        f.Changes.Clear();

        var session = f.Viewer.BeginStreaming();
        session.Append("a");
        session.ApplyPendingUpdate();

        Assert.Equal([""], f.Changes);
        Assert.False(f.Viewer.HasSelection);
    }

    // ── CopyingToClipboard ────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Copying_the_selection_raises_CopyingToClipboard_with_its_text()
    {
        using var f = Show(AlphaBeta);
        var raised = new List<(MarkdownCopyOrigin, string)>();
        f.Viewer.CopyingToClipboard += (_, e) => raised.Add((e.Origin, e.Text));
        f.Viewer.SelectAll();

        await f.Viewer.CopyToClipboardAsync();

        Assert.Equal([(MarkdownCopyOrigin.Selection, "Alpha\nBeta")], raised);
        Assert.Equal("Alpha\nBeta".ReplaceLineEndings(), await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public async Task The_copy_hotkey_raises_CopyingToClipboard()
    {
        using var f = Show(AlphaBeta);
        var origins = new List<MarkdownCopyOrigin>();
        f.Viewer.CopyingToClipboard += (_, e) => origins.Add(e.Origin);
        f.Viewer.Focus();
        f.Viewer.SelectAll();

        PressCopy(f.Window);

        Assert.Equal([MarkdownCopyOrigin.Selection], origins);
        Assert.Equal("Alpha\nBeta".ReplaceLineEndings(), await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public async Task A_handler_can_change_the_copied_text()
    {
        using var f = Show(AlphaBeta);
        f.Viewer.CopyingToClipboard += (_, e) => e.Text = e.Text.ToUpperInvariant() + "\nfrom the app";
        f.Viewer.SelectAll();

        await f.Viewer.CopyToClipboardAsync();

        Assert.Equal("ALPHA\nBETA\nfrom the app".ReplaceLineEndings(), await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public async Task A_cancelled_copy_writes_nothing()
    {
        using var f = Show(AlphaBeta);
        await f.Window.Clipboard!.SetTextAsync("keep");
        f.Viewer.CopyingToClipboard += (_, e) => e.Cancel = true;
        f.Viewer.SelectAll();

        await f.Viewer.CopyToClipboardAsync();

        Assert.Equal("keep", await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public async Task A_copy_emptied_by_a_handler_writes_nothing()
    {
        using var f = Show(AlphaBeta);
        await f.Window.Clipboard!.SetTextAsync("keep");
        f.Viewer.CopyingToClipboard += (_, e) => e.Text = null!;
        f.Viewer.SelectAll();

        await f.Viewer.CopyToClipboardAsync();

        Assert.Equal("keep", await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public async Task Copying_with_nothing_selected_raises_nothing_and_writes_nothing()
    {
        using var f = Show(AlphaBeta);
        await f.Window.Clipboard!.SetTextAsync("keep");
        var raised = 0;
        f.Viewer.CopyingToClipboard += (_, _) => raised++;

        await f.Viewer.CopyToClipboardAsync();

        Assert.Equal(0, raised);
        Assert.Equal("keep", await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public void CopyingToClipboard_bubbles()
    {
        using var f = Show(AlphaBeta);
        var sources = new List<object?>();
        f.Window.AddHandler(MarkdownViewer.CopyingToClipboardEvent, (_, e) => sources.Add(e.Source));
        f.Viewer.SelectAll();

        _ = f.Viewer.CopyToClipboardAsync();

        Assert.Equal([f.Viewer], sources);
    }

    private static Button CopyButton(MarkdownViewer viewer) =>
        viewer.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("markdown-code-copy"));

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task The_code_block_copy_button_raises_CopyingToClipboard_with_the_code()
    {
        using var f = Show("```\nfirst\nsecond\n```");
        var raised = new List<(MarkdownCopyOrigin, string)>();
        f.Viewer.CopyingToClipboard += (_, e) => raised.Add((e.Origin, e.Text));
        var button = CopyButton(f.Viewer);

        Click(button);

        Assert.Equal([(MarkdownCopyOrigin.CodeBlock, "first\nsecond")], raised);
        Assert.Equal("first\nsecond".ReplaceLineEndings(), await f.Window.Clipboard!.TryGetTextAsync());
        Assert.Contains("copied", button.Classes);
    }

    [AvaloniaFact]
    public async Task A_handler_can_change_the_code_the_copy_button_copies()
    {
        using var f = Show("```\ncode\n```");
        f.Viewer.CopyingToClipboard += (_, e) => e.Text = "changed";

        Click(CopyButton(f.Viewer));

        Assert.Equal("changed", await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public async Task A_cancelled_code_block_copy_writes_nothing_and_shows_no_confirmation()
    {
        using var f = Show("```\ncode\n```");
        await f.Window.Clipboard!.SetTextAsync("keep");
        f.Viewer.CopyingToClipboard += (_, e) => e.Cancel = true;
        var button = CopyButton(f.Viewer);

        Click(button);

        Assert.Equal("keep", await f.Window.Clipboard!.TryGetTextAsync());
        Assert.DoesNotContain("copied", button.Classes);
    }
}
