// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// The lightbox <see cref="MarkdownViewer"/> lays over itself to show a clicked image: a
/// <c>markdown-image-lightbox</c> panel with a dimmed backdrop, the image as large as fits but never
/// enlarged past its natural size (<c>markdown-image-lightbox-image</c>), and a close button
/// (<c>markdown-image-lightbox-close</c>). Clicking anywhere, the close button included, or pressing
/// Escape raises <see cref="CloseRequested"/>.
/// </summary>
internal sealed class ImageLightbox : Panel
{
    // The backdrop without the theme, which sets MarkdownImageLightboxBackdrop; a backdrop is also
    // what makes the whole lightbox clickable.
    private static readonly IBrush DefaultBackdrop = new ImmutableSolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0));

    static ImageLightbox()
    {
        BackgroundProperty.OverrideDefaultValue<ImageLightbox>(DefaultBackdrop);
        FocusableProperty.OverrideDefaultValue<ImageLightbox>(true);
        // Tab moves between the lightbox and its close button only, never to the document behind.
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<ImageLightbox>(KeyboardNavigationMode.Cycle);
    }

    public ImageLightbox(IImage source, string? altText, IInputElement? returnFocus)
    {
        ReturnFocus = returnFocus;
        Classes.Add("markdown-image-lightbox");
        if (!string.IsNullOrEmpty(altText))
            AutomationProperties.SetName(this, altText);

        Image = new Image
        {
            Source = source,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Image.Classes.Add("markdown-image-lightbox-image");

        CloseButton = new Button
        {
            Content = new CloseGlyph(),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };
        CloseButton.Classes.Add("markdown-image-lightbox-close");
        CloseButton.Click += (_, e) =>
        {
            CloseRequested?.Invoke();
            e.Handled = true;
        };

        Children.Add(Image);
        Children.Add(CloseButton);

        // A click, as on a foldable alert header: the press implicitly captured the pointer to the
        // lightbox (or the image in it), so a release here started here.
        PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Left
                || !new Rect(Bounds.Size).Contains(e.GetPosition(this)))
                return;
            CloseRequested?.Invoke();
            e.Handled = true;
        };
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            CloseRequested?.Invoke();
            e.Handled = true;
        };
    }

    /// <summary>What had focus when the lightbox opened, to focus again when it closes.</summary>
    public IInputElement? ReturnFocus { get; }

    /// <summary>The image shown.</summary>
    public Image Image { get; }

    /// <summary>The button that closes the lightbox.</summary>
    public Button CloseButton { get; }

    /// <summary>Raised when the user asks to close the lightbox.</summary>
    public event Action? CloseRequested;

    // The lightbox covers its host whatever its size, so it never makes the host larger: its children
    // are measured, but it asks for no room, and is then arranged over the whole host.
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return default;
    }

    /// <summary>The cross of the close button, drawn in the button's text colour and sized by its font size.</summary>
    /// <remarks>Drawn rather than taken from a font, as the browser build only has the application's fonts.</remarks>
    internal sealed class CloseGlyph : Control
    {
        static CloseGlyph()
        {
            AffectsRender<CloseGlyph>(TextElement.ForegroundProperty);
            AffectsMeasure<CloseGlyph>(TextElement.FontSizeProperty);
        }

        internal double BoxSize => Math.Round(GetValue(TextElement.FontSizeProperty));

        protected override Size MeasureOverride(Size availableSize) => new(BoxSize, BoxSize);

        public override void Render(DrawingContext context)
        {
            var pen = new Pen(GetValue(TextElement.ForegroundProperty), BoxSize / 8, lineCap: PenLineCap.Round);
            foreach (var (start, end) in GetStrokes())
                context.DrawLine(pen, start, end);
        }

        /// <summary>The two strokes of the cross, in the control's coordinates.</summary>
        internal (Point Start, Point End)[] GetStrokes()
        {
            var size = BoxSize;
            return
            [
                (new Point(size * 0.2, size * 0.2), new Point(size * 0.8, size * 0.8)),
                (new Point(size * 0.8, size * 0.2), new Point(size * 0.2, size * 0.8)),
            ];
        }
    }
}
