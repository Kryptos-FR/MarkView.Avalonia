// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;

using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Finds the rendered table a context menu is opened on, and writes it out whole, as tab-separated values or as
/// Markdown, whatever is selected.
/// </summary>
internal static class TableCopy
{
    /// <summary>Whether <paramref name="control"/> is a table rendered by <see cref="Blocks.TableRenderer"/>.</summary>
    public static bool IsTable(Control control) => control is Grid grid && grid.Classes.Contains("markdown-table");

    /// <summary>
    /// The innermost table of the document under <paramref name="root"/> that contains <paramref name="position"/>,
    /// a position in <paramref name="relativeTo"/>, or <c>null</c>.
    /// </summary>
    public static Grid? TableAt(Panel root, Visual relativeTo, Point position)
    {
        Grid? found = null;
        foreach (var table in root.GetLogicalDescendants().OfType<Grid>().Where(IsTable))
        {
            if (!table.IsEffectivelyVisible || table.TranslatePoint(default, relativeTo) is not { } origin)
                continue;
            if (new Rect(origin, table.Bounds.Size).Contains(position))
                found = table;
        }
        return found;
    }

    /// <summary>The table that shows <paramref name="link"/>, or <c>null</c>.</summary>
    public static Grid? TableOf(MarkdownHyperlink link) =>
        link.GetLogicalAncestors().OfType<Grid>().FirstOrDefault(IsTable);

    /// <summary>
    /// The table's text as tab-separated values, one line per row, with lines separated by <c>'\n'</c>: what a
    /// spreadsheet takes as cells when it is pasted. A cell's text is the one selection copies, with any tab or
    /// line break in it replaced by a space; a cell that spans several columns is followed by empty ones.
    /// </summary>
    public static string ToTabSeparatedValues(Grid table, DocumentSelectionLayer layer)
    {
        var entries = new Dictionary<Control, IndexEntry>();
        foreach (var entry in layer.Entries)
            entries.TryAdd(entry.Element, entry);

        // A table always has its header row.
        var cells = table.Children.OfType<Border>().ToList();
        var columns = cells.Max(c => Grid.GetColumn(c) + Grid.GetColumnSpan(c));

        var lines = new List<string>();
        foreach (var row in cells.GroupBy(Grid.GetRow).OrderBy(r => r.Key))
        {
            var texts = new string[columns];
            foreach (var cell in row)
            {
                var text = MarkdownViewer.FindFirstTextBlock(cell) is { } first && entries.TryGetValue(first, out var entry)
                    ? EntryText(entry)
                    : string.Empty;
                texts[Grid.GetColumn(cell)] = string.Join(' ', text.Split(['\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
            }
            lines.Add(string.Join('\t', texts.Select(t => t ?? string.Empty)));
        }
        return string.Join('\n', lines);
    }

    /// <summary>The entry's text, with each embedded control's copy text in its place.</summary>
    private static string EntryText(IndexEntry entry)
    {
        if (entry.EmbeddedText is not { } embedded)
            return entry.PlainText;
        var sb = new StringBuilder();
        for (var i = 0; i < entry.PlainText.Length; i++)
        {
            if (embedded.TryGetValue(i, out var text)) sb.Append(text);
            else sb.Append(entry.PlainText[i]);
        }
        return sb.ToString();
    }
}
