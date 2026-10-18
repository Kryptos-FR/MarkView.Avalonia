// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using Markdig;
using Markdig.Syntax;

using MarkView.Avalonia.Extensions;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Streaming;

/// <summary>
/// The <see cref="MarkdownStreamingSession"/> state machine: batching, threading, completion, and how a
/// session ends when the viewer renders something else.
/// </summary>
public class MarkdownStreamingSessionTests
{
    private static MarkdownViewer Viewer() => new() { Template = ScrollingViewerTemplate.Create() };

    private static StackPanel Root(MarkdownViewer viewer) =>
        Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]);

    private static List<bool> CountUpdates(MarkdownStreamingSession session)
    {
        var updates = new List<bool>();
        session.Updated += (_, _) => updates.Add(Dispatcher.UIThread.CheckAccess());
        return updates;
    }

    /// <summary>Records every value an observable pushes.</summary>
    private sealed class Recorder<T> : IObserver<T>
    {
        public List<T> Values { get; } = [];
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => Values.Add(value);
    }

    /// <summary>A minimal hot observable for binding to a property.</summary>
    private sealed class TestSubject<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _observers = [];

        public void OnNext(T value)
        {
            foreach (var observer in _observers.ToArray())
                observer.OnNext(value);
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new Unsubscriber(() => _observers.Remove(observer));
        }

        private sealed class Unsubscriber(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }

    private sealed class ThrowingParagraphRenderer : AvaloniaObjectRenderer<ParagraphBlock>
    {
        protected override void Write(AvaloniaRenderer renderer, ParagraphBlock obj) =>
            throw new InvalidOperationException("paragraph renderer failed");
    }

    private sealed class ThrowingParagraphExtension : IMarkViewExtension
    {
        public void Register(AvaloniaRenderer renderer) =>
            renderer.ReplaceOrAdd<ParagraphRenderer>(new ThrowingParagraphRenderer());
    }

    // ── Beginning ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void BeginStreaming_sets_IsStreaming_and_continues_from_Markdown()
    {
        var viewer = Viewer();
        viewer.Markdown = "# T";
        var recorder = new Recorder<bool>();
        using var subscription = viewer.GetObservable(MarkdownViewer.IsStreamingProperty).Subscribe(recorder);

        var session = viewer.BeginStreaming();
        session.Append("\n\nbody");
        session.ApplyPendingUpdate();

        Assert.Equal("# T\n\nbody", viewer.Markdown);
        Assert.True(viewer.IsStreaming);
        Assert.False(session.IsCompleted);
        Assert.Equal(2, Root(viewer).Children.Count);
        Assert.Equal([false, true], recorder.Values);
    }

    [AvaloniaFact]
    public void Second_BeginStreaming_throws()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();

        var ex = Assert.Throws<InvalidOperationException>(() => viewer.BeginStreaming());

        Assert.Equal("a streaming session is already active", ex.Message);
        session.Append("still works");
        session.ApplyPendingUpdate();
        Assert.Equal("still works", viewer.Markdown);
        Assert.True(viewer.IsStreaming);
    }

    [AvaloniaFact]
    public void BeginStreaming_with_Source_throws()
    {
        var viewer = Viewer();
        viewer.Source = new Uri("avares://MarkView.Avalonia.Tests/TestAssets/test.md");

        var ex = Assert.Throws<InvalidOperationException>(() => viewer.BeginStreaming());

        Assert.Equal("streaming appends to Markdown, not to Source", ex.Message);
        Assert.False(viewer.IsStreaming);
    }

    [AvaloniaFact]
    public void Non_positive_interval_throws()
    {
        var viewer = Viewer();

        var zero = Assert.Throws<ArgumentOutOfRangeException>(() => viewer.BeginStreaming(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => viewer.BeginStreaming(TimeSpan.FromMilliseconds(-1)));

        Assert.Equal("updateInterval", zero.ParamName);
        Assert.False(viewer.IsStreaming);
        // Neither attempt left a session behind.
        viewer.BeginStreaming(TimeSpan.FromTicks(1));
        Assert.True(viewer.IsStreaming);
    }

    [AvaloniaFact]
    public void Default_interval_is_32_ms()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(32), MarkdownViewer.DefaultStreamingUpdateInterval);
    }

    // ── Appending ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Several_appends_within_one_interval_produce_one_update()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();
        var updates = CountUpdates(session);

        session.Append("a");
        session.Append("b");
        session.Append("c");
        session.ApplyPendingUpdate();

        Assert.Single(updates);
        Assert.Equal("abc", viewer.Markdown);

        session.ApplyPendingUpdate();

        Assert.Single(updates);
    }

    [AvaloniaFact]
    public async Task Append_from_a_background_thread_lands_on_the_UI_thread()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();

        await Task.Run(() => session.Append("x"));
        await AsyncTestHelpers.WaitUntilAsync(() => viewer.Markdown == "x");

        Assert.Equal("x", viewer.Markdown);
        Assert.Single(Root(viewer).Children);
    }

    [AvaloniaFact]
    public async Task Updated_is_raised_on_the_UI_thread()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();
        var updates = CountUpdates(session);

        await Task.Run(() => session.Append("x"));
        await AsyncTestHelpers.WaitUntilAsync(() => updates.Count == 1);

        Assert.Equal([true], updates);
    }

    [AvaloniaFact]
    public void Empty_append_is_a_no_op()
    {
        var viewer = Viewer();
        viewer.Markdown = "kept";
        var session = viewer.BeginStreaming();
        var updates = CountUpdates(session);

        session.Append("");
        session.Append(ReadOnlySpan<char>.Empty);
        session.ApplyPendingUpdate();

        Assert.Empty(updates);
        Assert.Equal("kept", viewer.Markdown);
    }

    [AvaloniaFact]
    public void Append_null_throws()
    {
        var session = Viewer().BeginStreaming();

        var ex = Assert.Throws<ArgumentNullException>(() => session.Append((string)null!));

        Assert.Equal("text", ex.ParamName);
    }

    [AvaloniaFact]
    public void Append_after_Complete_throws()
    {
        var session = Viewer().BeginStreaming();
        session.Complete();

        Assert.Throws<InvalidOperationException>(() => session.Append("x"));
        Assert.Throws<InvalidOperationException>(() => session.Append("x".AsSpan()));
    }

    [AvaloniaFact]
    public void Append_span_appends_the_characters()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();

        session.Append("abcdef".AsSpan(1, 3));
        session.ApplyPendingUpdate();

        Assert.Equal("bcd", viewer.Markdown);
    }

    // ── Completing ────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Complete_applies_a_final_update_and_releases()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();
        var updates = CountUpdates(session);
        session.Append("a");

        session.Complete();

        Assert.Equal("a", viewer.Markdown);
        Assert.True(session.IsCompleted);
        Assert.False(viewer.IsStreaming);
        Assert.Single(updates);
        Assert.True(session.CompleteAsync().IsCompletedSuccessfully);
        // The viewer is free for a new session.
        Assert.NotSame(session, viewer.BeginStreaming());
    }

    [AvaloniaFact]
    public void Complete_is_idempotent()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();
        var updates = CountUpdates(session);
        session.Append("a");

        session.Complete();
        session.Complete();
        session.Dispose();
        session.ApplyPendingUpdate();

        Assert.Single(updates);
        Assert.True(session.IsCompleted);
        Assert.Equal("a", viewer.Markdown);
    }

    [AvaloniaFact]
    public void Dispose_completes_the_session()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();
        session.Append("a");

        session.Dispose();

        Assert.True(session.IsCompleted);
        Assert.False(viewer.IsStreaming);
        Assert.Equal("a", viewer.Markdown);
    }

    [AvaloniaFact]
    public async Task CompleteAsync_awaits_the_final_update_from_a_background_thread()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();
        session.Append("a");

        var completion = Task.Run(() => session.CompleteAsync());
        await AsyncTestHelpers.WaitUntilAsync(() => completion.IsCompleted);

        Assert.True(completion.IsCompletedSuccessfully);
        Assert.Equal("a", viewer.Markdown);
        Assert.True(session.IsCompleted);
        Assert.False(viewer.IsStreaming);
    }

    [AvaloniaFact]
    public void Complete_without_append_on_empty_viewer()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();

        session.Complete();

        Assert.Equal("", viewer.Markdown);
        Assert.False(viewer.IsStreaming);
        Assert.True(session.IsCompleted);
        Assert.Empty(Root(viewer).Children);
    }

    // ── Ending from the viewer ────────────────────────────────────────────────

    [AvaloniaFact]
    public void Direct_Markdown_set_completes_the_session_silently()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();
        var updates = CountUpdates(session);
        session.Append("a");

        viewer.Markdown = "zzz";

        Assert.True(session.IsCompleted);
        Assert.False(viewer.IsStreaming);
        Assert.Equal("zzz", viewer.Markdown);
        Assert.Throws<InvalidOperationException>(() => session.Append("x"));
        Assert.True(session.CompleteAsync().IsCompletedSuccessfully);
        session.ApplyPendingUpdate();
        Assert.Empty(updates);
        Assert.Equal("zzz", viewer.Markdown);
    }

    [AvaloniaFact]
    public void Setting_Pipeline_completes_the_session_silently()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();
        var updates = CountUpdates(session);
        session.Append("a");
        session.ApplyPendingUpdate();
        session.Append("b");

        viewer.Pipeline = new MarkdownPipelineBuilder().Build();

        Assert.True(session.IsCompleted);
        Assert.False(viewer.IsStreaming);
        Assert.Equal("a", viewer.Markdown);
        Assert.Throws<InvalidOperationException>(() => session.Append("x"));
        Assert.True(session.CompleteAsync().IsCompletedSuccessfully);
        Assert.Single(updates);
    }

    [AvaloniaFact]
    public void Markdown_set_to_the_same_text_does_not_complete_the_session()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();
        session.Append("a");
        session.ApplyPendingUpdate();
        var content = viewer.Content;

        viewer.Markdown = viewer.Markdown;

        Assert.False(session.IsCompleted);
        Assert.True(viewer.IsStreaming);
        Assert.Same(content, viewer.Content);
    }

    [AvaloniaFact]
    public void Binding_on_Markdown_survives_a_session()
    {
        var viewer = Viewer();
        var subject = new TestSubject<string?>();
        using var binding = viewer.Bind(MarkdownViewer.MarkdownProperty, subject);
        subject.OnNext("start");
        var session = viewer.BeginStreaming();
        session.Append(" more");
        session.ApplyPendingUpdate();
        Assert.Equal("start more", viewer.Markdown);

        subject.OnNext("new");

        Assert.Equal("new", viewer.Markdown);
        Assert.True(session.IsCompleted);
        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Root(viewer).Children));
        Assert.Equal("new", MarkdownSelectableTextBlock.ExtractPlainText(paragraph.Inlines!));
    }

    [AvaloniaFact]
    public void Renderer_exception_completes_the_session_and_surfaces()
    {
        var viewer = Viewer();
        viewer.Extensions.Add(new ThrowingParagraphExtension());
        var session = viewer.BeginStreaming();
        var updates = CountUpdates(session);
        session.Append("x");

        var ex = Assert.Throws<InvalidOperationException>(session.ApplyPendingUpdate);

        Assert.Equal("paragraph renderer failed", ex.Message);
        Assert.True(session.IsCompleted);
        Assert.False(viewer.IsStreaming);
        var completion = session.CompleteAsync();
        Assert.True(completion.IsFaulted);
        Assert.Same(ex, completion.Exception!.InnerException);
        Assert.Empty(updates);
        // The failed session no longer blocks a new one.
        Assert.NotSame(session, viewer.BeginStreaming());
    }

    // ── The timer ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Real_timer_applies_an_update()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming(TimeSpan.FromMilliseconds(5));
        var updates = CountUpdates(session);

        session.Append("x");
        await AsyncTestHelpers.WaitUntilAsync(() => viewer.Markdown == "x");

        Assert.Equal([true], updates);
        // The next append restarts the timer the idle tick stopped.
        await AsyncTestHelpers.PumpAsync();
        session.Append("y");
        await AsyncTestHelpers.WaitUntilAsync(() => viewer.Markdown == "xy");
        Assert.Equal(2, updates.Count);
    }

    [AvaloniaFact]
    public async Task Concurrent_appends_keep_each_threads_order()
    {
        const int threads = 4, appends = 50;
        var viewer = Viewer();
        var session = viewer.BeginStreaming();

        var writers = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < appends; i++)
                session.Append($"{t}:{i}\n");
        })).ToArray();
        await AsyncTestHelpers.WaitUntilAsync(() => writers.All(w => w.IsCompleted));
        var completion = session.CompleteAsync();
        await AsyncTestHelpers.WaitUntilAsync(() => completion.IsCompleted);

        var text = viewer.Markdown!;
        var expectedLength = Enumerable.Range(0, threads)
            .Sum(t => Enumerable.Range(0, appends).Sum(i => $"{t}:{i}\n".Length));
        Assert.Equal(expectedLength, text.Length);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(threads * appends, lines.Length);
        for (var t = 0; t < threads; t++)
        {
            var indexes = lines.Where(l => l.StartsWith($"{t}:", StringComparison.Ordinal))
                .Select(l => int.Parse(l[(l.IndexOf(':') + 1)..], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(Enumerable.Range(0, appends), indexes);
        }
    }

    // ── Detached viewers and line endings ─────────────────────────────────────

    [AvaloniaFact]
    public void Session_on_a_detached_viewer_applies_updates()
    {
        var viewer = new MarkdownViewer();
        var session = viewer.BeginStreaming();

        session.Append("a");
        session.ApplyPendingUpdate();
        session.Append("b");
        session.Complete();

        Assert.Equal("ab", viewer.Markdown);
        Assert.Single(Root(viewer).Children);
        Assert.False(viewer.IsStreaming);
    }

    [AvaloniaFact]
    public void Crlf_pair_split_across_appends_renders_one_break()
    {
        var viewer = Viewer();
        var session = viewer.BeginStreaming();

        session.Append("a\r");
        session.ApplyPendingUpdate();
        session.Append("\nb");
        session.ApplyPendingUpdate();
        session.Complete();

        var paragraph = Assert.IsType<MarkdownSelectableTextBlock>(Assert.Single(Root(viewer).Children));
        var text = MarkdownSelectableTextBlock.ExtractPlainText(paragraph.Inlines!);
        Assert.EndsWith("b", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.Equal("a\r\nb", viewer.Markdown);

        var second = Viewer();
        var split = second.BeginStreaming();
        split.Append("a\r");
        split.ApplyPendingUpdate();
        split.Append("\n\r\nb");
        split.ApplyPendingUpdate();
        split.Complete();

        Assert.Equal(2, Root(second).Children.Count);
        Assert.All(Root(second).Children, c => Assert.IsType<MarkdownSelectableTextBlock>(c));
    }
}
