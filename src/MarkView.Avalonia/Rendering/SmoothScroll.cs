// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Animates a <see cref="ScrollViewer"/>'s vertical offset to a target, one step per rendered frame, easing out
/// over a <see cref="Duration"/> that grows with the distance. The target is asked for again at each frame, so content that grows or shrinks
/// above it while the animation runs does not make it miss. The animation stops at the target, when the target
/// is gone (<c>null</c>), or as soon as something else moves the offset, e.g. the user scrolling. A jump longer
/// than <see cref="LongDistance"/> goes at once to <see cref="LongDistance"/> away from the target and animates the
/// rest, so it takes <see cref="MaxDuration"/> like the longest animated jump.
/// </summary>
internal sealed class SmoothScroll
{
    /// <summary>How long an animation over a short distance takes.</summary>
    public static readonly TimeSpan MinDuration = TimeSpan.FromMilliseconds(150);

    /// <summary>How long an animation over a long distance takes, at most.</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The distance, in pixels, from which an animation takes <see cref="MaxDuration"/>; past it, a longer jump
    /// would only scroll faster, too fast to follow.
    /// </summary>
    public static readonly double LongDistance = Math.Pow(MaxDuration.TotalMilliseconds / 8, 2);

    private readonly ScrollViewer _scrollViewer;
    private readonly Func<double?> _target;
    private double _from;
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

    /// <summary>How long the animation takes, set by its first step from the distance to the target.</summary>
    public TimeSpan Duration { get; private set; }

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

        if (_startTime is null)
        {
            _startTime = time;
            // Rounded to a whole pixel, so the content is laid out at the same pixel positions as at the target.
            if (Math.Abs(target - _from) > LongDistance)
                _from = Math.Round(target - Math.Sign(target - _from) * LongDistance);
            Duration = DurationFor(Math.Abs(target - _from));
        }
        var progress = Math.Clamp((time - _startTime.Value) / Duration, 0, 1);
        var y = _from + (target - _from) * Ease(progress);
        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, Math.Max(0, y));
        // The scroll viewer keeps the offset within its extent.
        _lastOffset = _scrollViewer.Offset.Y;

        if (progress >= 1)
            Stop();
        return IsRunning;
    }

    /// <summary>
    /// How long an animation over <paramref name="distance"/> pixels takes: in proportion to its square root, as
    /// browsers do, so a long jump is quicker per pixel than a short one, from <see cref="MinDuration"/> (up to
    /// about 350 pixels) to <see cref="MaxDuration"/> (from about 3,900 pixels).
    /// </summary>
    internal static TimeSpan DurationFor(double distance)
    {
        var duration = TimeSpan.FromMilliseconds(8 * Math.Sqrt(distance));
        return duration < MinDuration ? MinDuration : duration > MaxDuration ? MaxDuration : duration;
    }

    /// <summary>A cubic ease-out: fast at first, slowing down to the target.</summary>
    internal static double Ease(double progress) => 1 - Math.Pow(1 - progress, 3);
}
