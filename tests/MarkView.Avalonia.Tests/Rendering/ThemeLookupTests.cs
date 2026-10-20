// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Tests.Rendering;

public class ThemeLookupTests
{
    private static ControlTheme Theme(double fontSize) => new(typeof(TextBlock))
    {
        Setters = { new Setter(TextBlock.FontSizeProperty, fontSize) },
    };

    [AvaloniaFact]
    public void Get_returns_the_resource_and_looks_each_key_up_once()
    {
        var theme = Theme(20);
        var lookups = 0;
        var themes = new ThemeLookup(key => { lookups++; return key == "A" ? theme : null; });

        Assert.Same(theme, themes.Get("A"));
        Assert.Same(theme, themes.Get("A"));
        Assert.Equal(1, lookups);
    }

    [AvaloniaFact]
    public void A_missing_key_gets_an_empty_theme_of_its_own()
    {
        var themes = new ThemeLookup(_ => null);

        var missing = themes.Get("A");

        Assert.Empty(missing.Setters);
        Assert.Same(missing, themes.Get("A"));
        Assert.NotSame(missing, themes.Get("B"));
    }

    [AvaloniaFact]
    public void A_resource_that_is_not_a_control_theme_counts_as_missing()
    {
        var themes = new ThemeLookup(_ => "not a theme");

        Assert.Empty(themes.Get("A").Setters);
    }

    [AvaloniaFact]
    public void Refresh_reports_nothing_when_no_theme_changed()
    {
        var theme = Theme(20);
        var themes = new ThemeLookup(key => key == "A" ? theme : null);
        themes.Get("A");
        themes.Get("B");

        Assert.Null(themes.Refresh());
    }

    [AvaloniaFact]
    public void Refresh_swaps_in_a_theme_defined_or_redefined_since_and_a_placeholder_for_a_removed_one()
    {
        var first = Theme(20);
        var second = Theme(30);
        var resources = new Dictionary<string, ControlTheme> { ["A"] = first };
        var themes = new ThemeLookup(key => resources.GetValueOrDefault(key));
        var placeholder = themes.Get("B");
        themes.Get("A");

        resources["A"] = second;
        resources["B"] = first;
        var changed = themes.Refresh();

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);
        Assert.Same(second, changed[first]);
        Assert.Same(first, changed[placeholder]);
        Assert.Same(second, themes.Get("A"));
        Assert.Same(first, themes.Get("B"));

        resources.Remove("B");
        changed = themes.Refresh();

        Assert.NotNull(changed);
        var removed = Assert.Single(changed);
        Assert.Same(first, removed.Key);
        Assert.Empty(removed.Value.Setters);
        Assert.Same(removed.Value, themes.Get("B"));
        Assert.Null(themes.Refresh());
    }

    [AvaloniaFact]
    public void Replace_restyles_the_controls_that_had_a_changed_theme_at_once()
    {
        var oldTheme = Theme(20);
        var newTheme = Theme(30);
        var untouched = Theme(40);
        var changed = new TextBlock { Theme = oldTheme };
        var other = new TextBlock { Theme = untouched };
        var root = new StackPanel { Children = { new Border { Child = changed }, other } };
        var window = new Window { Content = root };
        window.Show();
        Assert.Equal(20, changed.FontSize);

        ThemeLookup.Replace(root, new Dictionary<ControlTheme, ControlTheme> { [oldTheme] = newTheme });

        Assert.Same(newTheme, changed.Theme);
        Assert.Equal(30, changed.FontSize);
        Assert.Same(untouched, other.Theme);
        window.Close();
    }

    [AvaloniaFact]
    public void A_viewer_swaps_the_themes_of_its_document_when_a_preset_is_added_and_removed()
    {
        var viewer = new MarkdownViewer { Markdown = "# Title" };
        viewer.Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml"),
        });
        var window = new Window { Content = viewer };
        window.Show();
        var heading = viewer.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("markdown-h1"));
        var coreTheme = heading.Theme;

        var preset = new Styles { Resources = { ["MarkdownHeading1Theme"] = Theme(50) } };
        viewer.Styles.Add(preset);

        Assert.Equal(50, heading.FontSize);

        viewer.Styles.Remove(preset);

        Assert.Same(coreTheme, heading.Theme);
        Assert.NotEqual(50, heading.FontSize);
        window.Close();
    }
}
