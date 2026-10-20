// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

using Markdig;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests;

/// <summary>
/// <see cref="MarkdownViewer.GetSelectedMarkdown"/>, <see cref="MarkdownViewer.CopyAsMarkdownAsync"/> and the HTML
/// written next to a copied selection.
/// </summary>
public class MarkdownViewerCopyAsMarkdownTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public DocumentSelectionLayer Layer =>
            Assert.IsType<DocumentSelectionLayer>(Assert.IsType<Grid>(Viewer.Content).Children[1]);

        /// <summary>
        /// Selects the <paramref name="occurrence"/>-th occurrence of <paramref name="text"/> in the document's flat
        /// text (each entry's text followed by its separator, an embedded control being one U+FFFC).
        /// </summary>
        public void Select(string text, int occurrence = 0)
        {
            var flat = FlatText;
            Match match = Match.Empty;
            for (var i = 0; i <= occurrence; i++)
            {
                match = Find(flat, text, i == 0 ? 0 : match.Index + 1);
                Assert.True(match.Success, $"'{text}' not found in '{flat}'");
            }
            Layer.SetSelectionForTest(match.Index, match.Index + match.Length);
        }

        /// <summary>Selects from the start of <paramref name="from"/> to the end of <paramref name="to"/>.</summary>
        public void Select(string from, string to)
        {
            var flat = FlatText;
            var start = Find(flat, from, 0);
            var end = start.Success ? Find(flat, to, start.Index) : Match.Empty;
            Assert.True(start.Success && end.Success, $"'{from}' … '{to}' not found in '{flat}'");
            Layer.SetSelectionForTest(start.Index, end.Index + end.Length);
        }

        private string FlatText => string.Concat(Layer.Entries.Select(e => e.PlainText + e.Separator));

        // A line break inside a block is Environment.NewLine in the flat text ("\r\n" on Windows), so a "\n" in
        // the searched text matches either.
        private static Match Find(string flat, string text, int start) =>
            new Regex(string.Join(@"\r?\n", text.Split('\n').Select(Regex.Escape))).Match(flat, start);

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown, Action<MarkdownViewer>? configure = null)
    {
        var viewer = new MarkdownViewer();
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 600, Height = 400, Content = viewer };
        window.Show();
        return new Fixture(window, viewer);
    }

    // ── Blocks rebuilt from their controls ────────────────────────────────────

    /// <summary>
    /// The whole document rebuilt from its controls, as a partly selected block is, rather than copied as its
    /// source: what <see cref="SelectionMarkdownWriter"/> writes without a source to copy from.
    /// </summary>
    private static string Rebuilt(Fixture f)
    {
        f.Viewer.SelectAll();
        var root = Assert.IsAssignableFrom<Panel>(Assert.IsType<Grid>(f.Viewer.Content).Children[0]);
        return SelectionMarkdownWriter.Write(f.Layer, root);
    }

    [AvaloniaTheory]
    [InlineData("# One\n\n## Two\n\n### Three\n\n#### Four\n\n##### Five\n\n###### Six")]
    [InlineData("> quoted\n>\n> after a blank line")]
    [InlineData("- a\n\n- b\n\n  more b")]
    [InlineData("1) one\n2) two\n\n10. ten\n11. eleven")]
    [InlineData("- [ ] open\n- [x] done")]
    [InlineData("Intro\n\n````\nuse ``` here\n````")]
    [InlineData("Intro\n\n```c\nint x;\n```")]
    [InlineData("| a | b |\n| --- | --- |\n| x \\| y | p<br>q |")]
    [InlineData("Line one\\\nline two")]
    [InlineData("Sup <sup>up</sup>, sub <sub>dn</sub>, <ins>ins</ins>, <mark>mark</mark> and <kbd>Ctrl</kbd>")]
    [InlineData("[a](<my file.md>), [b](<x(1).md>), [c](<>) and <https://example.com/>")]
    [InlineData("`` ` ``, ``` `` ```, `  x  ` and ` `")]
    [InlineData("Escaped \\* \\_ \\[ \\] \\< \\~ \\` \\\\ text")]
    public void Blocks_rebuilt_from_their_controls_read_back_as_the_same_markdown(string markdown)
    {
        using var f = Show(markdown, v => v.Pipeline = new MarkdownPipelineBuilder().UseSupportedExtensions().Build());

        Assert.Equal(markdown, Rebuilt(f));
    }

    [AvaloniaTheory]
    [InlineData("NOTE")]
    [InlineData("TIP")]
    [InlineData("IMPORTANT")]
    [InlineData("WARNING")]
    [InlineData("CAUTION")]
    public void An_alert_rebuilt_from_its_controls_keeps_its_kind(string kind)
    {
        var markdown = $"> [!{kind}]\n> Some text\n\nAfter";
        using var f = Show(markdown, v => v.UseAlertBlocks());

        Assert.Equal(markdown, Rebuilt(f));
    }

    [AvaloniaFact]
    public void A_citation_rebuilt_from_its_controls_is_a_cite_element()
    {
        using var f = Show("Read \"\"the book\"\" now", v => v.UseCitations());

        Assert.Equal("Read <cite>the book</cite> now", Rebuilt(f));
    }

    [AvaloniaFact]
    public async Task An_image_that_could_not_be_loaded_keeps_its_address()
    {
        using var f = Show("Intro\n\nSee ![the logo](file:///nonexistent/missing.png) here");
        await AsyncTestHelpers.WaitUntilAsync(() =>
            f.Viewer.GetVisualDescendants().OfType<Border>().Any(b => b.Classes.Contains("markdown-image-failed")));

        f.Select("Intro", "her");

        Assert.Equal("Intro\n\nSee ![the logo](file:///nonexistent/missing.png) her", f.Viewer.GetSelectedMarkdown());
    }

    // ── Whole blocks ──────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Nothing_selected_gives_nothing()
    {
        using var f = Show("Alpha");

        Assert.Equal(string.Empty, f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Selecting_everything_gives_back_the_markdown_as_written()
    {
        const string markdown = "Title\n=====\n\nSome __bold__ and _italic_ text.\n\n+ one\n+ two\n\n[docs]: https://example.com/\n";
        using var f = Show(markdown);

        f.Viewer.SelectAll();

        Assert.Equal(markdown.TrimEnd('\n'), f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Selecting_everything_keeps_image_size_hints_as_written()
    {
        const string markdown = "Before ![logo](https://example.invalid/logo.png =32x32) after";
        using var f = Show(markdown);

        f.Viewer.SelectAll();

        Assert.Equal(markdown, f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Whole_blocks_are_copied_as_their_source_with_the_text_between_them()
    {
        using var f = Show("First\n\n## Second  \n\n\n* __third__\n\nFourth");

        f.Select("Second", "third");

        Assert.Equal("## Second  \n\n\n* __third__", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Part_of_right_to_left_text_leaves_out_the_isolate_around_its_code()
    {
        using var f = Show("הפקודה `foo()` נשארת שלמה");

        f.Select("הפקודה", "נשארת");

        Assert.Equal("הפקודה `foo()` נשארת", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Whole_blocks_and_a_partial_block_at_the_end()
    {
        using var f = Show("# Title\n\nPara *one*\n\nPara two");

        f.Select("Title", "Para t");

        Assert.Equal("# Title\n\nPara *one*\n\nPara t", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void A_partial_block_at_the_start_keeps_its_block_syntax()
    {
        using var f = Show("## Heading\n\nParagraph");

        f.Select("ading", "Para");

        Assert.Equal("## ading\n\nPara", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Selecting_a_whole_heading_alone_copies_its_source()
    {
        using var f = Show("Intro\n\n### Heading ###\n\nOutro");

        f.Select("Heading");

        Assert.Equal("### Heading ###", f.Viewer.GetSelectedMarkdown());
    }

    // ── Inside a block ────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Part_of_a_heading_is_its_text_only()
    {
        using var f = Show("## Heading");

        f.Select("ead");

        Assert.Equal("ead", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Emphasis_cut_by_the_selection_is_closed()
    {
        using var f = Show("Hello __bold__ and *it* world");

        f.Select("llo bo");
        Assert.Equal("llo **bo**", f.Viewer.GetSelectedMarkdown());

        f.Select("ld and i");
        Assert.Equal("**ld** and *i*", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Strikethrough_and_inline_html_formatting()
    {
        using var f = Show("a ~~gone~~ b <sub>x</sub> c <mark>m</mark>");

        f.Select("one", "m");

        Assert.Contains("~~one~~", f.Viewer.GetSelectedMarkdown());
        Assert.Contains("<sub>x</sub>", f.Viewer.GetSelectedMarkdown());
        Assert.Contains("<mark>m</mark>", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void A_cut_link_keeps_its_address()
    {
        using var f = Show("See [the docs](https://example.com/docs) now");

        f.Select("the do");

        Assert.Equal("[the do](https://example.com/docs)", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void An_autolink_stays_an_autolink()
    {
        using var f = Show("Go to <https://example.com/> today");

        f.Select("to https://example.com/ to");

        Assert.Equal("to <https://example.com/> to", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Code_spans_are_fenced_and_text_is_escaped()
    {
        using var f = Show("use `a*b` or ``x`y`` but not a*b_c");

        f.Select("e a*b or x`y but not a*b_c");

        Assert.Equal("e `a*b` or ``x`y`` but not a\\*b\\_c", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Text_that_would_start_a_block_is_escaped()
    {
        using var f = Show("x 1. y and x # z");

        f.Select("1. y");
        Assert.Equal("1\\. y", f.Viewer.GetSelectedMarkdown());

        f.Select("# z");
        Assert.Equal("\\# z", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void A_hard_line_break_is_kept()
    {
        using var f = Show("one\\\ntwo three");

        f.Select("ne", "tw");

        Assert.Equal("ne\\\ntw", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Part_of_a_code_block_alone_is_its_raw_code()
    {
        using var f = Show("```csharp\nvar a = b * c;\nvar d = e_f;\n```");

        f.Select("b * c;\nvar d");

        Assert.Equal("b * c;\nvar d", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Part_of_a_code_block_with_other_blocks_keeps_its_fence_and_language()
    {
        using var f = Show("Intro text\n\n```csharp\nvar a = 1;\nvar b = 2;\n```");

        f.Select("text", "var b");

        Assert.Equal("text\n\n```csharp\nvar a = 1;\nvar b\n```", f.Viewer.GetSelectedMarkdown());
    }

    // ── Containers ────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Part_of_a_list_keeps_its_bullets()
    {
        using var f = Show("* one\n* two\n* three\n\nafter");

        f.Select("ne", "tw");

        Assert.Equal("- ne\n- tw", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Part_of_an_ordered_list_keeps_its_numbers()
    {
        using var f = Show("3. three\n4. four\n5. five\n\nafter");

        f.Select("hree", "fo");

        Assert.Equal("3. hree\n4. fo", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Part_of_a_task_list_keeps_its_boxes()
    {
        using var f = Show("- [x] done\n- [ ] todo\n\nafter");

        f.Select("one", "to");

        Assert.Equal("- [x] one\n- [ ] to", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void A_nested_list_is_indented_under_its_item()
    {
        using var f = Show("- outer\n  - inner\n- last\n\nafter");

        f.Select("ter", "la");

        Assert.Equal("- ter\n  - inner\n- la", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Part_of_a_quote_keeps_its_markers()
    {
        using var f = Show("> quoted **text**\n> more\n\nafter");

        f.Select("ted", "aft");

        Assert.Equal("> ted **text** more\n\naft", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Part_of_an_alert_keeps_its_kind()
    {
        using var f = Show("> [!WARNING]\n> Be careful\n\nafter", v => v.UseAlertBlocks());

        f.Select("careful", "aft");

        Assert.Equal("> [!WARNING]\n> careful\n\naft", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void Part_of_a_table_keeps_its_header_and_rows()
    {
        using var f = Show("| A | B |\n|---|---|\n| a1 | b1 |\n| a2 | b2 |\n\nafter");

        f.Select("1\tb1", "aft");

        Assert.Equal("| A | B |\n| --- | --- |\n| 1 | b1 |\n| a2 | b2 |\n\naft", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void A_pipe_in_a_table_cell_is_escaped()
    {
        using var f = Show("| A |\n|---|\n| x \\| y |\n| z |\n\nafter");

        f.Select("| y", "z");

        Assert.Equal("| A |\n| --- |\n| \\| y |\n| z |", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void A_cut_image_keeps_its_alt_text_and_address()
    {
        using var f = Show("See ![the logo](https://example.invalid/logo.png) here");

        f.Select("e \uFFFC h");

        Assert.Equal("e ![the logo](https://example.invalid/logo.png) h", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void A_block_control_is_copied_as_its_copy_text()
    {
        using var f = Show("Intro text\n\n```chart\npie\n```\n\nOutro text",
            v => v.UseFenceRenderer("chart", (code, _) => new Border { Child = new TextBlock { Text = code } }));

        f.Select("text", "Out");

        Assert.Equal("text\n\n```chart\npie\n```\n\nOut", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void A_collapsed_alert_is_left_out_like_its_text()
    {
        using var f = Show("Before text\n\n> [!NOTE]-\n> Hidden body\n\nAfter text", v => v.UseAlertBlocks());

        f.Select("text", "Aft");

        Assert.Equal("text\n\nAft", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void A_toggled_task_is_copied_in_its_new_state()
    {
        using var f = Show("- [ ] todo\n\nafter", v => v.AreTaskListsInteractive = true);
        var toggle = f.Viewer.GetVisualDescendants().OfType<ToggleButton>().Single();
        toggle.IsChecked = true;

        f.Viewer.SelectAll();
        Assert.Equal("- [x] todo\n\nafter", f.Viewer.GetSelectedMarkdown());

        f.Select("do", "aft");
        Assert.Equal("- [x] do\n\naft", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void While_streaming_everything_selected_is_the_text_so_far()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("# Title\n\nSome **bo");
        session.ApplyPendingUpdate();

        f.Viewer.SelectAll();

        Assert.Equal("# Title\n\nSome **bo", f.Viewer.GetSelectedMarkdown());
    }

    [AvaloniaFact]
    public void While_streaming_the_kept_blocks_are_copied_as_their_source()
    {
        using var f = Show("");
        var session = f.Viewer.BeginStreaming();
        session.Append("First __one__\n\nSecond *two*\n\nThird");
        session.ApplyPendingUpdate();

        f.Select("First", "Th");

        Assert.Equal("First __one__\n\nSecond *two*\n\nTh", f.Viewer.GetSelectedMarkdown());
    }

    // ── Clipboard ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task CopyAsMarkdownAsync_writes_the_markdown_as_text()
    {
        using var f = Show("Some **bold** text\n\n- item");
        var raised = new List<(MarkdownCopyOrigin, string, string?)>();
        f.Viewer.CopyingToClipboard += (_, e) => raised.Add((e.Origin, e.Text, e.Html));
        f.Select("bold", "it");

        await f.Viewer.CopyAsMarkdownAsync();

        Assert.Equal([(MarkdownCopyOrigin.SelectionAsMarkdown, "**bold** text\n\n- it", null)], raised);
        Assert.Equal("**bold** text\n\n- it".ReplaceLineEndings(), await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public async Task CopyAsMarkdownAsync_with_nothing_selected_does_nothing()
    {
        using var f = Show("Alpha");
        await f.Window.Clipboard!.SetTextAsync("keep");

        await f.Viewer.CopyAsMarkdownAsync();

        Assert.Equal("keep", await f.Window.Clipboard!.TryGetTextAsync());
    }

    private static async Task<string?> ClipboardHtml(Window window) =>
        await window.Clipboard!.TryGetDataAsync() is { } data ? await HtmlClipboardFormat.TryGetAsync(data) : null;

    [AvaloniaFact]
    public async Task Copying_the_selection_writes_html_next_to_the_text()
    {
        using var f = Show("Some **bold** and [a link](https://example.com/)\n\n- item");
        string? eventHtml = null;
        f.Viewer.CopyingToClipboard += (_, e) => eventHtml = e.Html;
        f.Viewer.SelectAll();

        await f.Viewer.CopyToClipboardAsync();

        const string expected = "<p>Some <strong>bold</strong> and <a href=\"https://example.com/\">a link</a></p>\n<ul>\n<li>item</li>\n</ul>\n";
        Assert.Equal(expected, eventHtml);
        Assert.Equal("Some bold and a link\n• item".ReplaceLineEndings(), await f.Window.Clipboard!.TryGetTextAsync());
        Assert.Equal(expected, await ClipboardHtml(f.Window));
    }

    [AvaloniaFact]
    public async Task Html_of_a_partial_selection_has_only_the_selected_part()
    {
        using var f = Show("Hello **world** and more");
        string? eventHtml = null;
        f.Viewer.CopyingToClipboard += (_, e) => eventHtml = e.Html;
        f.Select("lo wor");

        await f.Viewer.CopyToClipboardAsync();

        Assert.Equal("<p>lo <strong>wor</strong></p>\n", eventHtml);
    }

    [AvaloniaFact]
    public async Task Relative_addresses_in_the_html_are_resolved_against_the_base_uri()
    {
        using var f = Show("[guide](docs/guide.md) and [top](#top)", v => v.BaseUri = new Uri("https://example.com/repo/"));
        string? eventHtml = null;
        f.Viewer.CopyingToClipboard += (_, e) => eventHtml = e.Html;
        f.Viewer.SelectAll();

        await f.Viewer.CopyToClipboardAsync();

        Assert.Contains("href=\"https://example.com/repo/docs/guide.md\"", eventHtml);
        Assert.Contains("href=\"#top\"", eventHtml);
    }

    [AvaloniaFact]
    public async Task No_html_is_written_when_html_copy_is_turned_off()
    {
        using var f = Show("Some **bold**", v => v.IsHtmlCopyEnabled = false);
        var raised = 0;
        f.Viewer.CopyingToClipboard += (_, e) => { raised++; Assert.Null(e.Html); };
        f.Viewer.SelectAll();

        await f.Viewer.CopyToClipboardAsync();

        Assert.Equal(1, raised);
        Assert.Equal("Some bold", await f.Window.Clipboard!.TryGetTextAsync());
        Assert.Null(await ClipboardHtml(f.Window));
    }

    [AvaloniaFact]
    public async Task A_handler_can_replace_or_drop_the_html()
    {
        using var f = Show("Some **bold**");
        f.Viewer.SelectAll();

        f.Viewer.CopyingToClipboard += Replace;
        await f.Viewer.CopyToClipboardAsync();
        Assert.Equal("<b>mine</b>", await ClipboardHtml(f.Window));
        f.Viewer.CopyingToClipboard -= Replace;

        f.Viewer.CopyingToClipboard += (_, e) => e.Html = null;
        await f.Viewer.CopyToClipboardAsync();
        Assert.Null(await ClipboardHtml(f.Window));
        Assert.Equal("Some bold", await f.Window.Clipboard!.TryGetTextAsync());

        static void Replace(object? sender, CopyingToClipboardEventArgs e) => e.Html = "<b>mine</b>";
    }

    // ── Windows HTML format ───────────────────────────────────────────────────

    [Fact]
    public void The_windows_html_format_gives_the_byte_offsets_of_the_fragment()
    {
        const string fragment = "<p>café ☕</p>";

        var data = HtmlClipboardFormat.ToWindowsFormat(fragment);

        var text = System.Text.Encoding.UTF8.GetString(data);
        Assert.StartsWith("Version:0.9\r\nStartHTML:", text);
        int Offset(string name) => int.Parse(text.Substring(text.IndexOf(name) + name.Length, 10));
        Assert.Equal("<html>", System.Text.Encoding.UTF8.GetString(data, Offset("StartHTML:"), 6));
        Assert.Equal(data.Length, Offset("EndHTML:"));
        Assert.Equal(fragment, HtmlClipboardFormat.FragmentOf(data));
    }

    [Fact]
    public void A_windows_html_format_fragment_can_end_with_the_data()
    {
        // Offsets at the very start and the very end, the last one with no line end after it.
        const string data = "StartFragment:0\nEndFragment:30";

        Assert.Equal(data, HtmlClipboardFormat.FragmentOf(System.Text.Encoding.UTF8.GetBytes(data)));
    }

    [Fact]
    public void A_windows_html_format_without_offsets_has_no_fragment()
    {
        Assert.Null(HtmlClipboardFormat.FragmentOf(System.Text.Encoding.UTF8.GetBytes("Version:0.9\r\n<p>x</p>")));
        Assert.Null(HtmlClipboardFormat.FragmentOf(System.Text.Encoding.UTF8.GetBytes("StartFragment:0000000099\r\nEndFragment:0000000001\r\n")));
    }
}
