// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class DocumentSelectionLayerSpliceTests
{
    // "alpha" is 0-5, "beta" 6-10 and "gamma" 11-16, each followed by a "\n" separator.
    private static DocumentSelectionLayer ThreeEntries()
    {
        var layer = new DocumentSelectionLayer();
        foreach (var text in new[] { "alpha", "beta", "gamma" })
            layer.Register(Entry(text));
        return layer;
    }

    private static IndexEntry Entry(string text) => new(new TextBlock { Text = text }, text, "\n");

    private static string AllText(DocumentSelectionLayer layer)
    {
        layer.SelectAll();
        return layer.GetSelectedText();
    }

    [AvaloniaFact]
    public void Splice_replaces_entries_in_the_middle_and_moves_the_later_ones()
    {
        var layer = ThreeEntries();
        var gamma = layer.Entries[2];

        layer.Splice(1, 1, [Entry("b"), Entry("bb")]);

        Assert.Equal("alpha\nb\nbb\ngamma", AllText(layer));
        Assert.Same(gamma, layer.Entries[3]);
        Assert.Equal(11, gamma.AbsStart);
    }

    [AvaloniaFact]
    public void Splice_continues_registration_after_the_last_entry()
    {
        var layer = ThreeEntries();

        layer.Splice(0, 1, []);
        var entry = Entry("delta");
        layer.Register(entry);

        Assert.Equal(11, entry.AbsStart);
    }

    [AvaloniaFact]
    public void Selection_in_a_later_entry_moves_with_its_text()
    {
        var layer = ThreeEntries();
        layer.SetSelectionForTest(12, 15);   // "amm"

        layer.Splice(0, 1, [Entry("a")]);

        Assert.Equal((8, 11), layer.SelectionForTest);
        Assert.Equal("amm", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Selection_end_in_a_removed_entry_moves_to_the_end_of_the_entry_before()
    {
        var layer = ThreeEntries();
        layer.SetSelectionForTest(2, 8);   // "pha\nbe"

        layer.Splice(1, 1, [Entry("b")]);

        Assert.Equal((2, 5), layer.SelectionForTest);
    }

    [AvaloniaFact]
    public void Selection_start_in_a_removed_entry_moves_to_the_end_of_the_entry_before()
    {
        var layer = ThreeEntries();
        layer.SetSelectionForTest(8, 13);   // "ta\nga"

        layer.Splice(1, 1, [Entry("bbbbbbbb")]);

        Assert.Equal((5, 17), layer.SelectionForTest);
        Assert.Equal("\nbbbbbbbb\nga", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Selection_inside_removed_entries_is_cleared()
    {
        var layer = ThreeEntries();
        layer.SetSelectionForTest(7, 9);

        layer.Splice(1, 1, [Entry("b")]);

        Assert.Equal((null, null), layer.SelectionForTest);
    }

    [AvaloniaFact]
    public void Splice_with_nothing_to_remove_inserts()
    {
        var layer = ThreeEntries();
        layer.SetSelectionForTest(0, 3);

        layer.Splice(3, 0, [Entry("delta")]);

        Assert.Equal("alpha\nbeta\ngamma\ndelta", AllText(layer));
    }

    [AvaloniaFact]
    public void Splice_rejects_a_range_beyond_the_entries()
    {
        var layer = ThreeEntries();

        Assert.Throws<ArgumentOutOfRangeException>(() => layer.Splice(-1, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => layer.Splice(0, -1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => layer.Splice(2, 2, []));
        Assert.Equal(3, layer.EntryCount);
    }

    [AvaloniaFact]
    public void Find_matches_in_removed_entries_are_dropped_and_the_others_kept()
    {
        var layer = ThreeEntries();
        // Twice in "alpha", once in "beta", twice in "gamma".
        Assert.Equal(5, layer.Find("a", default));

        layer.Splice(1, 1, []);

        Assert.Equal(4, layer.FindMatches.Count);
        Assert.DoesNotContain(layer.FindMatches, m => m.Entry.PlainText == "beta");
    }
}
