// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;

using MarkView.Avalonia.Automation;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The fold button at the start of a foldable heading (class <c>markdown-heading-fold</c>, see
/// <see cref="HeadingFold"/>): a chevron, like a foldable alert's, that a click, Enter or Space toggles.
/// It is a Tab stop, draws a focus ring when focused from the keyboard, and is exposed to screen readers
/// as a button that expands or collapses, named by its heading's text.
/// </summary>
internal sealed class HeadingFoldToggle : AlertChevron
{
    public HeadingFoldToggle()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        AddHandler(PointerReleasedEvent, (_, e) =>
        {
            // A click, like a foldable alert header's: the press captures the pointer to the toggle, so the
            // release comes here only when the press started on it, and not at all once the viewer has
            // turned the press into a selection drag.
            if (e.InitialPressMouseButton != MouseButton.Left || !new Rect(Bounds.Size).Contains(e.GetPosition(this)))
                return;
            RaiseToggled();
            e.Handled = true;
        });
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Space)) return;
            RaiseToggled();
            e.Handled = true;
        });
    }

    // A heading's text is large: a smaller triangle keeps it from competing with the text, in a square
    // as large as an alert's chevron, so the button stays easy to hit.
    private protected override double TriangleScale => 0.7;

    /// <summary>Raised when the user asks to fold or unfold the section.</summary>
    internal event Action? Toggled;

    internal void RaiseToggled() => Toggled?.Invoke();

    /// <summary>The text screen readers announce for the button: its heading's text.</summary>
    internal string AccessibleName { get; set; } = string.Empty;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
            (ControlAutomationPeer.FromElement(this) as HeadingFoldAutomationPeer)?.RaiseExpandCollapseStateChanged(change.GetOldValue<bool>(), change.GetNewValue<bool>());
    }

    // The focus ring depends on :focus-visible, which the base sets as focus arrives and clears as it leaves.
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // A transparent fill makes the whole box clickable, not just the triangle.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        base.Render(context);
        if (IsFocused && Classes.Contains(":focus-visible") && GetValue(TextElement.ForegroundProperty) is { } foreground)
            context.DrawRectangle(null, new Pen(foreground), new RoundedRect(new Rect(Bounds.Size).Deflate(0.5), 2));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HeadingFoldAutomationPeer(this);
}
