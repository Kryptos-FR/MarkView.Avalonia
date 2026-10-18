// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class DocumentSelectionLayerTruncateTests
{
    // "alpha" is 0-5, "beta" 6-10 and "gamma" 11-16, each followed by a "\n" separator.
    private static DocumentSelectionLayer ThreeEntries()
    {
        var layer = new DocumentSelectionLayer();
        Add(layer, "alpha");
        Add(layer, "beta");
        Add(layer, "gamma");
        return layer;
    }

    private static IndexEntry Add(DocumentSelectionLayer layer, string text)
    {
        var entry = new IndexEntry(new TextBlock { Text = text }, text, "\n");
        layer.Register(entry);
        return entry;
    }

    [AvaloniaFact]
    public void TruncateTo_removes_later_entries_and_resets_the_running_offset()
    {
        var layer = ThreeEntries();

        layer.TruncateTo(2);

        Assert.Equal(2, layer.EntryCount);
        Assert.Equal(11, Add(layer, "delta").AbsStart);
    }

    [AvaloniaFact]
    public void TruncateTo_zero_restarts_at_offset_zero()
    {
        var layer = ThreeEntries();

        layer.TruncateTo(0);

        Assert.Equal(0, layer.EntryCount);
        Assert.Equal(0, Add(layer, "x").AbsStart);
    }

    [AvaloniaFact]
    public void TruncateTo_count_keeps_everything()
    {
        var layer = ThreeEntries();

        layer.TruncateTo(3);
        layer.SelectAll();

        Assert.Equal(3, layer.EntryCount);
        Assert.Equal("alpha\nbeta\ngamma", layer.GetSelectedText());
        Assert.Equal(17, Add(layer, "delta").AbsStart);
    }

    [AvaloniaFact]
    public void Selection_inside_kept_entries_is_untouched()
    {
        var layer = ThreeEntries();
        layer.SetSelectionForTest(0, 4);

        layer.TruncateTo(2);

        Assert.Equal("alph", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Selection_ending_in_a_removed_entry_is_clamped()
    {
        var layer = ThreeEntries();
        layer.SetSelectionForTest(2, 14);

        layer.TruncateTo(2);

        Assert.Equal("pha\nbeta", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Selection_starting_in_a_removed_entry_is_cleared()
    {
        var layer = ThreeEntries();
        layer.SetSelectionForTest(12, 15);

        layer.TruncateTo(2);

        Assert.Equal(string.Empty, layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Reversed_selection_is_clamped_the_same_way()
    {
        var layer = ThreeEntries();
        layer.SetSelectionForTest(14, 2);

        layer.TruncateTo(2);

        Assert.Equal("pha\nbeta", layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Selection_is_cleared_when_every_entry_is_removed()
    {
        var layer = ThreeEntries();
        layer.SetSelectionForTest(0, 4);

        layer.TruncateTo(0);
        Add(layer, "again");

        Assert.Equal(string.Empty, layer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Find_matches_in_removed_entries_are_dropped()
    {
        var layer = ThreeEntries();
        Assert.Equal(5, layer.Find("a", MarkdownFindOptions.None));
        layer.CurrentFindMatch = 4;

        layer.TruncateTo(2);

        Assert.Equal(3, layer.FindMatches.Count);
        Assert.All(layer.FindMatches, match => Assert.NotEqual("gamma", match.Entry.PlainText));
        Assert.Equal(-1, layer.CurrentFindMatch);
    }

    [AvaloniaFact]
    public void Current_find_match_in_a_kept_entry_is_kept()
    {
        var layer = ThreeEntries();
        layer.Find("a", MarkdownFindOptions.None);
        layer.CurrentFindMatch = 1;

        layer.TruncateTo(2);

        Assert.Equal(1, layer.CurrentFindMatch);
    }

    [AvaloniaFact]
    public void Find_matches_are_untouched_when_nothing_is_removed()
    {
        var layer = ThreeEntries();
        layer.Find("a", MarkdownFindOptions.None);
        layer.CurrentFindMatch = 4;

        layer.TruncateTo(3);

        Assert.Equal(5, layer.FindMatches.Count);
        Assert.Equal(4, layer.CurrentFindMatch);
    }

    [AvaloniaTheory]
    [InlineData(-1)]
    [InlineData(4)]
    public void TruncateTo_rejects_out_of_range(int entryCount)
    {
        var layer = ThreeEntries();

        Assert.Throws<ArgumentOutOfRangeException>(() => layer.TruncateTo(entryCount));
        Assert.Equal(3, layer.EntryCount);
    }
}
