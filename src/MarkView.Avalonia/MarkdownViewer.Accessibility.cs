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

namespace MarkView.Avalonia;

[PseudoClasses(":high-contrast", ":copy-on-hover")]
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

    private IPlatformSettings? _platformSettings;
    private bool _platformHighContrast;

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

    /// <summary>The text of the document's first heading, if it has one.</summary>
    internal string? FirstHeadingText => _headingEntries.Count > 0 ? _headingEntries[0].Text : null;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HighContrastModeProperty)
            UpdateHighContrast();
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
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Stryker disable all : headless tests can't raise the platform's ColorValuesChanged
        if (_platformSettings is not null)
            _platformSettings.ColorValuesChanged -= OnPlatformColorValuesChanged;
        _platformSettings = null;
        // Stryker restore all
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
        if (direction is not (NavigationDirection.Next or NavigationDirection.Previous))
            return (false, null);
        // Tab stays in the open lightbox.
        if (_lightbox is { } lightbox && element is Visual inner && (inner == lightbox || lightbox.IsVisualAncestorOf(inner)))
            return (true, lightbox.GetNextTabStop(element, direction == NavigationDirection.Next));
        if (_linkFocusLayer is null)
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
