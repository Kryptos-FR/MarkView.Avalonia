// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

using Microsoft.Win32;

namespace MarkView.Avalonia;

/// <summary>
/// The operating system's text scale, the factor by which the user asked for larger (or smaller) text, which
/// <see cref="MarkdownViewer"/> applies to its font sizes while <see cref="MarkdownViewer.FollowSystemTextScale"/>
/// is <c>true</c>. Avalonia doesn't expose this setting, so it is read here where the platform allows it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Windows: Settings → Accessibility → Text size (1 to 2.25). Read again each time a window
/// showing a viewer is activated, e.g. when the user comes back from the Settings app.</description></item>
/// <item><description>Browser: the browser's default font size, relative to 16px (Chrome: Settings → Appearance →
/// Font size), read at startup. Page zoom is not included: Avalonia already applies it as render scaling.</description></item>
/// <item><description>Android, iOS, Linux: not read; 1 unless the application sets <see cref="Factor"/> from the
/// platform's own API (see docs/accessibility.md).</description></item>
/// <item><description>macOS has no system-wide text size; 1.</description></item>
/// </list>
/// </remarks>
public static partial class SystemTextScale
{
    private static double? _factor;
    private static bool _isSetByApplication;

    /// <summary>
    /// Gets or sets the system text scale. Setting it (for instance from Android's <c>Configuration.FontScale</c>)
    /// replaces the value read from the platform from then on, and updates every viewer that follows it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a positive finite number.</exception>
    public static double Factor
    {
        get => _factor ??= Detect();
        set
        {
            if (!(value > 0) || !double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The text scale must be a positive finite number.");
            _isSetByApplication = true;
            Update(value);
        }
    }

    /// <summary>Raised on the UI thread when <see cref="Factor"/> changes.</summary>
    public static event EventHandler? Changed;

    /// <summary>
    /// Reads the platform's text scale again, unless the application has set <see cref="Factor"/>, and raises
    /// <see cref="Changed"/> if it differs.
    /// </summary>
    public static void Refresh()
    {
        if (!_isSetByApplication)
            Update(Detect());
    }

    /// <summary>Forgets the value set by the application and the value read so far, for tests.</summary>
    internal static void Reset()
    {
        _isSetByApplication = false;
        _factor = null;
    }

    /// <summary>The platform reader, replaceable by tests.</summary>
    internal static Func<double> Detect { get; set; } = DetectFromPlatform;

    private static void Update(double factor)
    {
        if (_factor == factor) return;
        _factor = factor;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    // Stryker disable all : the platform readers run on Windows and in a browser only
    private static double DetectFromPlatform()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return ReadWindowsTextScale();
            if (OperatingSystem.IsBrowser())
                return ReadBrowserTextScale();
        }
        catch (Exception)
        {
            // An unreadable setting is no reason to break rendering: keep the text as designed.
        }
        return 1;
    }

    [SupportedOSPlatform("windows")]
    private static double ReadWindowsTextScale()
    {
        // The value behind Settings → Accessibility → Text size, also exposed by WinRT's UISettings.TextScaleFactor:
        // a percentage from 100 to 225, absent until the user changes it.
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility");
        return key?.GetValue("TextScaleFactor") is int percent and >= 100 and <= 225 ? percent / 100.0 : 1;
    }

    [SupportedOSPlatform("browser")]
    private static double ReadBrowserTextScale()
    {
        // The root element's computed size is the browser's default font size unless the page's own CSS changes it.
        using var document = JSHost.GlobalThis.GetPropertyAsJSObject("document");
        using var root = document?.GetPropertyAsJSObject("documentElement");
        if (root is null) return 1;
        using var style = GetComputedStyle(root);
        return double.TryParse(style.GetPropertyAsString("fontSize")?.Replace("px", "", StringComparison.Ordinal),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var px) && px > 0
            ? px / 16
            : 1;
    }

    [JSImport("globalThis.getComputedStyle")]
    [SupportedOSPlatform("browser")]
    private static partial JSObject GetComputedStyle(JSObject element);
    // Stryker restore all
}
