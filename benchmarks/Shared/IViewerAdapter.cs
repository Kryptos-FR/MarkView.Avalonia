// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia;

namespace MarkView.Benchmarks.Shared;

/// <summary>
/// What a runner must say about its library: how to create a viewer for one configuration, how to give it a
/// document, and when the document is fully shown. Everything else (the window, the corpus, the timing and the
/// memory probe) is shared, so that the only code that differs between libraries is this adapter.
/// </summary>
public interface IViewerAdapter
{
    /// <summary>Library name and version, as printed in results.</summary>
    string Library { get; }

    /// <summary>The configurations this runner measures, e.g. "Plain", "GitHub", "GitHub+Mermaid".</summary>
    IReadOnlyList<string> Configurations { get; }

    /// <summary>Adds the library's own styles to the application (called once, at startup).</summary>
    void AddStyles(Styles styles);

    /// <summary>A new, empty viewer for <paramref name="configuration"/>.</summary>
    Control CreateViewer(string configuration);

    /// <summary>Replaces the viewer's document, the way an application normally does it.</summary>
    void SetMarkdown(Control viewer, string markdown);

    /// <summary>
    /// Whether the last document set is fully shown. A library that parses or renders on another thread must
    /// return false until that work has reached the visual tree; the harness keeps rendering frames until then.
    /// </summary>
    bool IsSettled(Control viewer) => true;

    /// <summary>
    /// Starts streaming a document into <paramref name="viewer"/> through the library's streaming API. A library
    /// without one gets the text so far set as its whole document on each append, which is what an application
    /// does with it.
    /// </summary>
    IStreamWriter BeginStreaming(Control viewer) => new ReplacingStreamWriter(this, viewer);
}

/// <summary>Text appended to a document being shown.</summary>
public interface IStreamWriter
{
    void Append(string chunk);

    /// <summary>Whether the last append is shown.</summary>
    bool IsSettled { get; }

    void Complete();
}

/// <summary>Streaming without a streaming API: each append sets the whole text so far.</summary>
public sealed class ReplacingStreamWriter(IViewerAdapter adapter, Control viewer) : IStreamWriter
{
    private readonly System.Text.StringBuilder _text = new();

    public void Append(string chunk)
    {
        _text.Append(chunk);
        adapter.SetMarkdown(viewer, _text.ToString());
    }

    public bool IsSettled => adapter.IsSettled(viewer);

    public void Complete()
    {
    }
}
