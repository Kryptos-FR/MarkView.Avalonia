// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Styling;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.VisualTree;

using MarkView.Avalonia.Automation;
using MarkView.Avalonia.Rendering;

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

    /// <summary>
    /// Defines the InHighContrast attached property, which is <c>true</c> on the viewer and inherited by every
    /// element of its document while the viewer is in high contrast (<see cref="IsHighContrast"/>). A control
    /// theme matches it on its own control, <c>^[(mv|MarkdownViewer.InHighContrast)=True]</c>, where a
    /// <c>:high-contrast</c> style would cost memory on every control of its type.
    /// </summary>
    public static readonly AttachedProperty<bool> InHighContrastProperty =
        AvaloniaProperty.RegisterAttached<MarkdownViewer, StyledElement, bool>("InHighContrast", inherits: true);

    /// <summary>Gets whether <paramref name="element"/> is in a viewer that is in high contrast.</summary>
    public static bool GetInHighContrast(StyledElement element) => element.GetValue(InHighContrastProperty);

    private const string HighContrastPaletteKey = "MarkdownHighContrastPalette";

    private IPlatformSettings? _platformSettings;
    private bool _platformHighContrast;

    // The theme's MarkdownHighContrastPalette resolved from this viewer, merged into its resources
    // while it is in high contrast; null otherwise.
    private ResourceDictionary? _highContrastResources;
    // Set while the viewer swaps that dictionary, which raises ResourcesChanged on the viewer itself.
    private bool _updatingHighContrastResources;

    // Where Tab goes on from while the viewer itself has the focus: the anchor the viewer last
    // jumped to, as a browser moves its starting point to a fragment's target. Cleared by a click
    // in the document, by Tab or Shift+Tab from the viewer, and when the focus leaves the viewer.
    private Control? _tabStart;

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
    /// <c>:high-contrast</c> pseudo-class and the theme's <c>MarkdownHighContrastPalette</c> in its resources.
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
        else if (change.Property == ThemeVariantScope.ActualThemeVariantProperty)
            UpdateHighContrastResources();
        else if (change.Property == IsKeyboardFocusWithinProperty && !change.GetNewValue<bool>())
            _tabStart = null;
        else if (change.Property == IsImageSaveEnabledProperty && _lightbox is { } lightbox)
            ShowLightboxSaveButton(lightbox);
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
        // Rendered while detached, the document may have missed themes defined above the viewer
        UpdateThemes();
        // A preset added or removed, or another value for a MarkdownHighContrast* key above the viewer
        ResourcesChanged += OnResourcesChanged;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Stryker disable all : headless tests can't raise the platform's ColorValuesChanged
        if (_platformSettings is not null)
            _platformSettings.ColorValuesChanged -= OnPlatformColorValuesChanged;
        _platformSettings = null;
        // Stryker restore all
        ResourcesChanged -= OnResourcesChanged;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new MarkdownViewerAutomationPeer(this);

    /// <summary>
    /// Tab order through the document: the viewer itself, then its links and focusable controls in
    /// reading order, then whatever follows the viewer. Shift+Tab walks the same stops backwards,
    /// and entering the document backwards lands on its last stop. After a jump to an anchor, Tab
    /// from the viewer goes on from that anchor instead of from the top of the document.
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
        {
            var start = _tabStart;
            _tabStart = null;
            if (start?.TranslatePoint(default, this) is not { } origin)
                return forward && stops.Count > 0 ? (true, stops[0]) : (false, null);
            // The first stop at or below the anchor; stops are in reading order.
            var next = stops.FindIndex(stop => ((Visual)stop).TranslatePoint(default, this)?.Y >= origin.Y);
            if (next < 0) next = stops.Count;
            if (forward)
                return (true, next < stops.Count ? stops[next] : null);
            return next > 0 ? (true, stops[next - 1]) : (false, null);
        }

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
        if (isHighContrast)
            SetValue(InHighContrastProperty, true);
        else
            ClearValue(InHighContrastProperty);
        UpdateHighContrastResources();
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e)
    {
        UpdateHighContrastResources();
        UpdateThemes();
    }

    /// <summary>
    /// Gives the document's controls the control themes their keys now resolve to, when a preset that
    /// defines some of its own was added or removed, without rendering the document again.
    /// </summary>
    private void UpdateThemes()
    {
        if (_themes?.Refresh() is { } changed)
            ThemeLookup.Replace(this, changed);
    }

    /// <summary>
    /// Merges the theme's high-contrast palette into the viewer's resources while it is in high contrast,
    /// so the styles' DynamicResource bindings pick up the high-contrast values: an entry whose value is a
    /// string takes the value of the resource it names, found from the viewer for its theme variant (a
    /// preset's MarkdownHighContrast* colours), and is left out when nothing defines that resource. Off,
    /// the viewer's resources are left as they are. Rebuilt when the theme variant or the resources
    /// above the viewer change; a rebuild that resolves to the same values changes nothing.
    /// </summary>
    private void UpdateHighContrastResources()
    {
        if (_updatingHighContrastResources)
            return;
        ResourceDictionary? resolved = null;
        if (IsHighContrast && this.TryFindResource(HighContrastPaletteKey, ActualThemeVariant, out var found)
            && found is IResourceDictionary palette)
        {
            resolved = [];
            foreach (var key in palette.Keys)
            {
                if (!palette.TryGetResource(key, ActualThemeVariant, out var value))
                    continue;
                if (value is string source && !this.TryFindResource(source, ActualThemeVariant, out value))
                    continue;
                resolved[key] = value;
            }
        }

        if (resolved is null && _highContrastResources is null)
            return;
        var merged = Resources.MergedDictionaries;
        // Stryker disable once Logical : only differs when Resources was replaced, which raises no ResourcesChanged to test it with
        if (_highContrastResources is { } current && merged.Contains(current) && SameValues(current, resolved))
            return;
        _updatingHighContrastResources = true;
        try
        {
            if (_highContrastResources is not null)
                merged.Remove(_highContrastResources);
            _highContrastResources = resolved;
            if (resolved is not null)
                merged.Add(resolved);
        }
        finally
        {
            _updatingHighContrastResources = false;
        }
    }

    private static bool SameValues(ResourceDictionary current, ResourceDictionary? resolved) =>
        resolved is not null && current.Count == resolved.Count
        && resolved.All(entry => current.TryGetValue(entry.Key, out var value) && ReferenceEquals(value, entry.Value));
}
