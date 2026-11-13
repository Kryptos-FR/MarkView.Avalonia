// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;

using Xunit;

namespace MarkView.Avalonia.Tests.Streaming;

/// <summary>
/// A code block that ends the streamed text is written in chunks of lines, and an update keeps the complete chunks
/// (see <see cref="GrowingCodeBlock"/>); once it no longer ends the text, it is a single text block again.
/// </summary>
public class GrowingCodeBlockTests
{
    private static string Lines(int from, int count) =>
        string.Concat(Enumerable.Range(from, count).Select(i => $"line {i}\n"));

    private static StackPanel Root(MarkdownViewer viewer) =>
        Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);

    private static Border CodeBorder(MarkdownViewer viewer) =>
        Assert.IsType<Border>(Assert.Single(Root(viewer).Children), exactMatch: false);

    private static List<TextBlock> Chunks(MarkdownViewer viewer) =>
        Assert.IsAssignableFrom<IEnumerable<TextBlock>>(CodeBlockToolbar.FindCodeChunks(CodeBorder(viewer))).ToList();

    private static string Text(TextBlock text) => MarkdownSelectableTextBlock.ExtractPlainText(text.Inlines!);

    private static (MarkdownViewer Viewer, MarkdownStreamingSession Session) Stream(string text, Action<MarkdownViewer>? configure = null)
    {
        // No copy button, so the code block is the root's only child, without a toolbar around it.
        var viewer = new MarkdownViewer { ShowCodeBlockCopyButton = false };
        configure?.Invoke(viewer);
        var session = viewer.BeginStreaming();
        session.Append(text);
        session.ApplyPendingUpdate();
        return (viewer, session);
    }

    [AvaloniaFact]
    public void Code_block_that_ends_the_text_is_written_in_chunks_of_lines()
    {
        var (viewer, _) = Stream("```\n" + Lines(0, 70));

        var chunks = Chunks(viewer);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(Lines(0, 32).TrimEnd('\n'), Text(chunks[0]));
        Assert.Equal(Lines(32, 32).TrimEnd('\n'), Text(chunks[1]));
        // Markdig counts the empty line after the last line break as a line of the open block.
        Assert.StartsWith("line 64\n", Text(chunks[2]));
    }

    [AvaloniaFact]
    public void Update_keeps_the_shell_and_the_complete_chunks()
    {
        var (viewer, session) = Stream("```\n" + Lines(0, 70));
        var border = CodeBorder(viewer);
        var before = Chunks(viewer);

        session.Append(Lines(70, 30));
        session.ApplyPendingUpdate();

        var after = Chunks(viewer);
        Assert.Same(border, CodeBorder(viewer));
        Assert.Same(before[0], after[0]);
        Assert.Same(before[1], after[1]);
        // The last chunk was not complete: it is written again, with the lines that joined it.
        Assert.NotSame(before[2], after[2]);
        Assert.Equal(Lines(64, 32).TrimEnd('\n'), Text(after[2]));
        Assert.Equal(4, after.Count);
    }

    [AvaloniaFact]
    public void Chunk_whose_last_line_was_still_growing_is_written_again()
    {
        // 32 lines, the last without its line break: the first chunk is full but not complete.
        var (viewer, session) = Stream("```\n" + Lines(0, 31) + "line 3");
        var before = Chunks(viewer);
        Assert.Single(before);

        session.Append("1 and more\n");
        session.ApplyPendingUpdate();

        var after = Chunks(viewer);
        Assert.NotSame(before[0], after[0]);
        Assert.EndsWith("line 31 and more", Text(after[0]));
    }

    [AvaloniaFact]
    public void Selection_copies_the_chunks_as_the_lines_they_hold()
    {
        var (viewer, _) = Stream("```\n" + Lines(0, 70) + "last");

        viewer.SelectAll();

        Assert.Equal(Lines(0, 70) + "last", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Selection_as_markdown_fences_the_selected_lines_of_every_chunk()
    {
        var (viewer, _) = Stream("```cs\n" + Lines(0, 40) + "last");
        var layer = Assert.IsType<DocumentSelectionLayer>(Assert.IsType<Grid>(viewer.Content).Children[1]);

        // From "line 30" to the end of "line 33", across the first two chunks.
        layer.SetSelectionForTest(Lines(0, 30).Length, Lines(0, 34).Length - 1);

        Assert.Equal("```cs\n" + Lines(30, 4) + "```", viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Line_numbers_go_on_from_one_chunk_to_the_next()
    {
        var (viewer, _) = Stream("```\n" + Lines(0, 40) + "last", v => v.ShowCodeBlockLineNumbers = true);

        var rows = Assert.IsType<StackPanel>(CodeBorder(viewer).Child).Children;

        var second = Assert.IsType<Grid>(rows[1]);
        var numbers = Assert.IsType<TextBlock>(second.Children[0]);
        Assert.StartsWith("33\n34\n", numbers.Text);
        Assert.EndsWith("\n41", numbers.Text);
        // The gutters of all the chunks are one column wide.
        Assert.True(Grid.GetIsSharedSizeScope(CodeBorder(viewer).Child!));
        Assert.Equal("MarkdownCodeLineNumbers", second.ColumnDefinitions[0].SharedSizeGroup);
    }

    [AvaloniaFact]
    public void Copy_button_copies_the_code_streamed_so_far()
    {
        // In a window, so the button finds the viewer whose CopyingToClipboard event reports the copy.
        var viewer = new MarkdownViewer { Template = ScrollingViewerTemplate.Create() };
        var window = new Window { Content = viewer };
        window.Show();
        var session = viewer.BeginStreaming();
        session.Append("```\n" + Lines(0, 40));
        session.ApplyPendingUpdate();
        session.Append(Lines(40, 10) + "last");
        session.ApplyPendingUpdate();

        var container = Assert.IsType<StackPanel>(Assert.Single(Root(viewer).Children));
        var button = Assert.Single(container.GetLogicalDescendants().OfType<Button>(), b => b.Classes.Contains("markdown-code-copy"));
        string? copied = null;
        viewer.CopyingToClipboard += (_, e) =>
        {
            copied = e.Text;
            e.Cancel = true;
        };

        button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(Lines(0, 50) + "last", copied);
        window.Close();
    }

    [AvaloniaFact]
    public void Completed_session_renders_the_block_as_a_single_text_block()
    {
        var (viewer, session) = Stream("```\n" + Lines(0, 70) + "last");

        session.Complete();

        var border = CodeBorder(viewer);
        Assert.Null(CodeBlockToolbar.FindCodeChunks(border));
        Assert.Equal(Lines(0, 70) + "last", Text(Assert.IsType<TextBlock>(CodeBlockToolbar.FindCode(border))));
    }

    [AvaloniaFact]
    public void Code_block_followed_by_more_text_is_a_single_text_block_again()
    {
        var (viewer, session) = Stream("```\n" + Lines(0, 40));

        session.Append("```\n\nAfter");
        session.ApplyPendingUpdate();

        var code = Assert.IsType<Border>(Root(viewer).Children[0]);
        Assert.Null(CodeBlockToolbar.FindCodeChunks(code));
        Assert.NotNull(CodeBlockToolbar.FindCode(code));
    }

    [AvaloniaFact]
    public void Language_change_writes_the_block_again()
    {
        var (viewer, session) = Stream("```py");
        var border = CodeBorder(viewer);

        session.Append("thon\n" + Lines(0, 3));
        session.ApplyPendingUpdate();

        Assert.NotSame(border, CodeBorder(viewer));
        Assert.Contains("language-python", CodeBorder(viewer).Classes);
    }

    [AvaloniaFact]
    public void Code_block_inside_a_list_is_written_as_usual()
    {
        var (viewer, _) = Stream("- item\n\n  ```\n  " + Lines(0, 3));

        Assert.DoesNotContain(Root(viewer).GetLogicalDescendants().OfType<StackPanel>(),
            p => p.Classes.Contains(GrowingCodeBlock.ChunksClass));
    }

    [AvaloniaFact]
    public void Height_limited_code_block_scrolls_its_chunks()
    {
        var (viewer, _) = Stream("```\n" + Lines(0, 40), v => v.CodeBlockMaxHeight = 100);

        var border = CodeBorder(viewer);
        var scroll = Assert.IsType<ScrollViewer>(border.Child);
        Assert.Contains("markdown-code-scroll", scroll.Classes);
        Assert.Equal(100, border.MaxHeight);
        Assert.Equal(2, Chunks(viewer).Count);
    }
}
