// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Xunit;

namespace MarkView.Avalonia.Tests;

public class TocEntryTests
{
    [Fact]
    public void BuildTree_nests_headings_by_level()
    {
        var flat = new List<(int Level, string Text, string Slug)>
        {
            (1, "H1", "h1"),
            (2, "H2a", "h2a"),
            (2, "H2b", "h2b"),
            (1, "H1b", "h1b"),
        };

        var tree = TocEntry.BuildTree(flat, maxDepth: 6);

        Assert.Equal(2, tree.Count);
        Assert.Equal(2, tree[0].Children.Count);
        Assert.Equal("h2a", tree[0].Children[0].Slug);
        Assert.Equal("h2b", tree[0].Children[1].Slug);
        Assert.Empty(tree[1].Children);
    }

    [Fact]
    public void BuildTree_nests_skipped_level_under_nearest_shallower_ancestor()
    {
        var flat = new List<(int Level, string Text, string Slug)>
        {
            (1, "H1", "h1"),
            (3, "H3", "h3"), // no H2 in between
        };

        var tree = TocEntry.BuildTree(flat, maxDepth: 6);

        Assert.Single(tree);
        var h3 = Assert.Single(tree[0].Children);
        Assert.Equal("h3", h3.Slug);
    }

    [Fact]
    public void BuildTree_root_level_heading_with_no_shallower_ancestor()
    {
        var flat = new List<(int Level, string Text, string Slug)> { (3, "H3", "h3") };

        var tree = TocEntry.BuildTree(flat, maxDepth: 6);

        Assert.Single(tree);
        Assert.Equal("h3", tree[0].Slug);
    }

    [Fact]
    public void BuildTree_drops_headings_deeper_than_maxDepth()
    {
        var flat = new List<(int Level, string Text, string Slug)>
        {
            (1, "H1", "h1"),
            (2, "H2", "h2"),
            (3, "H3", "h3"),
        };

        var tree = TocEntry.BuildTree(flat, maxDepth: 2);

        Assert.Single(tree);
        var h2 = Assert.Single(tree[0].Children);
        Assert.Empty(h2.Children);
    }

    [Fact]
    public void BuildTree_empty_input_returns_empty_tree()
    {
        var tree = TocEntry.BuildTree([], maxDepth: 6);
        Assert.Empty(tree);
    }
}
