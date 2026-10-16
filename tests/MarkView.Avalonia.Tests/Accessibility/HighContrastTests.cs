// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MarkView.Avalonia.Rendering.Inlines;
using MarkView.Avalonia.Tests.Shared;
using Xunit;

namespace MarkView.Avalonia.Tests.Accessibility;

public class HighContrastTests
{
    private static StyleInclude MarkdownThemeInclude() => new((Uri?)null)
    {
        Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml"),
    };

    // Only tests of the theme's colours load it: Stryker compiles the mutated assembly without
    // Avalonia's XAML compiler, so loading the theme throws there and hides what a test covers.
    private static MarkdownViewer ShowViewer(string markdown, HighContrastMode mode = HighContrastMode.System,
        bool withTheme = false)
    {
        var viewer = new MarkdownViewer { HighContrastMode = mode, Markdown = markdown };
        if (withTheme)
            viewer.Styles.Add(MarkdownThemeInclude());
        new Window { Width = 400, Height = 300, Content = viewer }.Show();
        return viewer;
    }

    private static Color LinkColor(MarkdownViewer viewer)
    {
        var link = viewer.GetVisualDescendants().OfType<TextBlock>()
            .SelectMany(t => t.Inlines!).OfType<MarkdownHyperlink>().Single();
        return Assert.IsAssignableFrom<ISolidColorBrush>(link.Foreground).Color;
    }

    [AvaloniaFact]
    public void Viewer_follows_the_system_by_default_and_headless_reports_no_preference()
    {
        var viewer = ShowViewer("Text");

        Assert.Equal(HighContrastMode.System, viewer.HighContrastMode);
        Assert.False(viewer.IsHighContrast);
        Assert.DoesNotContain(":high-contrast", viewer.Classes);
    }

    [AvaloniaFact]
    public void Enabled_and_Disabled_override_the_system_and_toggle_the_pseudo_class()
    {
        var viewer = ShowViewer("Text", HighContrastMode.Enabled);
        Assert.True(viewer.IsHighContrast);
        Assert.Contains(":high-contrast", viewer.Classes);

        viewer.HighContrastMode = HighContrastMode.Disabled;
        Assert.False(viewer.IsHighContrast);
        Assert.DoesNotContain(":high-contrast", viewer.Classes);
    }

    [AvaloniaFact]
    public void System_mode_tracks_the_platform_contrast_preference()
    {
        var viewer = ShowViewer("Text");

        viewer.OnPlatformColorValuesChanged(null, new PlatformColorValues { ContrastPreference = ColorContrastPreference.High });
        Assert.True(viewer.IsHighContrast);
        Assert.Contains(":high-contrast", viewer.Classes);

        viewer.OnPlatformColorValuesChanged(null, new PlatformColorValues { ContrastPreference = ColorContrastPreference.NoPreference });
        Assert.False(viewer.IsHighContrast);

        // An explicit mode wins over the platform until it's set back to System.
        viewer.OnPlatformColorValuesChanged(null, new PlatformColorValues { ContrastPreference = ColorContrastPreference.High });
        viewer.HighContrastMode = HighContrastMode.Disabled;
        Assert.False(viewer.IsHighContrast);
        viewer.HighContrastMode = HighContrastMode.System;
        Assert.True(viewer.IsHighContrast);
    }

    [AvaloniaTheory]
    [InlineData("Light", "SystemAccentColorDark2")]
    [InlineData("Dark", "SystemAccentColorLight2")]
    public void Link_uses_an_accent_shade_with_enough_contrast(string variant, string accentKey)
    {
        var theme = variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        using var _ = new ThemeScope(theme);
        var viewer = ShowViewer("[link](https://example.com)", withTheme: true);

        Assert.True(Application.Current!.TryFindResource(accentKey, theme, out var accent));
        Assert.Equal((Color)accent!, LinkColor(viewer));
    }

    [AvaloniaTheory]
    [InlineData("Light", "#00009F", "#FFFFFF", "#000000")]
    [InlineData("Dark", "#FFFF00", "#000000", "#FFFFFF")]
    public void High_contrast_palette_replaces_theme_colours(string variant, string link, string background, string foreground)
    {
        using var _ = new ThemeScope(variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark);
        var viewer = ShowViewer("[link](https://example.com)\n\n> [!TIP]\n> Body", HighContrastMode.Enabled, withTheme: true);

        Assert.Equal(Color.Parse(link), LinkColor(viewer));
        Assert.Equal(Color.Parse(background), ((ISolidColorBrush)viewer.Background!).Color);
        Assert.Equal(Color.Parse(foreground), ((ISolidColorBrush)viewer.Foreground!).Color);

        var alert = viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-alert"));
        Assert.Equal(Color.Parse(foreground), ((ISolidColorBrush)alert.BorderBrush!).Color);
        var title = viewer.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-alert-header"));
        Assert.Equal(Color.Parse(foreground), ((ISolidColorBrush)title.Foreground!).Color);

        viewer.HighContrastMode = HighContrastMode.Disabled;
        Assert.NotEqual(Color.Parse(link), LinkColor(viewer));
    }

    [AvaloniaFact]
    public void Viewer_is_a_document_named_by_its_first_heading()
    {
        var viewer = ShowViewer("Intro\n\n# Release notes\n\n## Fixes");
        var peer = ControlAutomationPeer.CreatePeerForElement(viewer);

        Assert.Equal(AutomationControlType.Document, peer.GetAutomationControlType());
        Assert.True(peer.IsControlElement());
        Assert.True(peer.IsContentElement());
        Assert.Equal("Release notes", peer.GetName());

        AutomationProperties.SetName(viewer, "Help");
        Assert.Equal("Help", peer.GetName());
    }

    [AvaloniaFact]
    public void Viewer_without_headings_has_no_name()
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(ShowViewer("Just text"));
        Assert.Equal(string.Empty, peer.GetName());
    }
}
