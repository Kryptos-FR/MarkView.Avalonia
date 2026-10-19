// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

/// <summary>
/// <see cref="MarkdownViewer.ShowCodeBlockCopyButtonOnHover"/> hides the copy buttons until the pointer is
/// over their block, they have keyboard focus, or they confirm a copy, through the default theme only.
/// </summary>
public class CopyButtonOnHoverTests
{
    private const string TwoBlocks = """
        [before](https://example.com/b)

        ```cs
        var x = 1;
        ```

        Between

        ```
        second
        ```
        """;

    private sealed record Fixture(Window Window, MarkdownViewer Viewer) : IDisposable
    {
        public Button[] CopyButtons =>
            Viewer.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("markdown-code-copy")).ToArray();

        // Headless drawing records nothing to hit-test a border against, so a mouse move never makes it
        // :pointerover; the pseudo-class is set the way the input manager sets it, on the block and its
        // ancestors.
        public static void SetPointerOver(Control block, bool value)
        {
            foreach (var control in block.GetSelfAndVisualAncestors().OfType<Control>())
                ((IPseudoClasses)control.Classes).Set(":pointerover", value);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }

    private static Fixture Show(string markdown, Action<MarkdownViewer>? configure = null)
    {
        var viewer = new MarkdownViewer();
        configure?.Invoke(viewer);
        viewer.Markdown = markdown;
        var window = new Window { Width = 400, Height = 600, Content = viewer };
        window.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml"),
        });
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Fixture(window, viewer);
    }

    [AvaloniaFact]
    public void Copy_buttons_always_show_by_default()
    {
        using var f = Show(TwoBlocks);

        Assert.False(f.Viewer.ShowCodeBlockCopyButtonOnHover);
        Assert.DoesNotContain(":copy-on-hover", f.Viewer.Classes);
        Assert.All(f.CopyButtons, b => Assert.Equal(1, b.Opacity));
    }

    [AvaloniaFact]
    public void Turning_it_on_hides_the_copy_buttons_without_re_rendering_the_document()
    {
        using var f = Show(TwoBlocks);
        var content = f.Viewer.Content;
        var buttons = f.CopyButtons;

        f.Viewer.ShowCodeBlockCopyButtonOnHover = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(":copy-on-hover", f.Viewer.Classes);
        Assert.Same(content, f.Viewer.Content);
        Assert.Equal(buttons, f.CopyButtons);
        // Hidden, but still in the layout and the Tab order.
        Assert.All(buttons, b =>
        {
            Assert.Equal(0, b.Opacity);
            Assert.True(b.IsVisible);
            Assert.True(b.Bounds.Width > 0);
        });

        f.Viewer.ShowCodeBlockCopyButtonOnHover = false;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(":copy-on-hover", f.Viewer.Classes);
        Assert.All(buttons, b => Assert.Equal(1, b.Opacity));
    }

    [AvaloniaFact]
    public void Pointer_over_a_code_block_shows_its_copy_button_only()
    {
        using var f = Show(TwoBlocks, v => v.ShowCodeBlockCopyButtonOnHover = true);
        var code = f.Viewer.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("markdown-code-block"));

        Fixture.SetPointerOver(code, true);
        Assert.Equal([1d, 0d], f.CopyButtons.Select(b => b.Opacity));

        Fixture.SetPointerOver(code, false);
        Assert.Equal([0d, 0d], f.CopyButtons.Select(b => b.Opacity));
    }

    [AvaloniaFact]
    public void Keyboard_focus_shows_the_copy_button()
    {
        using var f = Show(TwoBlocks, v => v.ShowCodeBlockCopyButtonOnHover = true);
        f.Viewer.Focus();

        // The link, then the first copy button.
        for (var i = 0; i < 2; i++)
        {
            f.Window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Same(f.CopyButtons[0], f.Window.FocusManager?.GetFocusedElement());
        Assert.Equal([1d, 0d], f.CopyButtons.Select(b => b.Opacity));
    }

    [AvaloniaFact]
    public void The_copied_confirmation_stays_visible_after_the_pointer_leaves()
    {
        using var f = Show(TwoBlocks, v => v.ShowCodeBlockCopyButtonOnHover = true);
        var button = f.CopyButtons[1];

        button.Classes.Add("copied");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, button.Opacity);
    }

    [AvaloniaFact]
    public void It_can_be_turned_on_from_a_style()
    {
        using var f = Show(TwoBlocks);
        f.Window.Styles.Add(new Style(x => x.OfType<MarkdownViewer>())
        {
            Setters = { new Setter(MarkdownViewer.ShowCodeBlockCopyButtonOnHoverProperty, true) },
        });
        Dispatcher.UIThread.RunJobs();

        Assert.True(f.Viewer.ShowCodeBlockCopyButtonOnHover);
        Assert.Contains(":copy-on-hover", f.Viewer.Classes);
        Assert.All(f.CopyButtons, b => Assert.Equal(0, b.Opacity));
    }
}
