// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// The lightbox <see cref="MarkdownViewer"/> lays over itself to show a clicked image or diagram: a
/// <c>markdown-image-lightbox</c> panel with a dimmed backdrop, the picture
/// (<c>markdown-image-lightbox-image</c>), a close button (<c>markdown-image-lightbox-close</c>) and a
/// zoom toolbar (<c>markdown-image-lightbox-toolbar</c>).
/// </summary>
/// <remarks>
/// <para>
/// The picture opens fitted to the lightbox: as large as fits, but never enlarged past its natural size,
/// unless <see cref="ImageZoom.GetEnlargeToFit"/> holds for it (a diagram, drawn as vectors), which is
/// enlarged up to <see cref="MaxFitEnlargement"/> times. A <c>markdown-image-lightbox-backing</c> border behind
/// it is painted with the picture's <see cref="ImageZoom.GetBackground"/>, if any. It can then be zoomed with the mouse wheel, a
/// pinch, the toolbar, or the + and - keys (0 fits it again), and panned by dragging it or with the arrow
/// keys. It follows the source and background of the image it was opened from, so a diagram re-rendered for
/// another theme shows in its new colours.
/// </para>
/// <para>
/// A click that does not drag, anywhere but on the toolbar, closes the lightbox, as do the close button
/// and Escape: each raises <see cref="CloseRequested"/>.
/// </para>
/// </remarks>
internal sealed class ImageLightbox : Panel
{
    // The backdrop without the theme, which sets MarkdownImageLightboxBackdrop; a backdrop is also
    // what makes the whole lightbox clickable.
    private static readonly IBrush DefaultBackdrop = new ImmutableSolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0));

    /// <summary>How far a picture that can be enlarged is enlarged at most to fit the lightbox.</summary>
    internal const double MaxFitEnlargement = 2;

    /// <summary>How much larger than its natural or fitted size, whichever is larger, a picture can be zoomed.</summary>
    internal const double MaxZoomFactor = 8;

    /// <summary>The zoom factor of a toolbar button or key press.</summary>
    internal const double ZoomStep = 1.25;

    /// <summary>The zoom factor of one notch of the mouse wheel.</summary>
    internal const double WheelZoomStep = 1.2;

    /// <summary>How far, in pixels, the pointer moves before a press becomes a drag rather than a click.</summary>
    internal const double DragThreshold = 4;

    /// <summary>The share of the lightbox an arrow key pans by.</summary>
    internal const double KeyPanFraction = 0.1;

    private readonly Image _picture;
    private readonly bool _enlargeToFit;

    private bool _isFitted = true;
    private double _zoom = 1;
    // How far the centre of the picture is from the centre of the view.
    private Vector _pan;
    private Rect _view;

    private IPointer? _pressPointer;
    private Point _pressPosition;
    private Point _lastPosition;
    private bool _dragging;
    // The scale of the pinch in progress, or null.
    private double? _pinchScale;

    static ImageLightbox()
    {
        BackgroundProperty.OverrideDefaultValue<ImageLightbox>(DefaultBackdrop);
        FocusableProperty.OverrideDefaultValue<ImageLightbox>(true);
        ClipToBoundsProperty.OverrideDefaultValue<ImageLightbox>(true);
        // Tab moves between the lightbox and its buttons only, never to the document behind.
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<ImageLightbox>(KeyboardNavigationMode.Cycle);
    }

    /// <param name="picture">The document's image to show.</param>
    /// <param name="returnFocus">What had focus when the lightbox opened.</param>
    public ImageLightbox(Image picture, IInputElement? returnFocus)
    {
        _picture = picture;
        _enlargeToFit = ImageZoom.GetEnlargeToFit(picture);
        ReturnFocus = returnFocus;
        Classes.Add("markdown-image-lightbox");
        // Focused when it opens, so it is announced: by the picture's name (an image's alt text), or
        // the theme's name without one.
        AutomationRoles.Set(this, AutomationControlType.Image);
        if (AutomationProperties.GetName(picture) is { Length: > 0 } name)
            AutomationProperties.SetName(this, name);

        // Arranged at the picture's zoomed size and position by ArrangeOverride. Neither it nor its
        // backing is hit-testable, so a press anywhere captures the pointer to the lightbox itself,
        // which then sees the pointer's capture lost too.
        Image = new Image { Source = picture.Source, Stretch = Stretch.Fill, IsHitTestVisible = false };
        Image.Classes.Add("markdown-image-lightbox-image");

        Backing = new Border { Background = ImageZoom.GetBackground(picture), IsHitTestVisible = false };
        Backing.Classes.Add("markdown-image-lightbox-backing");

        CloseButton = CreateButton(new CloseGlyph(), "markdown-image-lightbox-close", () => CloseRequested?.Invoke());
        CloseButton.HorizontalAlignment = HorizontalAlignment.Right;
        CloseButton.VerticalAlignment = VerticalAlignment.Top;

        ZoomOutButton = CreateButton(new ZoomGlyph(ZoomGlyphKind.ZoomOut), "markdown-image-lightbox-zoom-out", ZoomOut);
        ZoomInButton = CreateButton(new ZoomGlyph(ZoomGlyphKind.ZoomIn), "markdown-image-lightbox-zoom-in", ZoomIn);
        FitButton = CreateButton(new ZoomGlyph(ZoomGlyphKind.Fit), "markdown-image-lightbox-zoom-fit", ZoomToFit);
        foreach (var button in new[] { ZoomOutButton, ZoomInButton, FitButton })
            button.Classes.Add("markdown-image-lightbox-zoom");

        ZoomLevel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
        ZoomLevel.Classes.Add("markdown-image-lightbox-zoom-level");

        Toolbar = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { ZoomOutButton, ZoomLevel, ZoomInButton, FitButton },
            },
        };
        Toolbar.Classes.Add("markdown-image-lightbox-toolbar");

        // Tab order: the close button, then the toolbar.
        Children.Add(Backing);
        Children.Add(Image);
        Children.Add(CloseButton);
        Children.Add(Toolbar);

        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(PinchEvent, OnPinch);
        AddHandler(PinchEndedEvent, (_, _) => _pinchScale = null);
        AddHandler(PointerTouchPadGestureMagnifyEvent, OnMagnify);
        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
        KeyDown += OnKeyDown;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerWheelChanged += OnPointerWheelChanged;
    }

    /// <summary>What had focus when the lightbox opened, to focus again when it closes.</summary>
    public IInputElement? ReturnFocus { get; }

    /// <summary>The picture shown.</summary>
    public Image Image { get; }

    /// <summary>The border behind the picture, painted with its <see cref="ImageZoom.GetBackground"/>.</summary>
    public Border Backing { get; }

    /// <summary>The button that closes the lightbox.</summary>
    public Button CloseButton { get; }

    /// <summary>The toolbar holding the zoom buttons and level.</summary>
    public Border Toolbar { get; }

    /// <summary>The toolbar button that zooms out.</summary>
    public Button ZoomOutButton { get; }

    /// <summary>The toolbar button that zooms in.</summary>
    public Button ZoomInButton { get; }

    /// <summary>The toolbar button that fits the picture to the lightbox again.</summary>
    public Button FitButton { get; }

    /// <summary>The zoom level shown in the toolbar, as a percentage of the picture's natural size.</summary>
    public TextBlock ZoomLevel { get; }

    /// <summary>The picture's scale relative to its natural size.</summary>
    public double Zoom => _zoom;

    /// <summary>Whether the picture is fitted to the lightbox, which it keeps to when the lightbox is resized.</summary>
    public bool IsFitted => _isFitted;

    /// <summary>Raised when the user asks to close the lightbox.</summary>
    public event Action? CloseRequested;

    private Size NaturalSize => Image.Source?.Size ?? default;


    private double FitZoom
    {
        get
        {
            var natural = NaturalSize;
            if (natural.Width <= 0 || natural.Height <= 0) return 0;
            var fit = Math.Min(_view.Width / natural.Width, _view.Height / natural.Height);
            return Math.Min(fit, _enlargeToFit ? MaxFitEnlargement : 1);
        }
    }

    private double MinZoom => Math.Min(FitZoom, 1);

    private double MaxZoom => Math.Max(FitZoom, 1) * MaxZoomFactor;

    /// <summary>
    /// The element Tab (<paramref name="forward"/>) or Shift+Tab moves to from <paramref name="current"/>:
    /// round the lightbox and its buttons, never out of it.
    /// </summary>
    internal IInputElement GetNextTabStop(IInputElement current, bool forward)
    {
        var stops = new List<IInputElement> { this };
        foreach (var button in new[] { CloseButton, ZoomOutButton, ZoomInButton, FitButton })
        {
            if (button is { Focusable: true, IsEffectivelyVisible: true, IsEffectivelyEnabled: true })
                stops.Add(button);
        }
        var index = Math.Max(0, stops.IndexOf(current));
        return stops[(index + (forward ? 1 : stops.Count - 1)) % stops.Count];
    }

    /// <summary>Zooms in one step, about the centre of the lightbox.</summary>
    public void ZoomIn() => ZoomBy(ZoomStep, _view.Center);

    /// <summary>Zooms out one step, about the centre of the lightbox.</summary>
    public void ZoomOut() => ZoomBy(1 / ZoomStep, _view.Center);

    /// <summary>Fits the picture to the lightbox again, centred.</summary>
    public void ZoomToFit()
    {
        _isFitted = true;
        _pan = default;
        InvalidateArrange();
    }

    /// <summary>
    /// Scales the picture by <paramref name="factor"/>, within its zoom range, keeping the point of it
    /// under <paramref name="origin"/> (in the lightbox's coordinates) in place.
    /// </summary>
    public void ZoomBy(double factor, Point origin)
    {
        // Nothing to zoom before the lightbox is laid out, or without a picture.
        var fit = FitZoom;
        if (fit <= 0) return;
        var zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        // Zooming back and forth by the same steps returns to the fitted size despite rounding.
        // Stryker disable once Equality : equal to within 1e-9 times the fitted zoom is not a case rounding produces
        if (Math.Abs(zoom - fit) < fit * 1e-9) zoom = fit;
        var centre = _view.Center + _pan;
        _pan = origin + (centre - origin) * (zoom / _zoom) - _view.Center;
        _zoom = zoom;
        _isFitted = zoom == fit;
        // Clamped once arranged.
        InvalidateArrange();
    }

    /// <summary>
    /// Moves the picture by <paramref name="delta"/>, as far as it stays over the lightbox. Clamped at
    /// once, so that a drag back from past an edge moves the picture straight away.
    /// </summary>
    public void PanBy(Vector delta)
    {
        _pan += delta;
        ClampPan();
        InvalidateArrange();
    }

    // A picture larger than the view along an axis can be panned until its edge reaches the view's
    // edge; along an axis it fits, it stays centred.
    private void ClampPan()
    {
        var size = NaturalSize * _zoom;
        var excessX = Math.Max(0, (size.Width - _view.Width) / 2);
        var excessY = Math.Max(0, (size.Height - _view.Height) / 2);
        _pan = new Vector(Math.Clamp(_pan.X, -excessX, excessX), Math.Clamp(_pan.Y, -excessY, excessY));
    }

    private Button CreateButton(Control glyph, string className, Action action)
    {
        var button = new Button { Content = glyph };
        button.Classes.Add(className);
        button.Click += (_, e) =>
        {
            action();
            e.Handled = true;
        };
        return button;
    }

    // The lightbox covers its host whatever its size, so it never makes the host larger: its children
    // are measured, but it asks for no room, and is then arranged over the whole host.
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The picture's margin keeps it off the lightbox's edges when it is fitted.
        _view = new Rect(finalSize).Deflate(Image.Margin);
        // Without a picture, or room to show it, there is nothing to zoom.
        if (FitZoom > 0)
        {
            if (_isFitted)
            {
                _zoom = FitZoom;
                _pan = default;
            }
            else
            {
                _zoom = Math.Clamp(_zoom, MinZoom, MaxZoom);
            }
            ClampPan();
            var size = NaturalSize * _zoom;
            var centre = _view.Center + _pan;
            var bounds = new Rect(centre.X - size.Width / 2, centre.Y - size.Height / 2, size.Width, size.Height);
            Image.Arrange(bounds.Inflate(Image.Margin));
            // Where the picture was arranged, after layout rounding.
            Backing.Arrange(Image.Bounds);
        }
        else
        {
            Image.Arrange(new Rect(finalSize));
            Backing.Arrange(default);
        }
        CloseButton.Arrange(new Rect(finalSize));
        Toolbar.Arrange(new Rect(finalSize));
        ZoomLevel.Text = string.Format(CultureInfo.CurrentCulture, "{0:0}%", _zoom * 100);
        return finalSize;
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Image.Source = _picture.Source;
        Backing.Background = ImageZoom.GetBackground(_picture);
        _picture.PropertyChanged += OnPicturePropertyChanged;
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) =>
        _picture.PropertyChanged -= OnPicturePropertyChanged;

    // A diagram is rendered again in the colours of a new theme; a picture removed from the document
    // (whose lightbox is about to close) keeps showing.
    private void OnPicturePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ImageZoom.BackgroundProperty)
            Backing.Background = ImageZoom.GetBackground(_picture);
        if (e.Property != Image.SourceProperty || _picture.Source is not { } source) return;
        Image.Source = source;
        InvalidateArrange();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var pan = new Vector(_view.Width, _view.Height) * KeyPanFraction;
        switch (e.Key)
        {
            case Key.Escape: CloseRequested?.Invoke(); break;
            case Key.OemPlus or Key.Add: ZoomIn(); break;
            case Key.OemMinus or Key.Subtract: ZoomOut(); break;
            case Key.D0 or Key.NumPad0: ZoomToFit(); break;
            // An arrow key moves the view over the picture, so the picture moves the other way.
            case Key.Left: PanBy(new Vector(pan.X, 0)); break;
            case Key.Right: PanBy(new Vector(-pan.X, 0)); break;
            case Key.Up: PanBy(new Vector(0, pan.Y)); break;
            case Key.Down: PanBy(new Vector(0, -pan.Y)); break;
            default: return;
        }
        e.Handled = true;
    }

    // A second touch, which may start a pinch, leaves the first one's press as it is.
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pressPointer is not null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressPointer = e.Pointer;
        _pressPosition = _lastPosition = e.GetPosition(this);
        _dragging = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer != _pressPointer) return;
        var position = e.GetPosition(this);
        if (!_dragging)
        {
            var moved = position - _pressPosition;
            if (Math.Sqrt(moved.X * moved.X + moved.Y * moved.Y) < DragThreshold) return;
            _dragging = true;
            Cursor = new Cursor(StandardCursorType.SizeAll);
        }
        PanBy(position - _lastPosition);
        _lastPosition = position;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer != _pressPointer) return;
        var dragged = _dragging;
        EndPress();
        if (dragged || e.InitialPressMouseButton != MouseButton.Left) return;
        // A click, as on a foldable alert header: the press implicitly captured the pointer to the
        // lightbox, so a release here started here.
        if (!new Rect(Bounds.Size).Contains(e.GetPosition(this)) || IsOnToolbar(e.Source)) return;
        CloseRequested?.Invoke();
        e.Handled = true;
    }

    // Also when a pinch starts: its recognizer captures both touches.
    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer == _pressPointer)
            EndPress();
    }

    private void EndPress()
    {
        _pressPointer = null;
        Cursor = null;
    }

    private bool IsOnToolbar(object? source) =>
        source is Visual visual && (visual == Toolbar || Toolbar.IsVisualAncestorOf(visual));

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // A sideways scroll (Delta.Y is 0) zooms by a factor of 1, so not at all.
        ZoomBy(Math.Pow(WheelZoomStep, e.Delta.Y), e.GetPosition(this));
        e.Handled = true;
    }

    // The pinch's scale is the distance between the two touches over their distance when it started.
    private void OnPinch(object? sender, PinchEventArgs e)
    {
        var previous = _pinchScale ?? 1;
        _pinchScale = e.Scale;
        // After the two touches met, the scale starts again from nothing.
        if (previous > 0)
            ZoomBy(e.Scale / previous, e.ScaleOrigin);
        e.Handled = true;
    }

    // A touchpad pinch on macOS.
    private void OnMagnify(object? sender, PointerDeltaEventArgs e)
    {
        ZoomBy(1 + e.Delta.X, e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>A glyph drawn as round-capped strokes in the text colour, sized by the font size.</summary>
    /// <remarks>Drawn rather than taken from a font, as the browser build only has the application's fonts.</remarks>
    internal abstract class StrokeGlyph : Control
    {
        static StrokeGlyph()
        {
            AffectsRender<StrokeGlyph>(TextElement.ForegroundProperty);
            AffectsMeasure<StrokeGlyph>(TextElement.FontSizeProperty);
        }

        internal double BoxSize => Math.Round(GetValue(TextElement.FontSizeProperty));

        protected override Size MeasureOverride(Size availableSize) => new(BoxSize, BoxSize);

        public override void Render(DrawingContext context)
        {
            var pen = new Pen(GetValue(TextElement.ForegroundProperty), BoxSize / 8, lineCap: PenLineCap.Round);
            foreach (var (start, end) in GetStrokes())
                context.DrawLine(pen, start, end);
        }

        /// <summary>The glyph's strokes, in the control's coordinates.</summary>
        internal abstract (Point Start, Point End)[] GetStrokes();

        private protected Point At(double x, double y) => new(BoxSize * x, BoxSize * y);
    }

    /// <summary>The cross of the close button.</summary>
    internal sealed class CloseGlyph : StrokeGlyph
    {
        internal override (Point Start, Point End)[] GetStrokes() =>
        [
            (At(0.2, 0.2), At(0.8, 0.8)),
            (At(0.8, 0.2), At(0.2, 0.8)),
        ];
    }

    internal enum ZoomGlyphKind
    {
        ZoomIn,
        ZoomOut,
        Fit,
    }

    /// <summary>The plus, minus or four corners of a zoom button.</summary>
    internal sealed class ZoomGlyph(ZoomGlyphKind kind) : StrokeGlyph
    {
        internal ZoomGlyphKind Kind { get; } = kind;

        internal override (Point Start, Point End)[] GetStrokes() => Kind switch
        {
            ZoomGlyphKind.ZoomIn => [(At(0.2, 0.5), At(0.8, 0.5)), (At(0.5, 0.2), At(0.5, 0.8))],
            ZoomGlyphKind.ZoomOut => [(At(0.2, 0.5), At(0.8, 0.5))],
            _ =>
            [
                (At(0.15, 0.4), At(0.15, 0.15)), (At(0.15, 0.15), At(0.4, 0.15)),
                (At(0.6, 0.15), At(0.85, 0.15)), (At(0.85, 0.15), At(0.85, 0.4)),
                (At(0.85, 0.6), At(0.85, 0.85)), (At(0.85, 0.85), At(0.6, 0.85)),
                (At(0.4, 0.85), At(0.15, 0.85)), (At(0.15, 0.85), At(0.15, 0.6)),
            ],
        };
    }
}
