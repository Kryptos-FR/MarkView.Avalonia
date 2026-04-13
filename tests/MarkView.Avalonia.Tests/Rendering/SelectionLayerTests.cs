using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MarkView.Avalonia.Rendering;
using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class SelectionLayerTests
{
    // ── SelectAll / ClearSelection ────────────────────────────────────────────

    [AvaloniaFact]
    public void SelectAll_covers_all_registered_entries()
    {
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("Hello"), "Hello", "\n"));
        layer.Register(new IndexEntry(MakeBlock("World"), "World", "\n"));

        layer.SelectAll();

        // SelectAll ends at AbsEnd of last entry (before trailing separator)
        Assert.Equal("Hello\nWorld", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void ClearSelection_returns_empty_text()
    {
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("Hello"), "Hello", "\n"));
        layer.SelectAll();
        layer.ClearSelection();

        Assert.Equal(string.Empty, layer.GetSelectedText());
    }

    // ── Single-entry selection ────────────────────────────────────────────────

    [AvaloniaFact]
    public void GetSelectedText_single_entry_partial()
    {
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("Hello World"), "Hello World", "\n"));

        // AbsStart=0, select chars 0..5 = "Hello"
        layer.SetSelectionForTest(0, 5);

        Assert.Equal("Hello", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void GetSelectedText_empty_when_anchor_equals_focus()
    {
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("Hello"), "Hello", "\n"));

        layer.SetSelectionForTest(2, 2);

        Assert.Equal(string.Empty, layer.GetSelectedText());
    }

    // ── Cross-entry selection ─────────────────────────────────────────────────

    [AvaloniaFact]
    public void GetSelectedText_cross_entry_with_newline_separator()
    {
        // "Hello\n" = offsets 0-5 text, 5 = '\n' separator
        // "World"   = offsets 6-10
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("Hello"), "Hello", "\n"));
        layer.Register(new IndexEntry(MakeBlock("World"), "World", "\n"));

        // select from offset 3 ("lo") through offset 9 ("Wor")
        layer.SetSelectionForTest(3, 9);

        Assert.Equal("lo\nWor", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void GetSelectedText_cross_entry_excludes_separator_when_focus_before_separator()
    {
        // "Hello\n" = 0-5 text; separator at 5
        // "World"   = 6-10
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("Hello"), "Hello", "\n"));
        layer.Register(new IndexEntry(MakeBlock("World"), "World", "\n"));

        // select exactly "Hello" (0..5) — focus is at AbsEnd of first entry, before separator
        layer.SetSelectionForTest(0, 5);

        Assert.Equal("Hello", layer.GetSelectedText());
    }

    // ── Table cell selection (tab separator) ─────────────────────────────────

    [AvaloniaFact]
    public void GetSelectedText_table_row_uses_tab_separator()
    {
        // Cells in one row: "Alice"\t"30"\n
        // Offsets: Alice=0..4(text), 5='\t'; 30=6..7(text), 8='\n'
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("Alice"), "Alice", "\t"));
        layer.Register(new IndexEntry(MakeBlock("30"),    "30",    "\n"));

        layer.SelectAll(); // selects 0..7 (AbsEnd of last entry = 7)

        Assert.Equal("Alice\t30", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void GetSelectedText_cross_table_rows()
    {
        // Row 1: "Alice"\t"30"\n  → offsets 0..4, 6..7, newline at 8
        // Row 2: "Bob"\t"25"\n    → offsets 9..11, 13..14
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("Alice"), "Alice", "\t"));
        layer.Register(new IndexEntry(MakeBlock("30"),    "30",    "\n"));
        layer.Register(new IndexEntry(MakeBlock("Bob"),   "Bob",   "\t"));
        layer.Register(new IndexEntry(MakeBlock("25"),    "25",    "\n"));

        layer.SelectAll();

        Assert.Equal("Alice\t30\nBob\t25", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void GetSelectedText_partial_table_second_column()
    {
        // Row 1: Alice(0-4)\t(5) 30(6-7)\n(8)
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("Alice"), "Alice", "\t"));
        layer.Register(new IndexEntry(MakeBlock("30"),    "30",    "\n"));

        // Select only "30" (offsets 6..8, which is AbsStart=6, AbsEnd=8)
        layer.SetSelectionForTest(6, 8);

        Assert.Equal("30", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void GetSelectedText_empty_middle_cell_preserves_tab()
    {
        // Table row: "A" | "" | "C" with tab separators
        // AbsStart: A=0, empty=2, C=3
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("A"),  "A",  "\t")); // AbsStart=0, AbsEnd=1, AbsEndWithSep=2
        layer.Register(new IndexEntry(MakeBlock(""),   "",   "\t")); // AbsStart=2, AbsEnd=2, AbsEndWithSep=3
        layer.Register(new IndexEntry(MakeBlock("C"),  "C",  "\n")); // AbsStart=3, AbsEnd=4, AbsEndWithSep=5

        layer.SelectAll(); // selects 0..4 (AbsEnd of last entry)

        Assert.Equal("A\t\tC", layer.GetSelectedText());
    }

    // ── Reversed selection (focus before anchor) ──────────────────────────────

    [AvaloniaFact]
    public void GetSelectedText_reversed_selection_same_as_forward()
    {
        var layer = new DocumentSelectionLayer();
        layer.Register(new IndexEntry(MakeBlock("Hello"), "Hello", "\n"));
        layer.Register(new IndexEntry(MakeBlock("World"), "World", "\n"));

        // anchor > focus — should produce same result as 3..9 forward
        layer.SetSelectionForTest(9, 3);

        Assert.Equal("lo\nWor", layer.GetSelectedText());
    }

    // ── AbsStart stamping ─────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Register_stamps_AbsStart_on_entries()
    {
        var layer = new DocumentSelectionLayer();
        var e1 = new IndexEntry(MakeBlock("Hi"),    "Hi",    "\n"); // 0..1, sep='\n' → next=3
        var e2 = new IndexEntry(MakeBlock("There"), "There", "\n"); // 3..7

        layer.Register(e1);
        layer.Register(e2);

        Assert.Equal(0, e1.AbsStart);
        Assert.Equal(3, e2.AbsStart); // "Hi"(2) + "\n"(1) = 3
    }

    private static TextBlock MakeBlock(string text) => new() { Text = text };
}
