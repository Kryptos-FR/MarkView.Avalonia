// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;
using System.Text.RegularExpressions;

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;

using Markdig.Syntax;

using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Writes the selected part of a rendered document as Markdown. A top-level block that is selected whole
/// is copied as its source text. A block that is only partly selected is rebuilt from its controls, for the
/// selected part only: its text with its emphasis, code, links and images, inside its heading, list item,
/// quote, alert, code block or table row. The rebuilt part is normalised Markdown (<c>**bold**</c>,
/// <c>*italic*</c>, <c>-</c> bullets), not the source's own characters.
/// </summary>
internal sealed partial class SelectionMarkdownWriter
{
    private readonly DocumentSelectionLayer _layer;
    private readonly int _start;
    private readonly int _end;
    private readonly Dictionary<Control, IndexEntry> _entries = [];

    private SelectionMarkdownWriter(DocumentSelectionLayer layer, int start, int end)
    {
        _layer = layer;
        _start = start;
        _end = end;
        foreach (var entry in layer.Entries)
            _entries.TryAdd(entry.Element, entry);
    }

    /// <summary>
    /// Returns the selection of <paramref name="layer"/>, whose entries were registered from the children of
    /// <paramref name="root"/>, as Markdown with lines separated by <c>'\n'</c>. <paramref name="source"/>
    /// returns the source text of a run of top-level blocks, from the first's start to the last's end;
    /// <paramref name="wholeSource"/> is the source of the whole document, returned when every block is
    /// selected. Without them, every block is rebuilt from its controls.
    /// </summary>
    public static string Write(DocumentSelectionLayer layer, Panel root, Func<Block, Block, string>? source = null,
        string? wholeSource = null)
    {
        if (layer.SelectedRange is not var (start, end))
            return string.Empty;
        return new SelectionMarkdownWriter(layer, start, end).Write(root, source, wholeSource).ReplaceLineEndings("\n");
    }

    /// <summary>
    /// Returns the whole of <paramref name="table"/>, a table of the document whose entries <paramref name="layer"/>
    /// holds, as a Markdown table rebuilt from its controls, with lines separated by <c>'\n'</c>, whatever is
    /// selected.
    /// </summary>
    public static string WriteTable(DocumentSelectionLayer layer, Grid table)
    {
        var cells = layer.Entries.Where(e => e.Element.GetLogicalAncestors().Contains(table)).ToList();
        if (cells.Count == 0)
            return string.Empty;
        return new SelectionMarkdownWriter(layer, cells[0].AbsStart, cells[^1].AbsEnd).WriteTable(table)
            .ReplaceLineEndings("\n");
    }

    private string Write(Panel root, Func<Block, Block, string>? source, string? wholeSource)
    {
        var groups = Groups(_layer.Entries);
        var selected = groups.Where(g => g.Entries.Any(IsSelected)).ToList();
        if (selected.Count == 0)
            return string.Empty;

        if (wholeSource is not null && groups.All(IsWhole))
            return wholeSource.TrimEnd('\r', '\n');

        // Within a single entry that is not a whole block, only the text is copied, with its inline
        // syntax: a few words of a heading are not a heading.
        var selectedEntries = selected.SelectMany(g => g.Entries).Where(IsSelected).ToList();
        if (selectedEntries.Count == 1 && !(source is not null && IsWhole(selected[0])))
            return WriteEntryText(selectedEntries[0]);

        var parts = new List<string>();
        Block? runFirst = null, runLast = null;
        foreach (var group in selected)
        {
            if (source is not null && IsWhole(group))
            {
                runFirst ??= group.Block;
                runLast = group.Block;
                continue;
            }
            if (runFirst is not null)
            {
                parts.Add(source!(runFirst, runLast!));
                runFirst = runLast = null;
            }
            parts.Add(WriteBlocks(root.Children.Where(c => group.Block is not null
                ? AvaloniaRenderer.GetTopLevelBlock(c) == group.Block
                : ContainsAny(c, group.Entries))));
        }
        if (runFirst is not null)
            parts.Add(source!(runFirst, runLast!));

        return string.Join("\n\n", parts.Where(p => p.Length > 0));
    }

    // ── Top-level blocks ──────────────────────────────────────────────────────

    private sealed record Group(Block? Block, List<IndexEntry> Entries);

    /// <summary>The entries grouped by the top-level block they render part of, in document order.</summary>
    private static List<Group> Groups(IReadOnlyList<IndexEntry> entries)
    {
        var groups = new List<Group>();
        foreach (var entry in entries)
        {
            if (groups.Count > 0 && groups[^1].Block is { } block && block == entry.TopLevelBlock)
                groups[^1].Entries.Add(entry);
            else
                groups.Add(new Group(entry.TopLevelBlock, [entry]));
        }
        return groups;
    }

    /// <summary>Whether the whole text of a known top-level block is selected.</summary>
    private bool IsWhole(Group group) =>
        group.Block is not null && _start <= group.Entries[0].AbsStart && _end >= group.Entries[^1].AbsEnd;

    private static bool ContainsAny(Control control, List<IndexEntry> entries) =>
        entries.Any(e => e.Element == control || e.Element.GetLogicalAncestors().Contains(control));

    // ── Entries ───────────────────────────────────────────────────────────────

    /// <summary>Whether some of the entry's text is selected and shown. An empty entry counts as a position.</summary>
    private bool IsSelected(IndexEntry entry) =>
        entry.AbsStart < _end && Math.Max(entry.AbsEnd, entry.AbsStart + 1) > _start && !_layer.IsHidden(entry);

    private bool IsSelected(Control control, out IndexEntry entry) =>
        _entries.TryGetValue(control, out entry!) && IsSelected(entry);

    /// <summary>The selected part of the entry's text, as offsets in it.</summary>
    private (int Start, int End) LocalRange(IndexEntry entry) =>
        (Math.Max(_start - entry.AbsStart, 0), Math.Min(_end - entry.AbsStart, entry.PlainText.Length));

    /// <summary>The selected text of an entry selected on its own, with its inline syntax.</summary>
    private string WriteEntryText(IndexEntry entry)
    {
        var (start, end) = LocalRange(entry);
        if (entry.TextBlock is null)
            return entry.EmbeddedText?[0] ?? string.Empty;
        if (IsCode(entry.TextBlock))
            return RawText(entry, start, end);
        if (entry.IsMarker)
            return string.Empty;
        return EscapeLineStarts(WriteEntryInlines(entry, start, end));
    }

    /// <summary>The entry's text between the offsets, with each embedded control's copy text and no escaping.</summary>
    private static string RawText(IndexEntry entry, int start, int end)
    {
        var sb = new StringBuilder();
        for (var i = start; i < end; i++)
        {
            if (entry.EmbeddedText is { } embedded && embedded.TryGetValue(i, out var text)) sb.Append(text);
            else sb.Append(entry.PlainText[i]);
        }
        return sb.ToString();
    }

    private static bool IsCode(TextBlock textBlock) =>
        textBlock.FindLogicalAncestorOfType<Border>() is { } border && border.Classes.Contains("markdown-code-block");

    /// <summary>
    /// The entry's inlines between the offsets. A table cell's entry covers every text block of its cell,
    /// joined with spaces, as <see cref="MarkdownViewer"/> registers it.
    /// </summary>
    private string WriteEntryInlines(IndexEntry entry, int start, int end)
    {
        var sb = new StringBuilder();
        var position = 0;
        var textBlock = entry.TextBlock!;
        if (textBlock.FindLogicalAncestorOfType<Border>() is { } cell && cell.Classes.Contains("markdown-table-cell")
            && cell.Child is Panel panel)
            WritePanelInlines(sb, panel, ref position, start, end);
        else if (textBlock.Inlines is { } inlines)
            WriteInlines(sb, inlines, ref position, start, end);
        else
            sb.Append(Escape(entry.PlainText[start..end]));
        return sb.ToString();
    }

    private void WritePanelInlines(StringBuilder sb, Panel panel, ref int position, int start, int end)
    {
        foreach (var child in panel.Children)
        {
            switch (child)
            {
                case MarkdownSelectableTextBlock textBlock:
                    if (string.IsNullOrEmpty(MarkdownSelectableTextBlock.ExtractPlainText(textBlock.Inlines!)))
                        continue;
                    if (position > 0)
                    {
                        if (position >= start && position < end) sb.Append(' ');
                        position++;
                    }
                    WriteInlines(sb, textBlock.Inlines!, ref position, start, end);
                    break;
                case Panel nested:
                    WritePanelInlines(sb, nested, ref position, start, end);
                    break;
            }
        }
    }

    // ── Inlines ───────────────────────────────────────────────────────────────

    private static void WriteInlines(StringBuilder sb, InlineCollection inlines, ref int position, int start, int end)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                {
                    var text = run.Text ?? string.Empty;
                    var from = Math.Clamp(start - position, 0, text.Length);
                    var to = Math.Clamp(end - position, 0, text.Length);
                    if (to > from)
                    {
                        var part = text[from..to];
                        if (run.Classes.Contains("markdown-code-inline")) sb.Append(CodeSpan(part));
                        else if (run.Classes.Contains("markdown-task-list")) sb.Append(TaskMarker(part));
                        else sb.Append(Escape(part));
                    }
                    position += text.Length;
                    break;
                }
                case LineBreak:
                    if (position >= start && position < end) sb.Append("\\\n");
                    position += Environment.NewLine.Length;
                    break;
                case Span span:
                {
                    var inner = new StringBuilder();
                    WriteInlines(inner, span.Inlines, ref position, start, end);
                    if (inner.Length > 0)
                        sb.Append(Wrap(span, inner.ToString()));
                    break;
                }
                case InlineUIContainer container:
                    if (position >= start && position < end && container.Child is { } child)
                        sb.Append(EmbeddedControl(child));
                    position++;
                    break;
            }
        }
    }

    /// <summary>The Markdown (or inline HTML, for what Markdown has no syntax for) of a span around its content.</summary>
    private static string Wrap(Span span, string content)
    {
        switch (span)
        {
            case Bold: return $"**{content}**";
            case Italic: return $"*{content}*";
            case Underline: return $"<ins>{content}</ins>";
            case MarkdownHyperlink link when link.Classes.Contains("markdown-footnote-ref"):
                return content;
            case MarkdownHyperlink { NavigateUri: { } uri }:
                var url = uri.OriginalString;
                return content == Escape(url) && uri.IsAbsoluteUri ? $"<{url}>" : $"[{content}]({LinkDestination(url)})";
        }
        if (span.Classes.Contains("markdown-subscript")) return $"<sub>{content}</sub>";
        if (span.Classes.Contains("markdown-superscript")) return $"<sup>{content}</sup>";
        if (span.Classes.Contains("markdown-marked")) return $"<mark>{content}</mark>";
        if (span.Classes.Contains("markdown-citation")) return $"<cite>{content}</cite>";
        if (span.TextDecorations is { } decorations && decorations.Any(d => d.Location == TextDecorationLocation.Strikethrough))
            return $"~~{content}~~";
        return content;
    }

    /// <summary>The Markdown for a control embedded in text: an image, a key, a formula, an abbreviation.</summary>
    private static string EmbeddedControl(Control child)
    {
        var copyText = MarkdownSelection.GetCopyText(child) ?? string.Empty;
        if (child.Classes.Contains("markdown-math-inline"))
            return copyText;
        if (child.Classes.Contains("markdown-html-kbd"))
            return $"<kbd>{System.Net.WebUtility.HtmlEncode(copyText)}</kbd>";
        if (FindImageUrl(child) is { } url)
            return $"![{Escape(copyText)}]({LinkDestination(url)})";
        return Escape(copyText);
    }

    /// <summary>
    /// The address of the image <paramref name="control"/> shows, itself or one of its descendants, or of the
    /// placeholder that replaced it when it could not be loaded.
    /// </summary>
    private static string? FindImageUrl(Control control)
    {
        foreach (var candidate in control.GetLogicalDescendants().OfType<Control>().Prepend(control))
        {
            if ((candidate.Classes.Contains("markdown-image") || candidate.Classes.Contains("markdown-image-failed"))
                && candidate.Tag is string url)
                return url;
        }
        return null;
    }

    private static string LinkDestination(string url) =>
        url.Length == 0 || url.Any(c => c is ' ' or '(' or ')' or '<' or '>') ? $"<{url.Replace("<", "%3C").Replace(">", "%3E")}>" : url;

    private static string TaskMarker(string glyph) => glyph switch
    {
        TaskCheckGlyph.CheckedGlyph => "[x]",
        TaskCheckGlyph.UncheckedGlyph => "[ ]",
        _ => Escape(glyph),
    };

    /// <summary>A code span holding <paramref name="code"/>, fenced with more backticks than any run inside it.</summary>
    private static string CodeSpan(string code)
    {
        var fence = new string('`', LongestRun(code, '`') + 1);
        var pad = code.StartsWith('`') || code.EndsWith('`') || (code.Length > 1 && code[0] == ' ' && code[^1] == ' ') ? " " : "";
        return fence + pad + code + pad + fence;
    }

    private static int LongestRun(string text, char c)
    {
        int longest = 0, current = 0;
        foreach (var ch in text)
        {
            current = ch == c ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }
        return longest;
    }

    /// <summary>Escapes the characters that would start inline syntax.</summary>
    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '~')
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    // A line that would start a heading, a quote, a list item or a thematic break.
    [GeneratedRegex(@"^(?:(?<c>#{1,6}|[>+-])(?=\s|$)|\d{1,9}(?<c>[.)])(?=\s|$))", RegexOptions.Multiline)]
    private static partial Regex BlockStartRegex();

    /// <summary>Escapes what would make a line of text start a block.</summary>
    private static string EscapeLineStarts(string text) =>
        BlockStartRegex().Replace(text, m => m.Value.Insert(m.Groups["c"].Index - m.Index, "\\"));

    // ── Blocks ────────────────────────────────────────────────────────────────

    /// <summary>The selected part of each block control, separated by blank lines.</summary>
    private string WriteBlocks(IEnumerable<Control> controls, string separator = "\n\n") =>
        string.Join(separator, controls.Select(WriteBlock).Where(b => b.Length > 0));

    /// <summary>The selected part of a block control, empty when nothing in it is selected.</summary>
    private string WriteBlock(Control control)
    {
        switch (control)
        {
            case Control blockControl when MarkdownSelection.GetCopyText(blockControl) is not null:
                return IsSelected(blockControl, out var blockEntry) ? blockEntry.EmbeddedText?[0] ?? string.Empty : string.Empty;
            case MarkdownSelectableTextBlock textBlock:
                return IsSelected(textBlock, out var entry) ? HeadingPrefix(textBlock) + WriteTextBlock(entry) : string.Empty;
            case Border border when border.Classes.Contains("markdown-code-block"):
                return CodeBlockToolbar.FindCode(border) is { } code && IsSelected(code, out var codeEntry)
                    ? CodeFence(border, codeEntry)
                    : string.Empty;
            case Panel list when list.Classes.Contains("markdown-list"):
                return WriteList(list);
            case Grid table when table.Classes.Contains("markdown-table"):
                return WriteTable(table);
            case Border { Child: Panel panel } border when border.Classes.Contains("markdown-alert"):
                var alert = WriteBlocks(panel.Children.Where(c => c.Classes.Contains("markdown-alert-content"))
                    .OfType<Panel>().SelectMany(p => p.Children));
                return alert.Length == 0 ? alert : Quote($"[!{AlertKind(border)}]\n{alert}");
            case Border { Child: Panel panel } border when border.Classes.Contains("markdown-blockquote"):
                return Quote(WriteBlocks(panel.Children));
            case Border { Child: Panel panel }:
                return WriteBlocks(panel.Children);
            case Panel panel:
                return WriteBlocks(panel.Children);
            default:
                return string.Empty;
        }
    }

    private string WriteTextBlock(IndexEntry entry)
    {
        var (start, end) = LocalRange(entry);
        return EscapeLineStarts(WriteEntryInlines(entry, start, end));
    }

    private static string HeadingPrefix(TextBlock textBlock)
    {
        for (var level = 1; level <= 6; level++)
        {
            if (textBlock.Classes.Contains($"markdown-h{level}"))
                return new string('#', level) + " ";
        }
        return string.Empty;
    }

    private string CodeFence(Border border, IndexEntry entry)
    {
        var (start, end) = LocalRange(entry);
        var code = RawText(entry, start, end).ReplaceLineEndings("\n");
        var language = border.Classes.FirstOrDefault(c => c.StartsWith("language-", StringComparison.Ordinal))?["language-".Length..];
        var fence = new string('`', Math.Max(3, LongestRun(code, '`') + 1));
        return $"{fence}{language}\n{code}\n{fence}";
    }

    private static string AlertKind(Border border) =>
        border.Classes.Select(c => c.StartsWith("markdown-alert-", StringComparison.Ordinal) ? c["markdown-alert-".Length..] : null)
            .FirstOrDefault(kind => kind is "note" or "tip" or "important" or "warning" or "caution")?.ToUpperInvariant() ?? "NOTE";

    private static string Quote(string content) =>
        string.Join('\n', content.Split('\n').Select(line => line.Length == 0 ? ">" : "> " + line));

    private static string Indent(string content, int width) =>
        string.Join('\n', content.Split('\n').Select((line, i) => i == 0 || line.Length == 0 ? line : new string(' ', width) + line));

    private string WriteList(Panel list)
    {
        var separator = list.Classes.Contains("markdown-list-loose") ? "\n\n" : "\n";
        var items = new List<string>();
        foreach (var item in list.Children.OfType<Grid>())
        {
            var content = item.Children.OfType<Panel>().FirstOrDefault(p => Grid.GetColumn(p) == 1);
            if (content is null) continue;
            var text = WriteBlocks(content.Children, separator);
            if (text.Length == 0) continue;

            var marker = ListMarker(item);
            items.Add(marker + " " + Indent(text, marker.Length + 1));
        }
        return string.Join(separator, items);
    }

    /// <summary>The Markdown marker of a list item: its number, a bullet, or a bullet and its task box.</summary>
    private static string ListMarker(Grid item)
    {
        var markerControl = item.Children.FirstOrDefault(c => Grid.GetColumn(c) == 0);
        var glyph = markerControl switch
        {
            ToggleButton { Content: TextBlock content } => content.Text,
            TextBlock textBlock => textBlock.Text,
            _ => null,
        };
        return glyph switch
        {
            TaskCheckGlyph.CheckedGlyph => "- [x]",
            TaskCheckGlyph.UncheckedGlyph => "- [ ]",
            { Length: > 1 } number when char.IsAsciiDigit(number[0]) && number[^1] is '.' or ')' => number,
            _ => "-",
        };
    }

    private string WriteTable(Grid table)
    {
        var cells = table.Children.OfType<Border>().ToList();
        if (cells.Count == 0) return string.Empty;
        var columns = cells.Max(Grid.GetColumn) + 1;

        var rows = new List<string>();
        var headerSelected = false;
        foreach (var row in cells.GroupBy(Grid.GetRow).OrderBy(r => r.Key))
        {
            var texts = new string[columns];
            var any = false;
            foreach (var cell in row)
            {
                var text = string.Empty;
                if (MarkdownViewer.FindFirstTextBlock(cell) is { } first && IsSelected(first, out var entry))
                {
                    var (start, end) = LocalRange(entry);
                    text = WriteEntryInlines(entry, start, end);
                    any = true;
                }
                texts[Grid.GetColumn(cell)] = text.Replace("|", "\\|").Replace("\\\n", "<br>").Replace("\n", " ");
            }
            if (!any) continue;
            if (row.Key == 0) headerSelected = true;
            rows.Add(Row(texts));
        }
        if (rows.Count == 0) return string.Empty;

        // A table needs its header row; a selection that starts below it still gets it, as written.
        var delimiter = Row(Enumerable.Repeat("---", columns));
        if (headerSelected)
            rows.Insert(1, delimiter);
        else
        {
            var header = new string[columns];
            foreach (var cell in cells.Where(c => Grid.GetRow(c) == 0))
                header[Grid.GetColumn(cell)] = Escape(MarkdownViewer.FindFirstTextBlock(cell)?.Inlines is { } inlines
                    ? MarkdownSelectableTextBlock.ExtractPlainText(inlines) : string.Empty).Replace("|", "\\|");
            rows.Insert(0, Row(header));
            rows.Insert(1, delimiter);
        }
        return string.Join('\n', rows);
    }

    private static string Row(IEnumerable<string?> cells) => "| " + string.Join(" | ", cells.Select(c => c ?? "")) + " |";
}
