// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace MarkView.Avalonia.Mermaid;

/// <summary>
/// The diagrams rendered by every document, so that rendering the same diagram again (a document re-rendered
/// after an option change, a streamed answer whose last blocks are rebuilt on each update, a viewer recreated)
/// neither lays it out nor parses its SVG again. Entries are keyed by everything baked into the drawing: the
/// source, the theme colours, the style and the document font.
/// </summary>
/// <remarks>
/// Holds at most <see cref="Capacity"/> diagrams and evicts the least recently used one first. A drawing is
/// never disposed, since it may still be shown: it is collected once nothing shows it.
/// Failed renders are never cached. Concurrent requests for the same diagram share a single render, which is
/// cancelled only once every one of them has been cancelled, if it has not started yet; a render that runs to
/// its end is cached even when nobody waits for it any more, since the diagram usually comes back at once.
/// Thread-safe.
/// </remarks>
internal sealed class MermaidDiagramCache(int capacity)
{
    /// <summary>The capacity of <see cref="Shared"/>: 64 diagrams.</summary>
    public const int DefaultCapacity = 64;

    /// <summary>The cache every render uses.</summary>
    public static MermaidDiagramCache Shared { get; } = new(DefaultCapacity);

    private readonly Lock _gate = new();
    // Most recently used first.
    private readonly LinkedList<(Key Key, RenderedDiagram Diagram)> _recency = new();
    private readonly Dictionary<Key, LinkedListNode<(Key Key, RenderedDiagram Diagram)>> _entries = [];
    private readonly Dictionary<Key, SharedRender> _renders = [];

    /// <summary>The number of cached diagrams.</summary>
    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    /// <summary>Removes every cached diagram. Renders in flight are not affected, but their results are cached.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _recency.Clear();
        }
    }

    /// <summary>
    /// Returns the diagram cached for <paramref name="key"/>, marking it as the most recently used, or
    /// runs <paramref name="render"/> on a background thread (where there is one) for it. An exception of
    /// <paramref name="render"/> propagates.
    /// </summary>
    public async Task<RenderedDiagram> GetOrRenderAsync(Key key, Func<RenderedDiagram> render, CancellationToken cancellationToken)
    {
        SharedRender? shared;
        bool start;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _recency.Remove(node);
                _recency.AddFirst(node);
                return node.Value.Diagram;
            }
            start = !_renders.TryGetValue(key, out shared);
            if (start)
                _renders[key] = shared = new SharedRender();
            shared!.Waiters++;
        }

        if (start)
            _ = RunAsync(key, shared, render);

        try
        {
            return await shared.Completion.Task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            bool cancelRender;
            lock (_gate)
            {
                cancelRender = --shared.Waiters == 0;
                if (cancelRender)
                    _renders.Remove(key);
            }
            if (cancelRender)
                shared.Cancellation.Cancel();
            throw;
        }
    }

    private async Task RunAsync(Key key, SharedRender shared, Func<RenderedDiagram> render)
    {
        RenderedDiagram diagram;
        try
        {
            diagram = await Task.Run(render, shared.Cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (shared.Cancellation.IsCancellationRequested)
        {
            // Every request gave up on it before it started, and it was already removed.
            shared.Completion.SetCanceled();
            return;
        }
        catch (Exception e)
        {
            lock (_gate)
            {
                if (_renders.GetValueOrDefault(key) == shared)
                    _renders.Remove(key);
            }
            shared.Completion.SetException(e);
            return;
        }

        lock (_gate)
        {
            if (_renders.GetValueOrDefault(key) == shared)
                _renders.Remove(key);
            // A render every request gave up on may have been started again since, and finished first.
            if (capacity > 0 && !_entries.ContainsKey(key))
            {
                _entries[key] = _recency.AddFirst((key, diagram));
                while (_entries.Count > capacity)
                {
                    _entries.Remove(_recency.Last!.Value.Key);
                    _recency.RemoveLast();
                }
            }
        }
        shared.Completion.SetResult(diagram);
    }

    /// <summary>Everything a diagram's drawing depends on.</summary>
    internal readonly record struct Key(string Source, string? Bg, string? Fg, string? Accent, MermaidStyle Style, FontFamily Font);

    private sealed class SharedRender
    {
        public readonly TaskCompletionSource<RenderedDiagram> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly CancellationTokenSource Cancellation = new();
        // Guarded by the cache's lock.
        public int Waiters;
    }
}

/// <summary>A rendered diagram: its drawing, and the SVG document a saved copy is written from, built when first saved.</summary>
internal sealed record RenderedDiagram(SvgSource Source, Lazy<byte[]> File);
