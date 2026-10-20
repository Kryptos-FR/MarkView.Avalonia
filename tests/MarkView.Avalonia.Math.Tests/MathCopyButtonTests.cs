// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using MarkView.Avalonia.Rendering;

using Xunit;

namespace MarkView.Avalonia.Math.Tests;

/// <summary>
/// Display formulas get a copy button for their TeX source, in their top right corner, under the same
/// switch as the code blocks' copy button.
/// </summary>
public class MathCopyButtonTests
{
    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public Button CopyButton =>
            Viewer.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("markdown-math-copy"));

        public void Click()
        {
            CopyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown, Action<MarkdownViewer>? configure = null)
    {
        var viewer = new MarkdownViewer().UseMath();
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 300, Content = viewer };
        window.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml"),
        });
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Fixture(window, viewer);
    }

    private static Control Block(MarkdownViewer viewer, int index = 0) =>
        Assert.IsType<StackPanel>(Assert.IsType<Grid>(viewer.Content).Children[0]).Children[index];

    [AvaloniaFact]
    public void Formula_is_written_under_a_copy_button_in_its_top_right_corner()
    {
        using var f = Show("$$\nx^2\n$$");

        var container = Assert.IsType<Grid>(Block(f.Viewer));
        Assert.Contains("markdown-math-container", container.Classes);
        var border = MathBlockAssert.MathBorder(container);
        Assert.Contains("markdown-math-block", border.Classes);

        var button = f.CopyButton;
        Assert.Same(container, button.GetVisualParent());
        Assert.Contains("markdown-code-copy", button.Classes);
        Assert.Equal(HorizontalAlignment.Right, button.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Top, button.VerticalAlignment);
        // The formula keeps clear of the button on both sides, so it stays centred.
        Assert.Equal(new Thickness(32, 0), border.Margin);
        Assert.True(button.Bounds.Width + button.Margin.Right <= border.Margin.Right,
            $"{button.Bounds.Width} + {button.Margin.Right} > {border.Margin.Right}");
    }

    [AvaloniaTheory]
    [InlineData("GitHubTheme")]
    [InlineData("ChatTheme")]
    [InlineData("DocsTheme")]
    public void Formula_keeps_clear_of_the_copy_button_in_a_preset(string preset)
    {
        using var f = Show("$$\nx^2\n$$\n\n```\ncode\n```");
        f.Window.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri($"avares://MarkView.Avalonia/Themes/{preset}.axaml"),
        });
        Dispatcher.UIThread.RunJobs();
        f.Window.UpdateLayout();

        var border = MathBlockAssert.MathBorder(Assert.IsType<Grid>(Block(f.Viewer)));
        var button = f.CopyButton;
        Assert.Equal(border.Margin.Left, border.Margin.Right);
        Assert.True(button.Bounds.Width + button.Margin.Right <= border.Margin.Right,
            $"{button.Bounds.Width} + {button.Margin.Right} > {border.Margin.Right}");
        // Lined up with the copy button of a code block
        var header = f.Viewer.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("markdown-code-header"));
        Assert.Equal(header.Padding.Right, button.Margin.Right);
    }

    [AvaloniaFact]
    public void Default_theme_names_the_button_after_the_formula_and_confirms_a_copy()
    {
        using var f = Show("$$\nx^2\n$$");
        var button = f.CopyButton;
        var icon = Assert.IsAssignableFrom<PathIcon>(button.Content);
        var copyIcon = icon.Data;

        Assert.Equal("Copy formula", AutomationProperties.GetName(button));
        Assert.Equal("Copy formula", ToolTip.GetTip(button));
        Assert.NotNull(copyIcon);

        button.Classes.Add("copied");
        Assert.Equal("Copied", ToolTip.GetTip(button));
        Assert.NotSame(copyIcon, icon.Data);
    }

    [AvaloniaFact]
    public void Formula_has_no_copy_button_when_the_code_block_copy_button_is_off()
    {
        using var f = Show("$$\nx^2\n$$", viewer => viewer.ShowCodeBlockCopyButton = false);

        var border = Assert.IsType<Border>(Block(f.Viewer));
        Assert.Contains("markdown-math-block", border.Classes);
        Assert.Empty(f.Viewer.GetVisualDescendants().OfType<Button>());
    }

    [AvaloniaFact]
    public void Turning_the_copy_button_off_re_renders_the_formula_without_it()
    {
        using var f = Show("$$\nx^2\n$$");
        Assert.IsType<Grid>(Block(f.Viewer));

        f.Viewer.ShowCodeBlockCopyButton = false;

        Assert.IsType<Border>(Block(f.Viewer));
    }

    [AvaloniaTheory]
    [InlineData("$$\na + b\n= c\n$$")]
    [InlineData("\\[\na + b\n= c\n\\]")]
    public async Task Copy_button_copies_the_TeX_source_without_its_delimiters(string markdown)
    {
        using var f = Show(markdown);
        var raised = new List<(MarkdownCopyOrigin, string)>();
        f.Viewer.CopyingToClipboard += (_, e) => raised.Add((e.Origin, e.Text));

        f.Click();

        Assert.Equal([(MarkdownCopyOrigin.MathBlock, "a + b\n= c")], raised);
        Assert.Equal("a + b\n= c".ReplaceLineEndings(), await f.Window.Clipboard!.TryGetTextAsync());
        Assert.Contains("copied", f.CopyButton.Classes);
    }

    [AvaloniaFact]
    public async Task A_cancelled_formula_copy_writes_nothing_and_shows_no_confirmation()
    {
        using var f = Show("$$\nx^2\n$$");
        await f.Window.Clipboard!.SetTextAsync("keep");
        f.Viewer.CopyingToClipboard += (_, e) => e.Cancel = true;

        f.Click();

        Assert.Equal("keep", await f.Window.Clipboard!.TryGetTextAsync());
        Assert.DoesNotContain("copied", f.CopyButton.Classes);
    }

    [AvaloniaFact]
    public async Task A_formula_that_fails_to_render_still_copies_its_source()
    {
        var nested = new string('{', 60) + "x" + new string('}', 60);
        using var f = Show($"$$\n{nested}\n$$");
        Assert.Contains("markdown-math-fallback", MathBlockAssert.MathBorder(Block(f.Viewer)).Classes);

        f.Click();

        Assert.Equal(nested, await f.Window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public void Selecting_everything_copies_the_formula_but_not_its_button()
    {
        using var f = Show("Intro\n\n$$\nx^2\n$$\n\nOutro");

        f.Viewer.SelectAll();

        Assert.Equal("Intro\n$$\nx^2\n$$\nOutro", f.Viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Copy_button_is_a_tab_stop_between_the_links_around_it()
    {
        using var f = Show("[before](https://example.com/b)\n\n$$\nx^2\n$$\n\n[after](https://example.com/a)");
        f.Viewer.Focus();

        var visited = new List<string?>();
        for (var i = 0; i < 3; i++)
        {
            f.Window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
            var focused = f.Window.FocusManager?.GetFocusedElement();
            visited.Add(focused switch
            {
                LinkFocusTarget link => link.AccessibleName,
                Control control => AutomationProperties.GetName(control),
                _ => null,
            });
        }

        Assert.Equal(["before", "Copy formula", "after"], visited);
    }

    [AvaloniaFact]
    public void With_hover_only_copy_buttons_the_button_shows_while_the_pointer_is_over_the_formula()
    {
        using var f = Show("$$\nx^2\n$$", viewer => viewer.ShowCodeBlockCopyButtonOnHover = true);
        var button = f.CopyButton;
        Assert.Equal(0, button.Opacity);

        // Headless drawing records nothing to hit-test the formula against, so the pseudo-class is set
        // the way the input manager would, on the formula and its ancestors.
        var formula = MathBlockAssert.MathBorder(Block(f.Viewer));
        SetPointerOver(formula, true);
        Assert.Equal(1, button.Opacity);

        SetPointerOver(formula, false);
        Assert.Equal(0, button.Opacity);

        // The whole row takes the pointer, also the empty space between the formula and the button.
        Assert.Same(Brushes.Transparent, formula.FindAncestorOfType<Grid>()!.Background);

        static void SetPointerOver(Control block, bool value)
        {
            foreach (var control in block.GetSelfAndVisualAncestors().OfType<Control>())
                ((IPseudoClasses)control.Classes).Set(":pointerover", value);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
