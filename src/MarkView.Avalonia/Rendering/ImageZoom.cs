// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using MarkView.Avalonia.Rendering.Inlines;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Makes a rendered image or diagram open in <see cref="MarkdownViewer"/>'s lightbox (see
/// <see cref="MarkdownViewer.IsImageZoomEnabled"/>).
/// </summary>
internal static class ImageZoom
{
    /// <summary>
    /// Whether the lightbox may enlarge the image past its natural size to fit it, as it may a picture
    /// drawn as vectors.
    /// </summary>
    internal static readonly AttachedProperty<bool> EnlargeToFitProperty =
        AvaloniaProperty.RegisterAttached<Image, bool>("EnlargeToFit", typeof(ImageZoom));

    internal static bool GetEnlargeToFit(Image image) => image.GetValue(EnlargeToFitProperty);

    /// <summary>
    /// What the lightbox paints behind the image, for a picture that is transparent where the document
    /// behind it shows through, such as a diagram.
    /// </summary>
    internal static readonly AttachedProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.RegisterAttached<Image, IBrush?>("Background", typeof(ImageZoom));

    internal static IBrush? GetBackground(Image image) => image.GetValue(BackgroundProperty);

    internal static void SetBackground(Image image, IBrush? value) => image.SetValue(BackgroundProperty, value);

    /// <summary>
    /// Makes a loaded image ask the renderer to zoom it when clicked, or when Enter or Space is pressed
    /// while it has focus. Screen readers see it as an image named by its alt text, whose help text
    /// (from the theme's <c>markdown-image-zoomable</c> style) says what it does.
    /// </summary>
    /// <param name="renderer">The renderer whose <see cref="AvaloniaRenderer.ImageZoomRequested"/> the image raises.</param>
    /// <param name="image">The image.</param>
    /// <param name="enlargeToFit">Whether the lightbox may enlarge the image past its natural size to fit it.</param>
    internal static void MakeZoomable(AvaloniaRenderer renderer, Image image, bool enlargeToFit = false)
    {
        image.Cursor = ZoomCursor.Instance;
        image.Focusable = true;
        image.Classes.Add("markdown-image-zoomable");
        image.SetValue(EnlargeToFitProperty, enlargeToFit);
        // A Tab stop, so not decorative even without alt text.
        AutomationRoles.Set(image, AutomationControlType.Image);
        // A click, like a foldable alert header's (see CollapsibleAlert): the press implicitly captures
        // the pointer to the image, so the image sees no release once the viewer has turned the press
        // into a selection drag.
        image.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Left || image.Source is null
                || !new Rect(image.Bounds.Size).Contains(e.GetPosition(image)))
                return;
            renderer.OnImageZoomRequested(image);
            e.Handled = true;
        };
        image.KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Space) || image.Source is null) return;
            renderer.OnImageZoomRequested(image);
            e.Handled = true;
        };
    }
}
