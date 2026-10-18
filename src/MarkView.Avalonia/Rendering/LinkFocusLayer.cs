// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;

using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A transparent overlay, placed over the markdown <see cref="StackPanel"/> like
/// <see cref="DocumentSelectionLayer"/>, that hosts one <see cref="LinkFocusTarget"/> per link and
/// keeps each one over its link's text. It also records the document's keyboard stops in reading
/// order: those targets, and the focusable controls renderers emitted (e.g. a video embed's button).
/// </summary>
internal sealed class LinkFocusLayer : Panel
{
    private readonly List<IInputElement> _stops = [];

    private readonly Action<MarkdownHyperlink> _activate;

    /// <summary>Creates an empty layer whose link targets call <paramref name="activate"/>.</summary>
    internal LinkFocusLayer(Action<MarkdownHyperlink> activate)
    {
        _activate = activate;
        IsHitTestVisible = false;
    }

    /// <summary>Creates the layer for the document rendered into <paramref name="root"/>.</summary>
    public static LinkFocusLayer Create(Panel root, Action<MarkdownHyperlink> activate)
    {
        var layer = new LinkFocusLayer(activate);
        layer.Collect(root);
        return layer;
    }

    /// <summary>The number of keyboard stops recorded, in reading order.</summary>
    internal int StopCount => _stops.Count;

    /// <summary>The number of <see cref="LinkFocusTarget"/> children hosted by the layer.</summary>
    internal int TargetCount => Children.Count;

    /// <summary>
    /// Drops every keyboard stop from index <paramref name="stopCount"/> on and every link target
    /// from index <paramref name="targetCount"/> on, so the layer holds only what an earlier
    /// <see cref="StopCount"/> and <see cref="TargetCount"/> described. The discarded targets are
    /// also removed from their text blocks.
    /// </summary>
    internal void TruncateTo(int stopCount, int targetCount)
    {
        // Stryker disable Statement : List.RemoveRange rejects the same counts, before it changes anything
        ArgumentOutOfRangeException.ThrowIfNegative(stopCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stopCount, _stops.Count);
        // Stryker restore Statement
        ArgumentOutOfRangeException.ThrowIfNegative(targetCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(targetCount, Children.Count);

        _stops.RemoveRange(stopCount, _stops.Count - stopCount);
        for (var i = targetCount; i < Children.Count; i++)
        {
            if (Children[i] is LinkFocusTarget target)
                target.TextBlock.LinkTargets = null;
        }
        Children.RemoveRange(targetCount, Children.Count - targetCount);
    }

    /// <summary>
    /// The keyboard stops a user can reach now, in reading order. Stops inside hidden content
    /// (e.g. a collapsed block) are skipped.
    /// </summary>
    public List<IInputElement> GetActiveStops()
    {
        var stops = new List<IInputElement>(_stops.Count);
        foreach (var stop in _stops)
        {
            var visible = stop is LinkFocusTarget target
                ? target.TextBlock.IsEffectivelyVisible
                : stop.IsEffectivelyVisible && stop.IsEffectivelyEnabled;
            if (visible) stops.Add(stop);
        }
        return stops;
    }

    /// <summary>
    /// Records the keyboard stops under <paramref name="control"/> after those already collected, and
    /// hosts a target for each link.
    /// </summary>
    internal void Collect(Control control)
    {
        switch (control)
        {
            case MarkdownSelectableTextBlock { Inlines: { } inlines } textBlock:
                var offset = 0;
                CollectInlines(textBlock, inlines, ref offset);
                break;
            case InputElement input when input.Focusable && input.IsTabStop:
                _stops.Add(input);
                break;
            case Panel panel:
                foreach (var child in panel.Children)
                    Collect(child);
                break;
            case Decorator { Child: { } child }:
                Collect(child);
                break;
        }
    }

    // Offsets follow MarkdownSelectableTextBlock.MeasureInlineLength, which matches the text layout's.
    private void CollectInlines(MarkdownSelectableTextBlock textBlock, InlineCollection inlines, ref int offset)
    {
        foreach (var inline in inlines)
        {
            var length = MarkdownSelectableTextBlock.MeasureInlineLength(inline);
            switch (inline)
            {
                // A link without a usable address can't be opened, so it isn't a stop.
                case MarkdownHyperlink { NavigateUri: not null } link when length > 0:
                    var target = new LinkFocusTarget(textBlock, link, offset, length, _activate);
                    Children.Add(target);
                    _stops.Add(target);
                    (textBlock.LinkTargets ??= []).Add(target);
                    break;
                case MarkdownHyperlink:
                    break;
                case Span span:
                    var spanOffset = offset;
                    CollectInlines(textBlock, span.Inlines, ref spanOffset);
                    break;
                case InlineUIContainer { Child: { } child }:
                    var firstStop = _stops.Count;
                    Collect(child);
                    for (var i = firstStop; i < _stops.Count; i++)
                        KeepFocusAcrossArrange((InputElement)_stops[i]);
                    break;
            }
            offset += length;
        }
    }

    /// <summary>
    /// A text block removes and re-adds its embedded controls on every arrange (e.g. once a video
    /// thumbnail has loaded), and Avalonia drops the focus of a control that is removed, which would
    /// leave a keyboard user nowhere. Gives the focus back, the same way it was given, once the
    /// control is re-added.
    /// </summary>
    private static void KeepFocusAcrossArrange(InputElement control)
    {
        NavigationMethod? refocus = null;
        control.DetachedFromVisualTree += (_, _) =>
            refocus = !control.IsFocused ? null
                : control.Classes.Contains(":focus-visible") ? NavigationMethod.Tab : NavigationMethod.Pointer;
        control.AttachedToVisualTree += (_, _) =>
        {
            if (refocus is not { } method) return;
            refocus = null;
            control.Focus(method);
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;
    }

    // A text block can move without this layer being re-arranged (an image above it finished
    // loading, a block was collapsed), so check after every layout pass whether a target is stale.
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var stale = false;
        foreach (var child in Children)
            stale |= ((LinkFocusTarget)child).UpdateGeometry(this);
        if (stale)
            InvalidateArrange();
    }

    // Takes no room of its own: the document's size is the root panel's. Arrange measures each
    // target (to nothing) on its own.
    protected override Size MeasureOverride(Size availableSize) => default;

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var target = (LinkFocusTarget)child;
            // Stryker disable once Statement : OnLayoutUpdated would catch up one layout pass later
            target.UpdateGeometry(this);
            target.Arrange(target.GeometryBounds);
        }
        return finalSize;
    }

    // Each target is reported inside its paragraph instead (see MarkdownSelectableTextBlock's peer).
    protected override AutomationPeer OnCreateAutomationPeer() => new ChildlessPeer(this);

    private sealed class ChildlessPeer(Control owner) : NoneAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
