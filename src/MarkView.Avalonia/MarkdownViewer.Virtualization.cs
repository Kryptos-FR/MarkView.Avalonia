// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Threading;

using Markdig.Syntax;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    // Blocks built at a time by the background build, and how long it builds before it lets input and rendering in.
    private const int BackgroundBuildBatch = 16;
    private static readonly TimeSpan BackgroundBuildSlice = TimeSpan.FromMilliseconds(8);

    // The blocks of the document shown that are not built yet, with IsVirtualizationEnabled.
    private DeferredBlocks? _deferred;

    /// <summary>Whether some blocks of the document shown are not built yet, for tests.</summary>
    internal int PendingBlockCount => _deferred?.Count ?? 0;

    /// <summary>
    /// Leaves the blocks of <paramref name="live"/>, just rendered with none built, to be built as they come near the
    /// visible area (the document's panel asks for them, see <see cref="VirtualizingBlockPanel.Pending"/>) and, in
    /// the meantime, in the background, a few at a time, so that the whole document is soon built.
    /// </summary>
    private void DeferBlocks(LiveDocument live)
    {
        if (live.PendingCount == 0 || live.RootPanel is not VirtualizingBlockPanel panel)
            return;
        var deferred = new DeferredBlocks(this, live, panel);
        _deferred = deferred;
        panel.Pending = deferred;
        BuildInBackground(deferred);
    }

    /// <summary>
    /// Settles the blocks not built yet before the live document is updated or replaced: those of
    /// <paramref name="kept"/>, the live document about to be updated, are built, since an update compares the
    /// whole last document with the new one; those of another document are dropped with it.
    /// </summary>
    private void SettleDeferredBlocks(LiveDocument? kept)
    {
        if (_deferred is { } deferred && deferred.Live == kept)
            BuildDeferred(deferred, int.MaxValue);
        _deferred = null;
    }

    /// <summary>
    /// Builds every block not built yet, for what works on the whole document: find, Select All, an anchor not
    /// found among the blocks built, Tab.
    /// </summary>
    private void BuildAllBlocks()
    {
        if (_deferred is { } deferred)
            BuildDeferred(deferred, int.MaxValue);
    }

    private void BuildDeferred(DeferredBlocks deferred, int count)
    {
        if (_deferred != deferred)
            return;
        var live = deferred.Live;
        live.Extend(count);
        var renderer = live.Renderer;
        _anchors = new Dictionary<string, Control>(renderer.Anchors, StringComparer.OrdinalIgnoreCase);
        if (live.PendingCount > 0)
            return;

        // The table of contents is rebuilt once, with the whole document, rather than with every batch.
        _deferred = null;
        deferred.Panel.Pending = null;
        _headingEntries = [.. renderer.HeadingEntries];
        RebuildTableOfContents();
    }

    private void BuildInBackground(DeferredBlocks deferred) => Dispatcher.UIThread.Post(() =>
    {
        var start = Stopwatch.GetTimestamp();
        while (_deferred == deferred && Stopwatch.GetElapsedTime(start) < BackgroundBuildSlice)
            BuildDeferred(deferred, BackgroundBuildBatch);
        if (_deferred == deferred)
            BuildInBackground(deferred);
    }, DispatcherPriority.Background);

    /// <summary>The blocks of a live document not built yet, which its panel has built as they come near.</summary>
    private sealed class DeferredBlocks(MarkdownViewer viewer, LiveDocument live, VirtualizingBlockPanel panel)
        : VirtualizingBlockPanel.IPendingBlocks
    {
        public LiveDocument Live => live;

        public VirtualizingBlockPanel Panel => panel;

        public int Count => live.PendingCount;

        public Block this[int index] => live.Document![live.BlockCount + index];

        public void Build(int count) => viewer.BuildDeferred(this, count);
    }
}
