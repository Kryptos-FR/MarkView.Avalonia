// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Threading;

using MarkView.Avalonia.Automation;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>
    /// The update interval of a session started without one: at most one update every 32 milliseconds.
    /// </summary>
    public static readonly TimeSpan DefaultStreamingUpdateInterval = TimeSpan.FromMilliseconds(32);

    /// <summary>
    /// Defines the <see cref="IsStreaming"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, bool> IsStreamingProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, bool>(nameof(IsStreaming), o => o.IsStreaming);

    /// <summary>
    /// Defines the <see cref="AutoScrollToEnd"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> AutoScrollToEndProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(AutoScrollToEnd));

    // The active session and the document it renders into; the document is null until its first update.
    private MarkdownStreamingSession? _streamingSession;
    private LiveDocument? _liveDocument;
    // Whether auto-scroll follows the end: true when a session begins, false while the user has
    // scrolled away from the bottom.
    private bool _followEnd;
    private bool _scrollToEndPending;
    // The offset the viewer's own last ScrollToEnd moved to, until the scroll viewer reports it.
    private double? _ownScrollTargetY;

    /// <summary>
    /// Gets whether a streaming session is active, from <see cref="BeginStreaming"/> until the session
    /// completes or the viewer renders something else.
    /// </summary>
    public bool IsStreaming
    {
        get;
        private set => SetAndRaise(IsStreamingProperty, ref field, value);
    }

    /// <summary>
    /// Gets or sets whether, during a streaming session, the viewer keeps the end of the document in
    /// view as text arrives. Scrolling away from the bottom pauses following; scrolling back to the
    /// bottom resumes it, and each new session starts following. No effect outside a session.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool AutoScrollToEnd
    {
        get => GetValue(AutoScrollToEndProperty);
        set => SetValue(AutoScrollToEndProperty, value);
    }

    /// <summary>
    /// Starts a streaming session that appends to the current <see cref="Markdown"/>; set it to an
    /// empty string (or <c>null</c>) first to stream a new document. Text appended to the session is
    /// rendered at most once per <paramref name="updateInterval"/>, re-rendering only the blocks the new
    /// text can have changed: the scroll position, the selection in unchanged blocks, the active search
    /// and an open image lightbox are kept. After each update <see cref="Markdown"/> holds the text so
    /// far; a <c>null</c> <see cref="Markdown"/> is treated as empty, so it is <c>""</c> once a session
    /// that appended nothing completes. Must be called on the UI thread.
    /// </summary>
    /// <param name="updateInterval">
    /// The shortest time between two updates; <see cref="DefaultStreamingUpdateInterval"/> when
    /// <c>null</c>.
    /// </param>
    /// <returns>The session to append text to and complete.</returns>
    /// <exception cref="InvalidOperationException">
    /// A session is already active, or <see cref="Source"/> is set.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="updateInterval"/> is not positive.</exception>
    public MarkdownStreamingSession BeginStreaming(TimeSpan? updateInterval = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_streamingSession is not null)
            throw new InvalidOperationException("A streaming session is already active.");
        if (Source is not null)
            throw new InvalidOperationException("Streaming appends to Markdown, not to Source.");
        var interval = updateInterval ?? DefaultStreamingUpdateInterval;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero, nameof(updateInterval));

        _followEnd = true;
        _streamingSession = new MarkdownStreamingSession(this, Markdown ?? "", interval);
        IsStreaming = true;
        return _streamingSession;
    }

    /// <summary>
    /// Renders <paramref name="text"/>, the session's whole text so far, into the session's document,
    /// installing that document on the first update. A <paramref name="final"/> update renders nothing
    /// as provisional and ends the session.
    /// </summary>
    internal void ApplyStreamingUpdate(string text, bool final)
    {
        var markdownText = PreprocessImageSizes(text, out _sourceShifts);
        var document = Markdig.Markdown.Parse(markdownText, EffectivePipeline);

        var install = _liveDocument is null;
        var live = _liveDocument ??= new LiveDocument(CreateRenderer(), ActivateLink);
        var renderer = live.Renderer;
        // A focused link may be about to be replaced; keep focus in the viewer rather than losing it.
        var focusWasInDocument = IsKeyboardFocusWithin && !IsFocused;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

        renderer.IsStreaming = !final;
        var result = live.Update(document, focused);

        _anchors = new Dictionary<string, Control>(renderer.Anchors, StringComparer.OrdinalIgnoreCase);
        if (install || result.HeadingsChanged)
        {
            _headingEntries = [.. renderer.HeadingEntries];
            RebuildTableOfContents();
        }

        if (install)
        {
            _selectionLayer = live.SelectionLayer;
            _linkFocusLayer = live.LinkLayer;
            InstallDocument(renderer, live.SelectionLayer, live.LinkLayer, focusWasInDocument);
        }
        else if (result.FocusDiscarded)
            Focus();

        // Marked as rendered first, so that OnMarkdownChanged finds them equal and does not render again.
        _renderedMarkdown = text;
        SetCurrentValue(MarkdownProperty, text);

        // Unlike a direct set, an update never resets the scroll position.
        UpdateFind(CurrentFindMatchIndex);
        if (AutoScrollToEnd && _followEnd && _scrollViewer is not null)
            RequestScrollToEnd();

        if (final)
        {
            _liveDocument = null;
            _streamingSession = null;
            IsStreaming = false;
            // Assistive technology re-reads the finished document.
            if (ControlAutomationPeer.CreatePeerForElement(this) is MarkdownViewerAutomationPeer peer)
                peer.NotifyDocumentChanged();
        }
    }

    /// <summary>
    /// Ends the active session, if any, without a final update: the viewer is about to render something
    /// else. Called first by <see cref="RenderMarkdown"/> and when <see cref="Source"/> changes.
    /// </summary>
    internal void EndStreamingSession()
    {
        if (_streamingSession is { } session)
            EndStreamingSession(session);
    }

    /// <summary>
    /// Ends <paramref name="session"/> without a final update if it is still the viewer's active session;
    /// does nothing otherwise (it already ended, and another session may have begun since).
    /// </summary>
    internal void EndStreamingSession(MarkdownStreamingSession session)
    {
        if (!ReferenceEquals(_streamingSession, session))
            return;
        session.Abandon();
        _streamingSession = null;
        _liveDocument = null;
        IsStreaming = false;
    }

    /// <summary>Scrolls to the end once the next layout pass has measured the new content.</summary>
    private void RequestScrollToEnd()
    {
        if (_scrollToEndPending)
            return;
        _scrollToEndPending = true;
        LayoutUpdated += OnLayoutUpdatedScrollToEnd;
    }

    private void OnLayoutUpdatedScrollToEnd(object? sender, EventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdatedScrollToEnd;
        _scrollToEndPending = false;
        if (_followEnd && _scrollViewer is { } scrollViewer)
        {
            _ownScrollTargetY = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
            scrollViewer.ScrollToEnd();
        }
    }

    /// <summary>
    /// Pauses following the end when the user scrolls away from the bottom and resumes it when they
    /// scroll back to it; an extent change alone moves no offset and is ignored. The scroll viewer
    /// reports a scroll one layout pass late, against the extent of that pass: when an update has grown
    /// the document in between, the viewer's own <see cref="ScrollViewer.ScrollToEnd"/> reads above the
    /// new bottom, so it is recognised by the offset it moved to and keeps following.
    /// </summary>
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.OffsetDelta.Y == 0 || sender is not ScrollViewer scrollViewer)
            return;
        // Stryker disable once Equality : the reported offset is the requested one; the half pixel only absorbs rounding
        var ownScroll = _ownScrollTargetY is { } target && Math.Abs(scrollViewer.Offset.Y - target) <= 0.5;
        _ownScrollTargetY = null;
        _followEnd = ownScroll
            || scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 1;
    }
}
