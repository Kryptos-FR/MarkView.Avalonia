// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Markdig;
using Markdig.Syntax;
using MarkView.Avalonia.Rendering;
using MarkView.Avalonia.Rendering.Blocks;
using MarkView.Avalonia.Tests.Shared;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Xunit;

namespace MarkView.Avalonia.Tests.Blocks;

public class CodeBlockToolbarTests
{
    private static StackPanel Render(string markdown, bool showLanguage = true, bool showCopyButton = true)
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var renderer = new AvaloniaRenderer
        {
            ShowCodeBlockLanguage = showLanguage,
            ShowCodeBlockCopyButton = showCopyButton,
        };
        pipeline.Setup(renderer);
        renderer.Render(Markdig.Markdown.Parse(markdown, pipeline));
        return renderer.RootPanel;
    }

    private static (StackPanel Container, Grid Toolbar, Border Code) Wrapped(StackPanel root)
    {
        var container = Assert.IsType<StackPanel>(Assert.Single(root.Children));
        Assert.Contains("markdown-code-container", container.Classes);
        Assert.Equal(2, container.Children.Count);

        var header = Assert.IsType<Border>(container.Children[0]);
        Assert.Contains("markdown-code-header", header.Classes);
        var code = Assert.IsType<Border>(container.Children[1]);
        Assert.Contains("markdown-code-block", code.Classes);
        Assert.IsType<TextBlock>(code.Child);

        return (container, Assert.IsType<Grid>(header.Child), code);
    }

    private static TextBlock? Language(Grid toolbar) =>
        toolbar.Children.OfType<TextBlock>().SingleOrDefault(tb => tb.Classes.Contains("markdown-code-language"));

    private static Button? CopyButton(Grid toolbar) =>
        toolbar.Children.OfType<Button>().SingleOrDefault(b => b.Classes.Contains("markdown-code-copy"));

    [AvaloniaFact]
    public void Renderer_shows_both_by_default()
    {
        var renderer = new AvaloniaRenderer();
        Assert.True(renderer.ShowCodeBlockLanguage);
        Assert.True(renderer.ShowCodeBlockCopyButton);
    }

    [AvaloniaFact]
    public void Fenced_block_with_language_gets_a_toolbar_with_label_and_copy_button()
    {
        var (_, toolbar, code) = Wrapped(Render("```csharp\nvar x = 1;\n```"));

        var language = Assert.IsType<TextBlock>(Language(toolbar));
        Assert.Equal("csharp", language.Text);
        Assert.Equal(0, Grid.GetColumn(language));

        var button = Assert.IsType<Button>(CopyButton(toolbar));
        Assert.Equal(1, Grid.GetColumn(button));
        Assert.IsType<PathIcon>(button.Content);

        Assert.Contains("language-csharp", code.Classes);
    }

    [AvaloniaFact]
    public void Block_without_language_gets_only_the_copy_button()
    {
        var (_, toolbar, _) = Wrapped(Render("    indented code"));

        Assert.Null(Language(toolbar));
        Assert.NotNull(CopyButton(toolbar));
    }

    [AvaloniaFact]
    public void Language_only_toolbar_has_no_copy_button()
    {
        var (_, toolbar, _) = Wrapped(Render("```csharp\nvar x = 1;\n```", showCopyButton: false));

        Assert.Equal("csharp", Language(toolbar)?.Text);
        Assert.Null(CopyButton(toolbar));
    }

    [AvaloniaFact]
    public void Copy_button_only_toolbar_has_no_language()
    {
        var (_, toolbar, _) = Wrapped(Render("```csharp\nvar x = 1;\n```", showLanguage: false));

        Assert.Null(Language(toolbar));
        Assert.NotNull(CopyButton(toolbar));
    }

    [AvaloniaTheory]
    [InlineData("```csharp\nvar x = 1;\n```", false, false)]
    [InlineData("    indented code", true, false)]
    public void Empty_toolbar_leaves_the_code_block_unwrapped(string markdown, bool showLanguage, bool showCopyButton)
    {
        var root = Render(markdown, showLanguage, showCopyButton);

        var border = Assert.IsType<Border>(Assert.Single(root.Children));
        Assert.Contains("markdown-code-block", border.Classes);
    }

    [AvaloniaFact]
    public void GetCode_joins_source_lines_with_line_feeds()
    {
        var document = Markdig.Markdown.Parse("```\nfirst\n  second\n\nlast\n```");
        var block = Assert.IsType<FencedCodeBlock>(Assert.Single(document));

        Assert.Equal("first\n  second\n\nlast", CodeBlockToolbar.GetCode(block));
    }

    [AvaloniaFact]
    public void GetCode_of_an_empty_block_is_empty()
    {
        var document = Markdig.Markdown.Parse("```\n```");
        var block = Assert.IsType<FencedCodeBlock>(Assert.Single(document));

        Assert.Equal(string.Empty, CodeBlockToolbar.GetCode(block));
    }

    [AvaloniaFact]
    public async Task Copy_button_copies_the_code_and_flags_itself_as_copied_for_a_while()
    {
        var root = Render("```csharp\nvar x = 1;\nvar y = 2;\n```");
        var window = new Window { Content = root };
        window.Show();
        try
        {
            var button = Assert.IsType<Button>(CopyButton(Wrapped(root).Toolbar));
            var click = new RoutedEventArgs(Button.ClickEvent);
            button.RaiseEvent(click);
            Dispatcher.UIThread.RunJobs();

            Assert.True(click.Handled);
            Assert.Equal("var x = 1;\nvar y = 2;", await window.Clipboard!.TryGetTextAsync());
            Assert.Contains("copied", button.Classes);

            await AsyncTestHelpers.WaitUntilAsync(() => !button.Classes.Contains("copied"),
                (int)(CodeBlockToolbar.CopiedFeedbackDuration + TimeSpan.FromSeconds(5)).TotalMilliseconds);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Copying_again_restarts_the_copied_feedback()
    {
        var root = Render("```\ncode\n```");
        var window = new Window { Content = root };
        window.Show();
        try
        {
            var button = Assert.IsType<Button>(CopyButton(Wrapped(root).Toolbar));
            var clock = Stopwatch.StartNew();
            TimeSpan? removedAt = null;
            button.Classes.CollectionChanged += (_, _) =>
            {
                if (!button.Classes.Contains("copied"))
                    removedAt ??= clock.Elapsed;
            };

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("copied", button.Classes);

            // Copy again halfway through the feedback: the first copy's timer must not cut the
            // second one short.
            await Task.Delay(CodeBlockToolbar.CopiedFeedbackDuration / 2);
            Assert.Null(removedAt);
            var secondCopy = clock.Elapsed;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("copied", button.Classes);

            await AsyncTestHelpers.WaitUntilAsync(() => removedAt != null,
                (int)(CodeBlockToolbar.CopiedFeedbackDuration * 2 + TimeSpan.FromSeconds(5)).TotalMilliseconds);
            // Small tolerance for the dispatcher's millisecond clock.
            Assert.True(removedAt >= secondCopy + CodeBlockToolbar.CopiedFeedbackDuration - TimeSpan.FromMilliseconds(50),
                $"'copied' was removed at {removedAt}, before the second copy's {CodeBlockToolbar.CopiedFeedbackDuration} " +
                $"(second copy at {secondCopy})");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copy_button_shows_no_confirmation_when_the_clipboard_refuses_the_code()
    {
        var root = Render("```\ncode\n```");
        var (window, clipboard) = RefusingClipboardProxy.CreateWindow();
        window.Content = root;
        window.Show();
        try
        {
            var button = Assert.IsType<Button>(CopyButton(Wrapped(root).Toolbar));
            var click = new RoutedEventArgs(Button.ClickEvent);
            button.RaiseEvent(click);
            Dispatcher.UIThread.RunJobs();

            Assert.True(click.Handled);
            Assert.True(clipboard.Refused);
            Assert.DoesNotContain("copied", button.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copy_button_outside_a_window_does_nothing()
    {
        var root = Render("```\ncode\n```");
        var button = Assert.IsType<Button>(CopyButton(Wrapped(root).Toolbar));

        var click = new RoutedEventArgs(Button.ClickEvent);
        button.RaiseEvent(click);
        Dispatcher.UIThread.RunJobs();

        Assert.True(click.Handled);
        Assert.DoesNotContain("copied", button.Classes);
    }

    [AvaloniaFact]
    public void Viewer_shows_both_by_default_and_rerenders_when_either_changes()
    {
        var viewer = new MarkdownViewer { Markdown = "```csharp\nvar x = 1;\n```" };
        Assert.True(viewer.ShowCodeBlockLanguage);
        Assert.True(viewer.ShowCodeBlockCopyButton);
        Assert.Single(viewer.GetLogicalDescendants().OfType<Button>());

        viewer.ShowCodeBlockCopyButton = false;
        Assert.Empty(viewer.GetLogicalDescendants().OfType<Button>());
        Assert.Single(viewer.GetLogicalDescendants().OfType<TextBlock>(), tb => tb.Classes.Contains("markdown-code-language"));

        viewer.ShowCodeBlockLanguage = false;
        Assert.DoesNotContain(viewer.GetLogicalDescendants().OfType<Border>(), b => b.Classes.Contains("markdown-code-header"));
    }

    [AvaloniaFact]
    public void Viewer_toolbar_can_be_turned_off_from_a_style()
    {
        var window = new Window();
        window.Styles.Add(new Style(x => x.OfType<MarkdownViewer>())
        {
            Setters =
            {
                new Setter(MarkdownViewer.ShowCodeBlockLanguageProperty, false),
                new Setter(MarkdownViewer.ShowCodeBlockCopyButtonProperty, false),
            },
        });
        var viewer = new MarkdownViewer { Markdown = "```csharp\nvar x = 1;\n```" };
        window.Content = viewer;
        window.Show();
        try
        {
            Assert.DoesNotContain(viewer.GetLogicalDescendants().OfType<Border>(), b => b.Classes.Contains("markdown-code-header"));
            Assert.Contains(viewer.GetLogicalDescendants().OfType<Border>(), b => b.Classes.Contains("markdown-code-block"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_everything_copies_the_code_but_not_the_toolbar()
    {
        var viewer = new MarkdownViewer { Markdown = "Intro\n\n```csharp\nvar x = 1;\n```\n\nOutro" };

        viewer.SelectAll();

        Assert.Equal("Intro\nvar x = 1;\nOutro", viewer.GetSelectedText());
    }

    [AvaloniaFact]
    public void Default_theme_gives_the_copy_button_an_icon_and_a_name_that_follow_the_copied_class()
    {
        var root = Render("```\ncode\n```");
        var window = new Window { Content = root };
        window.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://MarkView.Avalonia/Themes/MarkdownTheme.axaml"),
        });
        window.Show();
        try
        {
            var button = Assert.IsType<Button>(CopyButton(Wrapped(root).Toolbar));
            var icon = Assert.IsType<PathIcon>(button.Content);
            Assert.Equal("Copy code", AutomationProperties.GetName(button));
            var copyIcon = icon.Data;
            Assert.NotNull(copyIcon);

            button.Classes.Add("copied");
            Assert.NotNull(icon.Data);
            Assert.NotSame(copyIcon, icon.Data);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A clipboard that refuses every write, the way a browser can, plugged into a headless window
    /// by forwarding everything else of the window's platform implementation.
    /// </summary>
    public class RefusingClipboardProxy : DispatchProxy
    {
        public bool Refused { get; private set; }

        public static (Window Window, RefusingClipboardProxy Clipboard) CreateWindow()
        {
            var clipboard = Create<IClipboard, RefusingClipboardProxy>();
            var impl = Create<IWindowImpl, ClipboardOverridingWindowImplProxy>();
            var overriding = (ClipboardOverridingWindowImplProxy)(object)impl;
            overriding.Inner = (IWindowImpl)PlatformManager.CreateWindow();
            overriding.Clipboard = clipboard;
            return (new Window(impl), (RefusingClipboardProxy)(object)clipboard);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IClipboard.SetDataAsync))
                throw new NotSupportedException(targetMethod?.Name);
            Refused = true;
            return Task.FromException(new UnauthorizedAccessException("Clipboard access refused"));
        }
    }

    public class ClipboardOverridingWindowImplProxy : DispatchProxy
    {
        public IWindowImpl Inner { get; set; } = null!;
        public IClipboard Clipboard { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == "TryGetFeature" && Equals(args![0], typeof(IClipboard)))
                return Clipboard;
            try
            {
                return targetMethod.Invoke(Inner, args);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                ExceptionDispatchInfo.Throw(e.InnerException);
                throw;
            }
        }
    }
}
