// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Animates a <see cref="ScrollViewer"/>'s vertical offset to a target, one step per rendered frame, easing out
/// over <see cref="Duration"/>. The target is asked for again at each frame, so content that grows or shrinks
/// above it while the animation runs does not make it miss. The animation stops at the target, when the target
/// is gone (<c>null</c>), or as soon as something else moves the offset, e.g. the user scrolling.
/// </summary>
internal sealed class SmoothScroll
{
    /// <summary>How long an animation takes, whatever the distance.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(300);

    private readonly ScrollViewer _scrollViewer;
    private readonly Func<double?> _target;
    private readonly double _from;
    private double _lastOffset;
    private TimeSpan? _startTime;

    /// <summary>
    /// Prepares an animation of <paramref name="scrollViewer"/>'s vertical offset from where it is now to the
    /// offset <paramref name="target"/> returns, or until it returns <c>null</c>. Nothing moves until
    /// <see cref="Start"/> or <see cref="Step"/> is called.
    /// </summary>
    public SmoothScroll(ScrollViewer scrollViewer, Func<double?> target)
    {
        _scrollViewer = scrollViewer;
        _target = target;
        _from = _lastOffset = scrollViewer.Offset.Y;
    }

    /// <summary>Whether the animation has not finished or been stopped yet.</summary>
    public bool IsRunning { get; private set; } = true;

    /// <summary>Starts the animation on <paramref name="topLevel"/>'s frames.</summary>
    public void Start(TopLevel topLevel)
    {
        topLevel.RequestAnimationFrame(OnFrame);

        void OnFrame(TimeSpan time)
        {
            if (Step(time))
                topLevel.RequestAnimationFrame(OnFrame);
        }
    }

    /// <summary>Stops the animation where it is.</summary>
    public void Stop() => IsRunning = false;

    /// <summary>
    /// Moves the offset to where it should be at <paramref name="time"/>, the time of a frame; the first step's
    /// time is the animation's start. Returns whether the animation goes on after this step.
    /// </summary>
    internal bool Step(TimeSpan time)
    {
        if (!IsRunning)
            return false;
        // Stryker disable once Equality : the offset read back is the one set; the half pixel only absorbs rounding
        if (Math.Abs(_scrollViewer.Offset.Y - _lastOffset) > 0.5)
        {
            Stop();
            return false;
        }
        // The target is measured from where the content is laid out, which must be at the current offset.
        _scrollViewer.UpdateLayout();
        if (_target() is not { } target)
        {
            Stop();
            return false;
        }

        _startTime ??= time;
        var progress = Math.Clamp((time - _startTime.Value) / Duration, 0, 1);
        var y = _from + (target - _from) * Ease(progress);
        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, Math.Max(0, y));
        // The scroll viewer keeps the offset within its extent.
        _lastOffset = _scrollViewer.Offset.Y;

        if (progress >= 1)
            Stop();
        return IsRunning;
    }

    /// <summary>A cubic ease-out: fast at first, slowing down to the target.</summary>
    internal static double Ease(double progress) => 1 - Math.Pow(1 - progress, 3);
}
