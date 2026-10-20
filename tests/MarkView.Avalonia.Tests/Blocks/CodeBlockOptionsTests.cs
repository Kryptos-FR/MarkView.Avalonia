// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class CodeBlockOptionsTests
{
    private const string ThreeLines = "```csharp\nvar x = 1;\nvar y = 2;\nvar z = 3;\n```";

    private static string LongBlock(int lines) =>
        "```\n" + string.Join("\n", Enumerable.Range(1, lines).Select(i => $"line {i}")) + "\n```";

    private static StackPanel Render(string markdown, bool lineNumbers = false, bool wrap = false,
        double maxHeight = double.PositiveInfinity, bool toolbar = false, bool themed = false)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer
        {
            ShowCodeBlockLanguage = toolbar,
            ShowCodeBlockCopyButton = toolbar,
            ShowCodeBlockLineNumbers = lineNumbers,
            WrapCodeBlockLines = wrap,
            CodeBlockMaxHeight = maxHeight,
        };
        if (themed)
            renderer.Themes = RenderTestBase.MarkdownThemes();
        pipeline.Setup(renderer);
        renderer.Render(Markdig.Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    private static Border CodeBorder(StackPanel root) => CodeBlockAssert.CodeBorder(Assert.Single(root.Children));

    private static (Grid Lines, TextBlock Numbers, TextBlock Code) LineNumbered(Control content)
    {
        var grid = Assert.IsType<Grid>(content);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.Equal(GridLength.Auto, grid.ColumnDefinitions[0].Width);
        Assert.Equal(new GridLength(1, GridUnitType.Star), grid.ColumnDefinitions[1].Width);
        Assert.Equal(2, grid.Children.Count);

        var numbers = Assert.IsType<TextBlock>(grid.Children[0]);
        Assert.Contains("markdown-code-line-numbers", numbers.Classes);
        Assert.Equal(TextAlignment.Right, numbers.TextAlignment);
        Assert.Equal(0, Grid.GetColumn(numbers));

        var code = Assert.IsType<TextBlock>(grid.Children[1]);
        Assert.Equal(1, Grid.GetColumn(code));
        return (grid, numbers, code);
    }

    private static ScrollViewer Scrolling(Border border)
    {
        var scroll = Assert.IsType<ScrollViewer>(border.Child);
        Assert.Contains("markdown-code-scroll", scroll.Classes);
        Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
        return scroll;
    }

    private static string CodeText(TextBlock code) => MarkdownSelectableTextBlock.ExtractPlainText(code.Inlines!);

    // ── Renderer ──────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Line_numbers_are_hidden_from_screen_readers()
    {
        var (_, numbers, _) = LineNumbered(CodeBorder(Render(ThreeLines, lineNumbers: true)).Child!);

        var peer = ControlAutomationPeer.CreatePeerForElement(numbers);
        Assert.False(peer.IsControlElement());
        Assert.False(peer.IsContentElement());
    }

    [AvaloniaFact]
    public void Renderer_turns_every_option_off_by_default()
    {
        var renderer = new AvaloniaRenderer();

        Assert.False(renderer.ShowCodeBlockLineNumbers);
        Assert.False(renderer.WrapCodeBlockLines);
        Assert.Equal(double.PositiveInfinity, renderer.CodeBlockMaxHeight);
    }

    [AvaloniaFact]
    public void Without_options_the_code_is_the_only_child_of_the_block_and_never_wraps()
    {
        var border = CodeBorder(Render(ThreeLines));

        var code = Assert.IsType<TextBlock>(border.Child);
        Assert.Equal(TextWrapping.NoWrap, code.TextWrapping);
        Assert.Equal(double.PositiveInfinity, border.MaxHeight);
        Assert.Same(code, CodeBlockToolbar.FindCode(border));
    }

    [AvaloniaFact]
    public void Wrapping_wraps_the_code_and_keeps_it_the_only_child()
    {
        var border = CodeBorder(Render(ThreeLines, wrap: true));

        var code = Assert.IsType<TextBlock>(border.Child);
        Assert.Equal(TextWrapping.Wrap, code.TextWrapping);
        Assert.Equal(double.PositiveInfinity, border.MaxHeight);
    }

    [AvaloniaFact]
    public void Line_numbers_put_a_gutter_next_to_the_code()
    {
        var border = CodeBorder(Render(ThreeLines, lineNumbers: true));

        var (_, numbers, code) = LineNumbered(border.Child!);
        Assert.Equal("1\n2\n3", numbers.Text);
        // A code block's lines are LineBreak inlines, which plain text writes as the platform's line ending.
        var nl = Environment.NewLine;
        Assert.Equal($"var x = 1;{nl}var y = 2;{nl}var z = 3;", CodeText(code));
        Assert.Equal(TextWrapping.NoWrap, code.TextWrapping);
        Assert.Equal(double.PositiveInfinity, border.MaxHeight);
        Assert.Same(code, CodeBlockToolbar.FindCode(border));
    }

    [AvaloniaFact]
    public void Line_numbers_of_an_empty_block_are_empty()
    {
        var border = CodeBorder(Render("```\n```", lineNumbers: true));

        Assert.Equal(string.Empty, LineNumbered(border.Child!).Numbers.Text);
    }

    [AvaloniaFact]
    public void Maximum_height_caps_the_block_and_scrolls_the_code_vertically()
    {
        var border = CodeBorder(Render(ThreeLines, maxHeight: 120));

        Assert.Equal(120, border.MaxHeight);
        var scroll = Scrolling(border);
        var code = Assert.IsType<TextBlock>(scroll.Content);
        Assert.Equal(TextWrapping.NoWrap, code.TextWrapping);
        Assert.Same(code, CodeBlockToolbar.FindCode(border));
    }

    [AvaloniaFact]
    public void A_zero_maximum_height_still_applies()
    {
        var border = CodeBorder(Render(ThreeLines, maxHeight: 0));

        Assert.Equal(0, border.MaxHeight);
        Scrolling(border);
    }

    [AvaloniaFact]
    public void All_options_together_scroll_the_line_numbered_wrapped_code()
    {
        var border = CodeBorder(Render(ThreeLines, lineNumbers: true, wrap: true, maxHeight: 80));

        Assert.Equal(80, border.MaxHeight);
        var (_, numbers, code) = LineNumbered(Assert.IsType<Grid>(Scrolling(border).Content));
        Assert.Equal("1\n2\n3", numbers.Text);
        Assert.Equal(TextWrapping.Wrap, code.TextWrapping);
        Assert.Same(code, CodeBlockToolbar.FindCode(border));
    }

    [AvaloniaFact]
    public void Options_apply_below_the_toolbar()
    {
        var root = Render(ThreeLines, lineNumbers: true, maxHeight: 80, toolbar: true);

        var container = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        Assert.Contains("markdown-code-container", container.Classes);
        Assert.Contains("markdown-code-header", Assert.IsType<Border>(container.Children[0]).Classes);
        var border = Assert.IsType<Border>(container.Children[1]);
        Assert.Contains("language-csharp", border.Classes);
        LineNumbered(Assert.IsType<Grid>(Scrolling(border).Content));
    }

    [AvaloniaFact]
    public void Options_apply_to_indented_code_blocks()
    {
        var border = CodeBorder(Render("    first\n    second", lineNumbers: true, wrap: true));

        var (_, numbers, code) = LineNumbered(border.Child!);
        Assert.Equal("1\n2", numbers.Text);
        Assert.Equal(TextWrapping.Wrap, code.TextWrapping);
    }

    // ── Line numbers ──────────────────────────────────────────────────────────

    [AvaloniaTheory]
    [InlineData(0, "")]
    [InlineData(1, "1")]
    [InlineData(3, "1\n2\n3")]
    [InlineData(10, "1\n2\n3\n4\n5\n6\n7\n8\n9\n10")]
    public void LineNumbers_counts_from_one(int lineCount, string expected)
    {
        Assert.Equal(expected, CodeBlockToolbar.LineNumbers(lineCount));
    }

    private static TextLayout Layout(string text, double maxWidth) =>
        new(text, Typeface.Default, 10, Brushes.Black, textWrapping: TextWrapping.Wrap, maxWidth: maxWidth);

    [AvaloniaFact]
    public void Wrapped_line_numbers_leave_the_rows_a_line_continues_on_blank()
    {
        const string Text = "aaaa aaaa aaaa\nb\ncccc cccc";
        var layout = Layout(Text, 50);
        // Precondition: the first and last lines wrap at this width.
        Assert.True(layout.TextLines.Count > 3, $"expected wrapping, got {layout.TextLines.Count} rows");

        var numbers = CodeBlockToolbar.LineNumbers(layout, Text, 3).Split('\n');

        Assert.Equal(layout.TextLines.Count, numbers.Length);
        Assert.Equal(["1", "2", "3"], numbers.Where(n => n.Length > 0));
        var rows = layout.TextLines;
        for (int i = 0; i < rows.Count; i++)
        {
            var startsLine = i == 0 || Text[rows[i].FirstTextSourceIndex - 1] == '\n';
            Assert.Equal(startsLine, numbers[i].Length > 0);
        }
    }

    [AvaloniaFact]
    public void Wrapped_line_numbers_without_wrapping_are_one_per_row()
    {
        Assert.Equal("1\n2\n3", CodeBlockToolbar.LineNumbers(Layout("a\nb\nc", 1000), "a\nb\nc", 3));
    }

    [AvaloniaFact]
    public void Wrapped_line_numbers_of_an_empty_block_are_empty()
    {
        Assert.Equal(string.Empty, CodeBlockToolbar.LineNumbers(Layout(string.Empty, 100), string.Empty, 0));
    }

    [AvaloniaFact]
    public async Task Wrapped_code_renumbers_its_gutter_when_its_width_changes()
    {
        var viewer = new MarkdownViewer
        {
            ShowCodeBlockLineNumbers = true,
            WrapCodeBlockLines = true,
            Markdown = "```\nshort\n" + string.Join(" ", Enumerable.Repeat("word", 30)) + "\nend\n```",
        };
        var window = new Window { Width = 4000, Height = 400, Content = viewer };
        window.Show();
        try
        {
            var border = viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-code-block"));
            var (_, numbers, code) = LineNumbered(border.Child!);
            Assert.Equal(3, code.TextLayout.TextLines.Count);
            Assert.Equal("1\n2\n3", numbers.Text);

            window.Width = 200;
            await AsyncTestHelpers.WaitUntilAsync(() => numbers.Text != "1\n2\n3");

            var rows = code.TextLayout.TextLines.Count;
            Assert.True(rows > 4, $"expected the long line to wrap, got {rows} rows");
            Assert.Equal("1\n2" + new string('\n', rows - 3) + "\n3", numbers.Text);
            window.UpdateLayout();
            Assert.Equal(code.TextLayout.Height, numbers.TextLayout.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unwrapped_code_keeps_one_number_per_line_whatever_its_width()
    {
        var viewer = new MarkdownViewer
        {
            ShowCodeBlockLineNumbers = true,
            Markdown = "```\nshort\n" + string.Join(" ", Enumerable.Repeat("word", 30)) + "\nend\n```",
        };
        var window = new Window { Width = 200, Height = 400, Content = viewer };
        window.Show();
        try
        {
            var border = viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-code-block"));
            var (_, numbers, code) = LineNumbered(border.Child!);

            Assert.Equal(3, code.TextLayout.TextLines.Count);
            Assert.Equal("1\n2\n3", numbers.Text);
        }
        finally
        {
            window.Close();
        }
    }

    // ── FindCode ─────────────────────────────────────────────────────────────

    public static TheoryData<string, Func<Border>> OtherContents => new()
    {
        { "nothing", () => new Border() },
        { "an image", () => new Border { Child = new Image() } },
        { "a plain grid", () => new Border { Child = new Grid { Children = { new TextBlock(), new TextBlock() } } } },
        { "a single numbered text", () => new Border { Child = new Grid { Children = { new TextBlock { Classes = { "markdown-code-line-numbers" } } } } } },
        { "a grid not starting with numbers", () => new Border { Child = new Grid { Children = { new Image(), new TextBlock() } } } },
        { "numbers followed by no text", () => new Border { Child = new Grid { Children = { new TextBlock { Classes = { "markdown-code-line-numbers" } }, new Image() } } } },
        { "a plain scroll viewer", () => new Border { Child = new ScrollViewer { Content = new TextBlock() } } },
        { "a code scroll viewer around an image", () => new Border { Child = new ScrollViewer { Classes = { "markdown-code-scroll" }, Content = new Image() } } },
    };

    [AvaloniaTheory]
    [MemberData(nameof(OtherContents))]
    public void FindCode_finds_nothing_in_other_contents(string description, Func<Border> border)
    {
        Assert.NotNull(description);
        Assert.Null(CodeBlockToolbar.FindCode(border()));
    }

    // ── Viewer ────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Viewer_turns_every_option_off_by_default_and_rerenders_when_one_changes()
    {
        var viewer = new MarkdownViewer { Markdown = ThreeLines };
        Assert.False(viewer.ShowCodeBlockLineNumbers);
        Assert.False(viewer.WrapCodeBlockLines);
        Assert.Equal(double.PositiveInfinity, viewer.CodeBlockMaxHeight);
        Border Code() => viewer.GetLogicalDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-code-block"));
        Assert.IsType<TextBlock>(Code().Child);

        viewer.ShowCodeBlockLineNumbers = true;
        LineNumbered(Code().Child!);

        viewer.CodeBlockMaxHeight = 50;
        Assert.Equal(50, Code().MaxHeight);
        LineNumbered(Assert.IsType<Grid>(Scrolling(Code()).Content));

        viewer.WrapCodeBlockLines = true;
        Assert.Equal(TextWrapping.Wrap, CodeBlockToolbar.FindCode(Code())!.TextWrapping);
    }

    [AvaloniaTheory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Viewer_refuses_an_invalid_maximum_height(double maxHeight)
    {
        var viewer = new MarkdownViewer();

        Assert.Throws<ArgumentException>(() => viewer.CodeBlockMaxHeight = maxHeight);
    }

    [AvaloniaFact]
    public void Viewer_options_can_be_set_from_a_style()
    {
        var window = new Window();
        window.Styles.Add(new Style(x => x.OfType<MarkdownViewer>())
        {
            Setters =
            {
                new Setter(MarkdownViewer.ShowCodeBlockLineNumbersProperty, true),
                new Setter(MarkdownViewer.WrapCodeBlockLinesProperty, true),
                new Setter(MarkdownViewer.CodeBlockMaxHeightProperty, 90.0),
            },
        });
        var viewer = new MarkdownViewer { Markdown = ThreeLines };
        window.Content = viewer;
        window.Show();
        try
        {
            var border = viewer.GetLogicalDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-code-block"));
            Assert.Equal(90, border.MaxHeight);
            var (_, _, code) = LineNumbered(Assert.IsType<Grid>(Scrolling(border).Content));
            Assert.Equal(TextWrapping.Wrap, code.TextWrapping);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(true, false, double.PositiveInfinity)]
    [InlineData(false, true, double.PositiveInfinity)]
    [InlineData(false, false, 40)]
    [InlineData(true, true, 40)]
    public void Selecting_everything_copies_the_code_without_its_line_numbers(bool lineNumbers, bool wrap, double maxHeight)
    {
        var viewer = new MarkdownViewer
        {
            ShowCodeBlockLineNumbers = lineNumbers,
            WrapCodeBlockLines = wrap,
            CodeBlockMaxHeight = maxHeight,
            Markdown = "Intro\n\n" + ThreeLines + "\n\n- item\n\n  ```\n  nested\n  ```\n\nOutro",
        };

        viewer.SelectAll();

        Assert.Equal("Intro\nvar x = 1;\nvar y = 2;\nvar z = 3;\n• item\nnested\nOutro", viewer.GetSelectedText());
    }

    // ── Selection inside a block with a maximum height ───────────────────────

    private sealed record Shown(Window Window, MarkdownViewer Viewer, DocumentSelectionLayer Layer,
        Border Code, ScrollViewer Scroll, TextBlock CodeText, MarkdownSelectableTextBlock After) : IDisposable
    {
        public void Dispose() => Window.Close();

        public Point Center(Visual visual) =>
            visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Layer)!.Value;
    }

    private static Shown ShowScrollingCode(bool scrollingDocument = false, bool lineNumbers = false, int lines = 40)
    {
        var viewer = new MarkdownViewer
        {
            ShowCodeBlockLanguage = false,
            ShowCodeBlockCopyButton = false,
            ShowCodeBlockLineNumbers = lineNumbers,
            CodeBlockMaxHeight = 100,
            Markdown = "Before\n\n" + LongBlock(lines) + "\n\nAfter\n\n"
                + string.Join("\n\n", Enumerable.Range(1, scrollingDocument ? 40 : 0).Select(i => $"Paragraph {i}")),
        };
        if (scrollingDocument)
            viewer.Template = ScrollingViewerTemplate.Create();
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Show();
        try
        {
            var grid = Assert.IsType<Grid>(viewer.Content);
            var layer = Assert.IsType<DocumentSelectionLayer>(grid.Children[1]);
            var code = viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-code-block"));
            var after = viewer.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>().Single(tb => CodeText(tb) == "After");
            return new Shown(window, viewer, layer, code, Scrolling(code), CodeBlockToolbar.FindCode(code)!, after);
        }
        catch
        {
            window.Close();
            throw;
        }
    }

    [AvaloniaFact]
    public void Scrolling_code_is_capped_at_the_maximum_height()
    {
        using var s = ShowScrollingCode();

        Assert.Equal(100, s.Code.Bounds.Height);
        Assert.True(s.CodeText.Bounds.Height > 100);
        Assert.True(s.Scroll.Extent.Height > s.Scroll.Viewport.Height);
    }

    [AvaloniaFact]
    public void Short_code_keeps_its_own_height_under_the_maximum()
    {
        using var s = ShowScrollingCode(lines: 2);

        Assert.True(s.Code.Bounds.Height < 100);
        Assert.Equal(s.Scroll.Extent.Height, s.Scroll.Viewport.Height);
    }

    [AvaloniaFact]
    public void Hit_testing_below_scrolling_code_ignores_its_hidden_lines()
    {
        using var s = ShowScrollingCode();
        // Precondition: the code's hidden lines lie under the paragraph after the block.
        var codeBottom = s.CodeText.TranslatePoint(new Point(0, s.CodeText.Bounds.Height), s.Layer)!.Value.Y;
        Assert.True(codeBottom > s.Center(s.After).Y);

        var offset = s.Layer.HitTestOffset(s.Center(s.After));

        Assert.NotNull(offset);
        // "Before\n" + the code's text + "\n" precede "After".
        var codeLength = CodeText(s.CodeText).Length;
        Assert.InRange(offset.Value, 7 + codeLength + 1, 7 + codeLength + 1 + "After".Length);
        Assert.Same(s.After, s.Layer.HitTestEntry(s.Center(s.After))?.Element);
    }

    [AvaloniaFact]
    public void Hit_testing_scrolling_code_follows_its_scroll_offset()
    {
        using var s = ShowScrollingCode();
        var point = s.Center(s.Code);
        var before = s.Layer.HitTestOffset(point);
        Assert.InRange(before!.Value, 7, 7 + CodeText(s.CodeText).Length);

        s.Scroll.Offset = new Vector(0, 200);
        s.Window.UpdateLayout();

        var after = s.Layer.HitTestOffset(point);
        Assert.InRange(after!.Value, before.Value + 1, 7 + CodeText(s.CodeText).Length);
        var caret = s.CodeText.TextLayout.HitTestPoint(s.Layer.TranslatePoint(point, s.CodeText)!.Value);
        Assert.Equal(7 + caret.TextPosition, after);
    }

    [AvaloniaFact]
    public void Line_numbers_are_not_hit_tested()
    {
        using var s = ShowScrollingCode(lineNumbers: true);
        var numbers = s.Code.GetVisualDescendants().OfType<TextBlock>().Single(tb => tb.Classes.Contains("markdown-code-line-numbers"));

        Assert.Null(s.Layer.HitTestOffset(new Point(s.Center(numbers).X, s.Center(s.Code).Y)));
        Assert.NotNull(s.Layer.HitTestOffset(s.Center(s.Code)));
    }

    private static List<(Rect? Clip, Rect[] Rects)> RecordHighlights(DocumentSelectionLayer layer)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            layer.Render(context);

        var drawn = new List<(Rect?, Rect[])>();
        foreach (var drawing in group.Children)
        {
            switch (drawing)
            {
                case GeometryDrawing geometry:
                    drawn.Add((null, [geometry.Geometry!.Bounds]));
                    break;
                case DrawingGroup clipped:
                    drawn.Add((clipped.ClipGeometry?.Bounds,
                        clipped.Children.OfType<GeometryDrawing>().Select(d => d.Geometry!.Bounds).ToArray()));
                    break;
            }
        }
        return drawn;
    }

    [AvaloniaFact]
    public void Highlight_of_scrolling_code_is_clipped_to_the_visible_lines()
    {
        using var s = ShowScrollingCode();
        s.Layer.SelectAll();

        var drawn = RecordHighlights(s.Layer);

        // "Before" and "After" are drawn as is, the code inside its scroll viewer's viewport.
        Assert.Equal(3, drawn.Count);
        Assert.Null(drawn[0].Clip);
        Assert.Null(drawn[2].Clip);
        var presenter = s.Scroll.GetVisualDescendants().OfType<Control>().First(c => c.ClipToBounds);
        var viewport = new Rect(presenter.TranslatePoint(new Point(0, 0), s.Layer)!.Value, presenter.Bounds.Size);
        Assert.Equal(viewport, drawn[1].Clip);
        Assert.Equal(40, drawn[1].Rects.Length);
        Assert.Contains(drawn[1].Rects, r => r.Bottom > viewport.Bottom);
    }

    private static List<IndexEntry> Entries(DocumentSelectionLayer layer) =>
        (List<IndexEntry>)typeof(DocumentSelectionLayer)
            .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(layer)!;

    [AvaloniaFact]
    public void Highlight_is_drawn_again_when_scrolling_code_moves_under_it()
    {
        using var s = ShowScrollingCode();
        var code = Entries(s.Layer).Single(e => e.Element == s.CodeText);
        s.Layer.SelectAll();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var top = code.CachedBounds!.Value.Top;

        s.Scroll.Offset = new Vector(0, 200);
        s.Window.UpdateLayout();
        // The layout pass dropped the cached bounds; only drawing the highlight again fills them.
        Assert.Null(code.CachedBounds);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.Equal(top - 200, code.CachedBounds!.Value.Top);
    }

    [AvaloniaFact]
    public void Highlight_is_not_drawn_again_after_a_layout_without_selection()
    {
        using var s = ShowScrollingCode();
        var code = Entries(s.Layer).Single(e => e.Element == s.CodeText);

        s.Scroll.Offset = new Vector(0, 200);
        s.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.Null(code.CachedBounds);
    }

    // ── Mouse wheel ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Mouse_wheel_scrolls_the_code_first_then_the_document_once_the_code_reaches_its_end()
    {
        using var s = ShowScrollingCode(scrollingDocument: true);
        var document = s.Viewer.GetVisualDescendants().OfType<ScrollViewer>().Single(sv => sv.Name == "PART_ScrollViewer");
        var point = s.Code.TranslatePoint(new Point(s.Code.Bounds.Width / 2, s.Code.Bounds.Height / 2), s.Window)!.Value;
        var codeEnd = s.Scroll.Extent.Height - s.Scroll.Viewport.Height;

        s.Window.MouseWheel(point, new Vector(0, -1));
        s.Window.UpdateLayout();
        Assert.True(s.Scroll.Offset.Y > 0);
        Assert.Equal(0, document.Offset.Y);

        for (int i = 0; i < 100 && s.Scroll.Offset.Y < codeEnd; i++)
        {
            s.Window.MouseWheel(point, new Vector(0, -1));
            s.Window.UpdateLayout();
        }
        Assert.Equal(codeEnd, s.Scroll.Offset.Y);
        Assert.Equal(0, document.Offset.Y);

        s.Window.MouseWheel(point, new Vector(0, -1));
        s.Window.UpdateLayout();
        Assert.Equal(codeEnd, s.Scroll.Offset.Y);
        Assert.True(document.Offset.Y > 0);
    }

    [AvaloniaFact]
    public void Mouse_wheel_over_short_code_scrolls_the_document()
    {
        using var s = ShowScrollingCode(scrollingDocument: true, lines: 2);
        var document = s.Viewer.GetVisualDescendants().OfType<ScrollViewer>().Single(sv => sv.Name == "PART_ScrollViewer");
        var point = s.Code.TranslatePoint(new Point(s.Code.Bounds.Width / 2, s.Code.Bounds.Height / 2), s.Window)!.Value;

        s.Window.MouseWheel(point, new Vector(0, -1));
        s.Window.UpdateLayout();

        Assert.Equal(0, s.Scroll.Offset.Y);
        Assert.True(document.Offset.Y > 0);
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Default_theme_line_numbers_meet_the_text_contrast_minimum(string variant)
    {
        var theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var include = new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") };

        Assert.True(include.TryGetResource("MarkdownCodeLineNumberForeground", theme, out var foreground));
        Assert.True(include.TryGetResource("MarkdownCodeBlockBackground", theme, out var background));
        var fg = Assert.IsAssignableFrom<ISolidColorBrush>(foreground).Color;
        var bg = Assert.IsAssignableFrom<ISolidColorBrush>(background).Color;
        Assert.Equal(255, fg.A);

        var (lighter, darker) = (Math.Max(RelativeLuminance(fg), RelativeLuminance(bg)), Math.Min(RelativeLuminance(fg), RelativeLuminance(bg)));
        Assert.True((lighter + 0.05) / (darker + 0.05) >= 4.5, $"{fg} on {bg}");
    }

    [AvaloniaFact]
    public void Default_theme_gives_the_line_numbers_the_code_font_and_their_own_colour()
    {
        using var theme = new ThemeScope(ThemeVariant.Dark);
        var root = Render(ThreeLines, lineNumbers: true, maxHeight: 200, themed: true);
        var window = new Window { Content = root };
        window.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml") });
        window.Show();
        try
        {
            var border = CodeBorder(root);
            var (_, numbers, code) = LineNumbered(Assert.IsType<Grid>(Scrolling(border).Content));

            Assert.Equal(code.FontFamily, numbers.FontFamily);
            Assert.Equal(13, code.FontSize);
            Assert.Equal(code.FontSize, numbers.FontSize);
            Assert.Equal(Color.Parse("#D4D4D4"), Assert.IsAssignableFrom<ISolidColorBrush>(code.Foreground).Color);
            Assert.Equal(Color.Parse("#8A8A8A"), Assert.IsAssignableFrom<ISolidColorBrush>(numbers.Foreground).Color);
            Assert.True(numbers.Margin.Right > 0);
            // One row of numbers per row of code.
            Assert.Equal(code.Bounds.Height, numbers.Bounds.Height);
            Assert.Equal(code.TextLayout.Height, numbers.TextLayout.Height);
        }
        finally
        {
            window.Close();
        }
    }
}
