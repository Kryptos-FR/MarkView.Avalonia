// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>
    /// Defines the <see cref="IsSmoothScrollingEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsSmoothScrollingEnabledProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(IsSmoothScrollingEnabled));

    private SmoothScroll? _smoothScroll;

    /// <summary>
    /// Gets or sets whether jumping to an anchor, with a <c>#anchor</c> link, a footnote or
    /// <see cref="ScrollToAnchor"/>, scrolls there in a short animation rather than at once. Opening a document
    /// at a fragment (<c>Source</c> ending with <c>#anchor</c>) still shows it at once, as a browser does.
    /// The animation stops as soon as the user scrolls. Defaults to <c>false</c>. Can be set from a style, e.g.
    /// for every viewer of an application, or bound to the application's own reduced-motion setting.
    /// </summary>
    public bool IsSmoothScrollingEnabled
    {
        get => GetValue(IsSmoothScrollingEnabledProperty);
        set => SetValue(IsSmoothScrollingEnabledProperty, value);
    }

    /// <summary>The smooth scroll in progress, or <c>null</c>, for tests.</summary>
    internal SmoothScroll? ActiveSmoothScroll => _smoothScroll is { IsRunning: true } scroll ? scroll : null;

    /// <summary>
    /// Scrolls <c>PART_ScrollViewer</c> vertically to the offset <paramref name="target"/> returns, animated
    /// when <paramref name="animate"/> and <see cref="IsSmoothScrollingEnabled"/> are both <c>true</c> and the
    /// viewer is shown. The animation asks for the target at every frame, so it follows content that grows
    /// above it while it runs.
    /// </summary>
    private void ScrollVerticallyTo(Func<double?> target, bool animate)
    {
        if (_scrollViewer is not { } scrollViewer || target() is not { } y)
            return;
        if (animate && IsSmoothScrollingEnabled && TopLevel.GetTopLevel(this) is { } topLevel)
        {
            _smoothScroll = new SmoothScroll(scrollViewer, target);
            _smoothScroll.Start(topLevel);
            return;
        }
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, Math.Max(0, y));
    }

    private void StopSmoothScroll()
    {
        _smoothScroll?.Stop();
        _smoothScroll = null;
    }
}
