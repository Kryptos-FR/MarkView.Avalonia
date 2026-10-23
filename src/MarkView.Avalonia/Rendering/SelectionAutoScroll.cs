// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Scrolls a <see cref="ScrollViewer"/> vertically while a drag selects text with the pointer near or past its top or
/// bottom edge, one step per rendered frame, as text boxes and browsers do: the further past the edge, the faster,
/// up to <see cref="MaxSpeed"/>. It runs on frames rather than on pointer moves, so it goes on while the pointer is
/// held still; the viewer extends the selection after each layout pass that finds the offset moved. It stops once
/// the pointer is back away from the edges, or when <see cref="Stop"/> ends it with the drag. One instance serves
/// one drag.
/// </summary>
/// <remarks>
/// The edge band inside the viewport matters where the pointer cannot get past the edge: a window that fills the
/// screen, or a browser page whose canvas ends with the viewer, which only reports the pointer while it is over it.
/// A drag that starts in a band, on a line cut by the edge, only scrolls once it has left the band or gone past
/// the edge, so that selecting within that line does not scroll it away.
/// </remarks>
internal sealed class SelectionAutoScroll
{
    /// <summary>How far inside the viewport, in pixels, from its top or bottom edge scrolling starts.</summary>
    public const double EdgeSize = 24;

    /// <summary>How much faster, in pixels per second, scrolling gets for each pixel the pointer goes further.</summary>
    public const double SpeedPerPixel = 20;

    /// <summary>The fastest scrolling, in pixels per second, however far past the edge the pointer is.</summary>
    public const double MaxSpeed = 4000;

    // A frame late after a stall (e.g. a long layout pass) moves no further than this one would have.
    // Stryker disable once all : a static initializer runs before any mutant is activated, so its mutants always survive
    private static readonly TimeSpan MaxStep = TimeSpan.FromMilliseconds(100);

    private readonly ScrollViewer _scrollViewer;
    private TimeSpan? _lastTime;
    private Action<Action<TimeSpan>>? _requestFrame;
    // Whether the pointer has been between the edge bands in this drag, from when the bands count.
    private bool _leftEdgeBand;

    /// <summary>Prepares the scrolling of <paramref name="scrollViewer"/>; nothing moves until <see cref="Update"/>.</summary>
    public SelectionAutoScroll(ScrollViewer scrollViewer) => _scrollViewer = scrollViewer;

    /// <summary>The pointer's last position, in the scroll viewer's coordinates.</summary>
    public Point Pointer { get; private set; }

    /// <summary>Whether the frames are scrolling, from a pointer move into an edge band until it is out of it again.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Records where the pointer is now, in the scroll viewer's coordinates, and starts scrolling on the frames
    /// <paramref name="requestFrame"/> asks for (e.g. <see cref="TopLevel.RequestAnimationFrame"/>) when it is near
    /// or past the top or bottom edge.
    /// </summary>
    public void Update(Point pointer, Action<Action<TimeSpan>>? requestFrame)
    {
        Pointer = pointer;
        _leftEdgeBand |= SpeedAt(pointer.Y, _scrollViewer.Bounds.Height) == 0;
        if (IsRunning || Speed == 0)
            return;
        IsRunning = true;
        _lastTime = null;
        _requestFrame = requestFrame;
        requestFrame?.Invoke(OnFrame);
    }

    /// <summary>Stops scrolling where it is.</summary>
    public void Stop()
    {
        IsRunning = false;
        // Stryker disable once Statement : a stopped step asks for no frame anyway; this only lets the top level go
        _requestFrame = null;
    }

    private void OnFrame(TimeSpan time)
    {
        if (Step(time))
            _requestFrame?.Invoke(OnFrame);
    }

    /// <summary>
    /// Moves the offset by as far as the pointer's speed goes since the last step, at <paramref name="time"/>, the
    /// time of a frame; the first step only starts the clock. Returns whether scrolling goes on after this step.
    /// </summary>
    internal bool Step(TimeSpan time)
    {
        if (!IsRunning)
            return false;
        var speed = Speed;
        if (speed == 0)
        {
            Stop();
            return false;
        }

        var elapsed = _lastTime is { } last ? time - last : TimeSpan.Zero;
        _lastTime = time;
        // Stryker disable once Equality : at exactly the longest step, both branches move as far
        if (elapsed > MaxStep)
            elapsed = MaxStep;
        // The scroll viewer keeps the offset within its extent.
        var y = Math.Max(0, _scrollViewer.Offset.Y + speed * elapsed.TotalSeconds);
        // Stryker disable once all : setting the offset it already has changes nothing
        if (y != _scrollViewer.Offset.Y)
            _scrollViewer.Offset = _scrollViewer.Offset.WithY(y);
        return true;
    }

    /// <summary>The scrolling speed at the pointer's last position: see <see cref="SpeedAt"/>, and the remarks on a drag that starts in a band.</summary>
    private double Speed
    {
        get
        {
            var height = _scrollViewer.Bounds.Height;
            // Stryker disable once Equality : on the edge itself, the pointer is in the band either way
            var outside = Pointer.Y < 0 || Pointer.Y > height;
            return _leftEdgeBand || outside ? SpeedAt(Pointer.Y, height) : 0;
        }
    }

    /// <summary>
    /// The scrolling speed, in pixels per second, for the pointer at <paramref name="y"/> in a viewport
    /// <paramref name="height"/> pixels high: negative (up) within <see cref="EdgeSize"/> of its top edge or above
    /// it, positive (down) near or below its bottom edge, zero in between. A viewport too short for both bands
    /// keeps a quarter of its height for each.
    /// </summary>
    internal static double SpeedAt(double y, double height)
    {
        var band = Math.Min(EdgeSize, height / 4);
        // Stryker disable once Equality : at the inner edge of a band, the distance into it is zero either way
        var distance = y < band ? y - band : y > height - band ? y - (height - band) : 0;
        return Math.Clamp(distance * SpeedPerPixel, -MaxSpeed, MaxSpeed);
    }
}
