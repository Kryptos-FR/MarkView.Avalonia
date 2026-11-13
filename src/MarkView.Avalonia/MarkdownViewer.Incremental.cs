// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

using MarkView.Avalonia.Automation;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>
    /// Defines the <see cref="IsIncrementalRenderingEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsIncrementalRenderingEnabledProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(IsIncrementalRenderingEnabled));

    /// <summary>
    /// Gets or sets whether a new <see cref="Markdown"/> text re-renders only the top-level blocks that
    /// changed, as next to an editor: the blocks before and after the edit keep their controls, so the
    /// scroll position, the selection in them, the active search, task toggles, folded content and an
    /// open image lightbox are kept, and the cost of an update depends on what changed rather than on
    /// the length of the document. A text with no top-level block in common with the previous one (a
    /// different document) still scrolls back to the top. When <c>false</c>, every new text is rendered
    /// from scratch and scrolls to the top. Changing a rendering property (such as
    /// <see cref="Pipeline"/>) or <see cref="Source"/> always renders from scratch. Defaults to
    /// <c>false</c>.
    /// </summary>
    public bool IsIncrementalRenderingEnabled
    {
        get => GetValue(IsIncrementalRenderingEnabledProperty);
        set => SetValue(IsIncrementalRenderingEnabledProperty, value);
    }

    private void OnIncrementalRenderingChanged()
    {
        // The next text renders from scratch. A streaming session keeps its document until it completes.
        if (!IsIncrementalRenderingEnabled && _streamingSession is null)
            _liveDocument = null;
    }

    /// <summary>
    /// Renders <paramref name="markdown"/>, a new <see cref="Markdown"/> text, into the live document
    /// rendered last, re-rendering only the top-level blocks that changed. Ends a streaming session
    /// without its final update, as a direct set does.
    /// </summary>
    private void RenderEdit(string markdown)
    {
        if (_streamingSession is { } session)
            EndStreamingSession(session, keepDocument: true);
        var live = _liveDocument!;
        _renderedMarkdown = markdown;

        var markdownText = PreprocessImageSizes(markdown, EffectivePipeline, out _sourceShifts);
        var document = Markdig.Markdown.Parse(markdownText, EffectivePipeline);
        var renderer = live.Renderer;
        renderer.IsStreaming = false;
        renderer.ProvisionalStart = int.MaxValue;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var previousBlocks = live.BlockCount;
        var result = live.Update(document, markdownText, focused, reuseAfterChange: true);

        _anchors = new Dictionary<string, Control>(renderer.Anchors, StringComparer.OrdinalIgnoreCase);
        if (result.HeadingsChanged)
        {
            _headingEntries = [.. renderer.HeadingEntries];
            RebuildTableOfContents();
        }
        if (result.FocusDiscarded)
            Focus();

        // Nothing in common with the previous text: another document, read from its top. A document of
        // one block has nothing in common with itself after any edit, so it keeps its place. The scroll
        // anchor's block went with the others, which the anchor notices on the next layout pass.
        if (result.UnchangedBlocks == 0 && previousBlocks > 1 && document.Count > 1 && _scrollViewer is not null)
            _scrollViewer.Offset = default;

        UpdateFind(CurrentFindMatchIndex);
        if (result.ReusedBlocks < document.Count
            && ControlAutomationPeer.CreatePeerForElement(this) is MarkdownViewerAutomationPeer peer)
            peer.NotifyDocumentChanged();
    }
}
