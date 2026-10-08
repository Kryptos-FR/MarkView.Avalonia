// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;

using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Tests.Shared;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class ThemeTrackingTests
{
    private sealed class Tracked
    {
        public Border Element { get; } = new();
        public List<ThemeVariant?> Applied { get; } = [];

        public Tracked() =>
            ThemeTracking.ReapplyOnThemeChange(Element, () => Applied.Add(Application.Current!.ActualThemeVariant));
    }

    [AvaloniaFact]
    public void Attaching_under_the_theme_the_content_was_built_for_does_not_reapply()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var tracked = new Tracked();
        var window = new Window { Content = tracked.Element };
        try
        {
            window.Show();

            Assert.Empty(tracked.Applied);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Theme_switch_while_attached_reapplies_for_the_new_theme()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var tracked = new Tracked();
        var window = new Window { Content = tracked.Element };
        try
        {
            window.Show();

            theme.Switch(ThemeVariant.Dark);

            Assert.Equal([ThemeVariant.Dark], tracked.Applied);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Theme_switch_on_a_never_attached_element_does_not_reapply()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var tracked = new Tracked();

        theme.Switch(ThemeVariant.Dark);

        Assert.Empty(tracked.Applied);
    }

    [AvaloniaFact]
    public void Theme_switch_while_detached_reapplies_once_on_reattach()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var tracked = new Tracked();
        var window = new Window { Content = tracked.Element };
        try
        {
            window.Show();
            window.Content = null;

            theme.Switch(ThemeVariant.Dark);
            Assert.Empty(tracked.Applied);

            window.Content = tracked.Element;
            Assert.Equal([ThemeVariant.Dark], tracked.Applied);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_back_while_detached_does_not_reapply_on_reattach()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var tracked = new Tracked();
        var window = new Window { Content = tracked.Element };
        try
        {
            window.Show();
            window.Content = null;

            theme.Switch(ThemeVariant.Dark);
            theme.Switch(ThemeVariant.Light);
            window.Content = tracked.Element;

            Assert.Empty(tracked.Applied);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Never_attached_element_is_not_kept_alive()
    {
        using var theme = new ThemeScope(ThemeVariant.Light);
        var element = TrackUnreferenced();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(element.IsAlive);
    }

    // Kept out of line so no local in the test method roots the element.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference TrackUnreferenced() => new(new Tracked().Element);
}
