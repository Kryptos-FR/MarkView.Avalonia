// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia.Threading;

namespace MarkView.Avalonia;

/// <summary>
/// Streams markdown into a <see cref="MarkdownViewer"/>: text appended to the session, from any thread,
/// is rendered at most once per update interval, re-rendering only the blocks the new text can have
/// changed. Started with <see cref="MarkdownViewer.BeginStreaming"/>.
/// </summary>
/// <remarks>
/// <para>
/// After each update the viewer's <see cref="MarkdownViewer.Markdown"/> holds the text received so far.
/// <see cref="Complete"/> (or <see cref="Dispose"/>) applies the final update and ends the session.
/// Between updates the viewer closes the inline syntax the text leaves open, unless
/// <see cref="MarkdownViewer.BeginStreaming"/> was called with <c>healIncompleteSyntax: false</c>.
/// </para>
/// <para>
/// Setting <see cref="MarkdownViewer.Markdown"/>, <see cref="MarkdownViewer.Source"/>,
/// <see cref="MarkdownViewer.Pipeline"/> or any other property that makes the viewer render again ends
/// the session silently: no final update is applied, <see cref="IsCompleted"/> becomes <c>true</c> and
/// later appends throw.
/// </para>
/// </remarks>
public sealed class MarkdownStreamingSession : IDisposable
{
    private readonly Lock _gate = new();
    private readonly MarkdownViewer _viewer;
    private readonly TimeSpan _updateInterval;
    private readonly StringBuilder _text;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Text was appended since the last update.
    private bool _dirty;
    // No more text is accepted: Complete, Dispose or Abandon was called.
    private bool _completed;
    // The final update was applied, or the session was abandoned; nothing more will be rendered.
    private bool _finished;
    // A timer start was requested (or the timer runs) and no idle tick has stopped it since.
    private bool _timerRequested;
    // Created, started and stopped on the UI thread only.
    private DispatcherTimer? _timer;

    /// <summary>
    /// Creates a session that appends to <paramref name="initialText"/> and renders into
    /// <paramref name="viewer"/> at most once per <paramref name="updateInterval"/>.
    /// </summary>
    internal MarkdownStreamingSession(MarkdownViewer viewer, string initialText, TimeSpan updateInterval)
    {
        _viewer = viewer;
        _updateInterval = updateInterval;
        _text = new StringBuilder(initialText);
    }

    /// <summary>
    /// Gets whether the session accepts no more text: <see cref="Complete"/> or <see cref="Dispose"/> was
    /// called, an update failed, or the viewer rendered something else.
    /// </summary>
    public bool IsCompleted
    {
        get
        {
            lock (_gate)
                return _completed;
        }
    }

    /// <summary>
    /// Raised on the UI thread after each update is applied, the final one included. An update applies
    /// every append received since the previous one.
    /// </summary>
    public event EventHandler? Updated;

    /// <summary>
    /// Appends <paramref name="text"/> to the document. Can be called from any thread; the text is
    /// rendered by the next update. Appending an empty string does nothing.
    /// </summary>
    /// <param name="text">The markdown to append.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The session is completed.</exception>
    public void Append(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Append(text.AsSpan());
    }

    /// <summary>
    /// Appends <paramref name="text"/> to the document. Can be called from any thread; the text is
    /// rendered by the next update. Appending an empty span does nothing.
    /// </summary>
    /// <param name="text">The markdown to append.</param>
    /// <exception cref="InvalidOperationException">The session is completed.</exception>
    public void Append(ReadOnlySpan<char> text)
    {
        lock (_gate)
        {
            if (_completed)
                throw new InvalidOperationException("The streaming session is completed.");
            if (text.IsEmpty)
                return;
            _text.Append(text);
            _dirty = true;
            if (_timerRequested)
                return;
            _timerRequested = true;
        }

        if (Dispatcher.UIThread.CheckAccess())
            StartTimer();
        else
            Dispatcher.UIThread.Post(StartTimer, DispatcherPriority.Render);
    }

    /// <summary>
    /// Ends the session: applies a final update with every append received, then releases the session's
    /// rendering state. Can be called from any thread; called on the UI thread, the final update is
    /// applied before this method returns, otherwise it is posted to the UI thread. Calling it again does
    /// nothing.
    /// </summary>
    public void Complete()
    {
        lock (_gate)
        {
            if (_completed)
                return;
            _completed = true;
        }

        if (Dispatcher.UIThread.CheckAccess())
            ApplyPendingUpdate();
        else
            Dispatcher.UIThread.Post(ApplyPendingUpdate, DispatcherPriority.Render);
    }

    /// <summary>
    /// Ends the session as <see cref="Complete"/> does, and returns a task that completes once the final
    /// update is applied (at once when the session already ended). Never throws: when an update throws,
    /// on the UI thread or not, the returned task is faulted with that exception instead.
    /// </summary>
    public Task CompleteAsync()
    {
        try
        {
            Complete();
        }
        // A failed update has already faulted the task; the exception is reported through it.
        catch (Exception) when (_completion.Task.IsFaulted)
        {
        }
        return _completion.Task;
    }

    /// <summary>Ends the session, as <see cref="Complete"/> does.</summary>
    public void Dispose() => Complete();

    /// <summary>
    /// Applies the text appended since the last update, or the final update once the session is
    /// completed. The timer's tick, on the UI thread. A tick with nothing new stops the timer until the
    /// next append. An exception thrown by the update ends the session (faulting
    /// <see cref="CompleteAsync"/>'s task) and propagates.
    /// </summary>
    internal void ApplyPendingUpdate()
    {
        string text;
        bool final;
        lock (_gate)
        {
            if (_finished)
                return;
            if (!_dirty && !_completed)
            {
                _timer?.Stop();
                _timerRequested = false;
                return;
            }
            text = _text.ToString();
            _dirty = false;
            final = _completed;
        }

        try
        {
            _viewer.ApplyStreamingUpdate(text, final);
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _completion.TrySetException(ex);
            // Finished here rather than through the viewer: a final update has already released the
            // viewer's session (an Updated handler threw), and the viewer may have begun another since.
            Finish();
            _viewer.EndStreamingSession(this);
            throw;
        }

        if (final)
        {
            Finish();
            _completion.TrySetResult();
        }
    }

    /// <summary>
    /// Ends the session without a final update, because the viewer rendered something else. UI thread.
    /// </summary>
    internal void Abandon()
    {
        Finish();
        _completion.TrySetResult();
    }

    /// <summary>
    /// Replaces <paramref name="length"/> characters at <paramref name="start"/> of the accumulated text
    /// with <paramref name="replacement"/>, for a task toggled in the rendered document, so the next
    /// update does not revert it. UI thread. What is rendered already shows the toggle, so this asks for
    /// no update.
    /// </summary>
    internal void PatchText(int start, int length, string replacement)
    {
        lock (_gate)
            _text.Remove(start, length).Insert(start, replacement);
    }

    /// <summary>Whether the update timer is running, for tests.</summary>
    internal bool IsTimerRunning => _timer?.IsEnabled == true;

    /// <summary>Starts the update timer, creating it on first use. UI thread.</summary>
    private void StartTimer()
    {
        if (_finished)
            return;
        if (_timer is null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = _updateInterval };
            _timer.Tick += (_, _) => ApplyPendingUpdate();
        }
        _timer.Start();
    }

    /// <summary>Stops and drops the timer and marks the session finished. UI thread.</summary>
    private void Finish()
    {
        _timer?.Stop();
        _timer = null;
        lock (_gate)
            _completed = _finished = true;
    }
}
