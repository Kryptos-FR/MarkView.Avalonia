// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Writes a code block, preceded by a toolbar with its language and a copy button when the
/// renderer enables them, and lays its code out with the renderer's line-number, wrapping and
/// height options. Shared by every code block renderer, including those of the extension packages.
/// </summary>
internal static class CodeBlockToolbar
{
    /// <summary>How long the copy button keeps its <c>copied</c> class after a copy.</summary>
    internal static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Writes <paramref name="border"/> (the <c>markdown-code-block</c>) as is when the toolbar
    /// would be empty, or inside a <c>markdown-code-container</c> panel below a
    /// <c>markdown-code-header</c> otherwise. The code block itself is the same either way, so
    /// styles and text selection that target it keep working. When <paramref name="border"/>
    /// holds the code's <see cref="TextBlock"/>, the code is first laid out as described in
    /// <see cref="LayOut"/>.
    /// </summary>
    public static void Write(AvaloniaRenderer renderer, Border border, CodeBlock block, string? language)
    {
        if (border.Child is TextBlock code)
            LayOut(renderer, border, code, block.Lines.Count);

        var showLanguage = renderer.ShowCodeBlockLanguage && !string.IsNullOrEmpty(language);
        var showCopyButton = renderer.ShowCodeBlockCopyButton;
        if (!showLanguage && !showCopyButton)
        {
            renderer.ApplyTheme(border, "MarkdownCodeBlockTheme");
            renderer.WriteBlock(border);
            return;
        }
        renderer.ApplyTheme(border, "MarkdownToolbarCodeBlockTheme");

        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        if (showLanguage)
        {
            var label = new TextBlock { Text = language, VerticalAlignment = VerticalAlignment.Center };
            label.Classes.Add("markdown-code-language");
            renderer.ApplyTheme(label, "MarkdownCodeLanguageTheme");
            toolbar.Children.Add(label);
        }
        if (showCopyButton)
        {
            var button = CreateCopyButton(() => GetCode(block), MarkdownCopyOrigin.CodeBlock);
            Grid.SetColumn(button, 1);
            toolbar.Children.Add(button);
        }

        var header = new Border { Child = toolbar };
        header.Classes.Add("markdown-code-header");
        renderer.ApplyTheme(header, "MarkdownCodeHeaderTheme");

        var container = new StackPanel();
        container.Classes.Add("markdown-code-container");
        renderer.ApplyTheme(container, "MarkdownCodeContainerTheme");
        container.Children.Add(header);
        container.Children.Add(border);

        renderer.WriteBlock(container);
    }

    /// <summary>
    /// Applies the renderer's code block options to <paramref name="code"/>, the only child of
    /// <paramref name="border"/>: wrapping its lines, putting it next to a
    /// <c>markdown-code-line-numbers</c> gutter in a two-column <see cref="Grid"/>, and capping
    /// the block's height with a vertical <c>markdown-code-scroll</c> <see cref="ScrollViewer"/>
    /// around the code. With none of them set, the block keeps the code as its only child.
    /// </summary>
    private static void LayOut(AvaloniaRenderer renderer, Border border, TextBlock code, int lineCount)
    {
        renderer.ApplyTheme(code, "MarkdownCodeTheme");
        var wrap = renderer.WrapCodeBlockLines;
        if (wrap)
            code.TextWrapping = TextWrapping.Wrap;

        Control content = code;
        if (renderer.ShowCodeBlockLineNumbers)
        {
            // The numbers are a text block of their own, which text selection never registers, so
            // they are neither highlighted nor copied with the code.
            var numbers = new TextBlock { Text = LineNumbers(lineCount), TextAlignment = TextAlignment.Right };
            numbers.Classes.Add("markdown-code-line-numbers");
            renderer.ApplyTheme(numbers, "MarkdownCodeLineNumbersTheme");
            // Nor are they read out by screen readers.
            AutomationRoles.Hide(numbers);
            // A wrapped line takes several rows of the code, and the gutter leaves all but its
            // first blank; which lines wrap is only known once the code is laid out at its width.
            if (wrap)
                code.SizeChanged += (_, _) => numbers.Text = LineNumbers(
                    code.TextLayout, MarkdownSelectableTextBlock.ExtractPlainText(code.Inlines!), lineCount);

            border.Child = null;
            Grid.SetColumn(code, 1);
            content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Children = { numbers, code },
            };
        }

        if (!double.IsPositiveInfinity(renderer.CodeBlockMaxHeight))
        {
            border.MaxHeight = renderer.CodeBlockMaxHeight;
            border.Child = null;
            // Horizontal scrolling stays with the document, as without a maximum height. Scroll
            // chaining (on by default) hands the mouse wheel back to the document once the block
            // has reached either end, so a long block never traps it.
            var scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = content,
            };
            scroll.Classes.Add("markdown-code-scroll");
            content = scroll;
        }

        border.Child = content;
    }

    /// <summary>Returns the numbers from 1 to <paramref name="lineCount"/>, one per line.</summary>
    internal static string LineNumbers(int lineCount)
    {
        var sb = new StringBuilder();
        for (int i = 1; i <= lineCount; i++)
        {
            if (i > 1) sb.Append('\n');
            sb.Append(i);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns one line per row of <paramref name="layout"/>, the layout of <paramref name="text"/>,
    /// holding the number of the code line that starts on that row, or nothing for the rows a
    /// wrapped line continues on.
    /// </summary>
    internal static string LineNumbers(TextLayout layout, string text, int lineCount)
    {
        var sb = new StringBuilder();
        var number = 0;
        var rows = layout.TextLines;
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            // A code line starts on the first row and after each line break, not after a wrap. The
            // rows' own NewLineLength can't tell: it stays 0 after a LineBreak inline.
            var start = rows[i].FirstTextSourceIndex;
            if ((i == 0 || text[start - 1] == '\n') && number < lineCount)
                sb.Append(++number);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns the <see cref="TextBlock"/> holding the code of a <c>markdown-code-block</c>
    /// border, whichever options it was laid out with, or <c>null</c> when it holds anything else.
    /// </summary>
    internal static TextBlock? FindCode(Border codeBlock) => FindCode(codeBlock.Child);

    private static TextBlock? FindCode(object? content) => content switch
    {
        TextBlock code => code,
        ScrollViewer scroll when scroll.Classes.Contains("markdown-code-scroll") => FindCode(scroll.Content),
        Grid { Children: [TextBlock numbers, TextBlock code] } when numbers.Classes.Contains("markdown-code-line-numbers") => code,
        _ => null,
    };

    /// <summary>
    /// Creates a <c>markdown-code-copy</c> button that copies the text <paramref name="text"/> returns
    /// when it is clicked, reported to the viewer's <see cref="MarkdownViewer.CopyingToClipboard"/>
    /// handlers as coming from <paramref name="origin"/>, and takes the <c>copied</c> class for a moment
    /// once it did.
    /// </summary>
    internal static Button CreateCopyButton(Func<string> text, MarkdownCopyOrigin origin)
    {
        // The icon's geometry, the tooltip and the accessible name come from the theme, so they
        // can be restyled or translated, and can change with the "copied" class.
        var button = new Button { Content = new CopyIcon() };
        button.Classes.Add("markdown-code-copy");

        IDisposable? resetTimer = null;
        button.Click += async (_, e) =>
        {
            e.Handled = true;
            try
            {
                // In a viewer, the copy goes through its CopyingToClipboard event.
                if (!await CopyAsync(button, origin, text()))
                    return;
            }
            catch (Exception)
            {
                // Clipboard access can be refused (e.g. by the browser); there is nothing to report
                // it to, so the button just shows no confirmation.
                return;
            }

            button.Classes.Add("copied");
            resetTimer?.Dispose();
            resetTimer = DispatcherTimer.RunOnce(() => button.Classes.Remove("copied"), CopiedFeedbackDuration);
        };
        return button;
    }

    /// <summary>
    /// Copies <paramref name="text"/> through the viewer <paramref name="button"/> is in, or straight to the
    /// clipboard when it is in none. Returns whether anything was written.
    /// </summary>
    private static async Task<bool> CopyAsync(Button button, MarkdownCopyOrigin origin, string text)
    {
        if (button.FindAncestorOfType<MarkdownViewer>() is { } viewer)
            return await viewer.CopyAsync(origin, text);

        if (TopLevel.GetTopLevel(button)?.Clipboard is not { } clipboard)
            return false;
        // Like a copied selection, the text gets the platform's line endings.
        await clipboard.SetTextAsync(text.ReplaceLineEndings());
        return true;
    }

    /// <summary>Returns the code of <paramref name="block"/>, one line per source line.</summary>
    internal static string GetCode(CodeBlock block)
    {
        var lines = block.Lines;
        if (lines.Lines == null)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(lines.Lines[i].Slice.AsSpan());
        }
        return sb.ToString();
    }
}
