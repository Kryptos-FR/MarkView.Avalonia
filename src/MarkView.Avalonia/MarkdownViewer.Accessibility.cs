// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.VisualTree;

using MarkView.Avalonia.Automation;
using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia;

[PseudoClasses(":high-contrast")]
public partial class MarkdownViewer : ICustomKeyboardNavigation
{
    /// <summary>
    /// Defines the <see cref="HighContrastMode"/> property.
    /// </summary>
    public static readonly StyledProperty<HighContrastMode> HighContrastModeProperty =
        AvaloniaProperty.Register<MarkdownViewer, HighContrastMode>(nameof(HighContrastMode), HighContrastMode.System);

    /// <summary>
    /// Defines the <see cref="IsHighContrast"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, bool> IsHighContrastProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, bool>(nameof(IsHighContrast), o => o.IsHighContrast);

    /// <summary>
    /// Defines the <see cref="TextScale"/> property.
    /// </summary>
    public static readonly StyledProperty<double> TextScaleProperty =
        AvaloniaProperty.Register<MarkdownViewer, double>(nameof(TextScale), 1.0,
            validate: scale => scale > 0 && double.IsFinite(scale));

    /// <summary>
    /// Defines the <see cref="FollowSystemTextScale"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> FollowSystemTextScaleProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(FollowSystemTextScale), true);

    /// <summary>
    /// Defines the <see cref="EffectiveTextScale"/> property.
    /// </summary>
    public static readonly DirectProperty<MarkdownViewer, double> EffectiveTextScaleProperty =
        AvaloniaProperty.RegisterDirect<MarkdownViewer, double>(nameof(EffectiveTextScale), o => o.EffectiveTextScale);

    private IPlatformSettings? _platformSettings;
    private bool _platformHighContrast;
    private Window? _activationWindow;

    /// <summary>
    /// Gets or sets when the viewer uses its high-contrast palette. Defaults to
    /// <see cref="HighContrastMode.System"/>, which follows the operating system's setting.
    /// </summary>
    public HighContrastMode HighContrastMode
    {
        get => GetValue(HighContrastModeProperty);
        set => SetValue(HighContrastModeProperty, value);
    }

    /// <summary>
    /// Gets whether the viewer currently uses its high-contrast palette, i.e. whether it has the
    /// <c>:high-contrast</c> pseudo-class that <c>MarkdownTheme.axaml</c> styles against.
    /// </summary>
    public bool IsHighContrast
    {
        get;
        private set => SetAndRaise(IsHighContrastProperty, ref field, value);
    }

    /// <summary>
    /// Gets or sets the factor applied to every font size of the document, on top of the system text scale
    /// (<see cref="FollowSystemTextScale"/>): 1.5 shows the text 50% larger. Headings, code, alerts and the sizes set
    /// by theme presets or custom styles all scale; images, diagrams and spacing don't. Defaults to 1.
    /// </summary>
    public double TextScale
    {
        get => GetValue(TextScaleProperty);
        set => SetValue(TextScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the document's font sizes follow the text size the user chose in the operating
    /// system (<see cref="SystemTextScale.Factor"/>). Defaults to <c>true</c>.
    /// </summary>
    public bool FollowSystemTextScale
    {
        get => GetValue(FollowSystemTextScaleProperty);
        set => SetValue(FollowSystemTextScaleProperty, value);
    }

    /// <summary>
    /// Gets the factor applied to the document's font sizes: <see cref="TextScale"/>, times the system text scale
    /// while <see cref="FollowSystemTextScale"/> is <c>true</c>.
    /// </summary>
    public double EffectiveTextScale
    {
        get;
        private set => SetAndRaise(EffectiveTextScaleProperty, ref field, value);
    } = 1;

    /// <summary>The text of the document's first heading, if it has one.</summary>
    internal string? FirstHeadingText => _headingEntries.Count > 0 ? _headingEntries[0].Text : null;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HighContrastModeProperty)
            UpdateHighContrast();
        else if (change.Property == TextScaleProperty || change.Property == FollowSystemTextScaleProperty
                 || change.Property == FontSizeProperty)
            UpdateTextScale();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Subscribed only while attached, so the platform settings never keep a viewer that
        // was removed from its window alive.
        // Headless tests can't raise the platform's ColorValuesChanged, and their platform never
        // asks for high contrast, so nothing they observe depends on the subscription below.
        _platformSettings = this.GetPlatformSettings();
        // Stryker disable once Equality : see above
        if (_platformSettings is not null)
        {
            // Stryker disable once Assignment : see above
            _platformSettings.ColorValuesChanged += OnPlatformColorValuesChanged;
            _platformHighContrast = _platformSettings.GetColorValues().ContrastPreference == ColorContrastPreference.High;
        }
        // Stryker disable once Statement : see above
        UpdateHighContrast();

        SystemTextScale.Changed += OnSystemTextScaleChanged;
        // Avalonia raises no event when the user changes the text size, so it is read again when they come back
        // to the application, e.g. from the system settings.
        _activationWindow = TopLevel.GetTopLevel(this) as Window;
        if (_activationWindow is not null)
            _activationWindow.Activated += OnWindowActivated;
        UpdateTextScale();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Stryker disable all : headless tests can't raise the platform's ColorValuesChanged
        if (_platformSettings is not null)
            _platformSettings.ColorValuesChanged -= OnPlatformColorValuesChanged;
        _platformSettings = null;
        // Stryker restore all
        SystemTextScale.Changed -= OnSystemTextScaleChanged;
        if (_activationWindow is not null)
            _activationWindow.Activated -= OnWindowActivated;
        _activationWindow = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new MarkdownViewerAutomationPeer(this);

    /// <summary>
    /// Tab order through the document: the viewer itself, then its links and focusable controls in
    /// reading order, then whatever follows the viewer. Shift+Tab walks the same stops backwards,
    /// and entering the document backwards lands on its last stop.
    /// </summary>
    (bool handled, IInputElement? next) ICustomKeyboardNavigation.GetNext(IInputElement element, NavigationDirection direction)
    {
        if (_linkFocusLayer is null || direction is not (NavigationDirection.Next or NavigationDirection.Previous))
            return (false, null);

        var stops = _linkFocusLayer.GetActiveStops();
        var forward = direction == NavigationDirection.Next;
        if (element == this)
            return forward && stops.Count > 0 ? (true, stops[0]) : (false, null);

        var index = stops.IndexOf(element);
        if (index < 0)
        {
            // Default navigation from outside the viewer just landed on it, going backwards.
            var fromOutside = element is not Visual visual || !this.IsVisualAncestorOf(visual);
            return !forward && fromOutside && stops.Count > 0 ? (true, stops[^1]) : (false, null);
        }

        if (forward)
            // Past the last stop, (true, null) continues default navigation after the viewer.
            return (true, index + 1 < stops.Count ? stops[index + 1] : null);
        return (true, index > 0 ? stops[index - 1] : this);
    }

    internal void OnPlatformColorValuesChanged(object? sender, PlatformColorValues e)
    {
        _platformHighContrast = e.ContrastPreference == ColorContrastPreference.High;
        UpdateHighContrast();
    }

    private static void OnWindowActivated(object? sender, EventArgs e) => SystemTextScale.Refresh();

    private void OnSystemTextScaleChanged(object? sender, EventArgs e) => UpdateTextScale();

    /// <summary>Applies the effective text scale to the rendered document.</summary>
    private void UpdateTextScale()
    {
        EffectiveTextScale = TextScale * (FollowSystemTextScale ? SystemTextScale.Factor : 1);
        if (Content is Grid document)
            TextScaling.Apply(document, EffectiveTextScale, FontSize);
    }

    private void UpdateHighContrast()
    {
        var isHighContrast = HighContrastMode switch
        {
            HighContrastMode.Enabled => true,
            HighContrastMode.Disabled => false,
            _ => _platformHighContrast,
        };
        IsHighContrast = isHighContrast;
        PseudoClasses.Set(":high-contrast", isHighContrast);
    }
}
