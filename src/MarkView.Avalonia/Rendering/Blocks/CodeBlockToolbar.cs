// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;

using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Threading;

using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Writes a code block, preceded by a toolbar with its language and a copy button when the
/// renderer enables them. Shared by every code block renderer, including those of the
/// extension packages.
/// </summary>
internal static class CodeBlockToolbar
{
    /// <summary>How long the copy button keeps its <c>copied</c> class after a copy.</summary>
    internal static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Writes <paramref name="border"/> (the <c>markdown-code-block</c>) as is when the toolbar
    /// would be empty, or inside a <c>markdown-code-container</c> panel below a
    /// <c>markdown-code-header</c> otherwise. The code block itself is unchanged either way, so
    /// styles and text selection that target it keep working.
    /// </summary>
    public static void Write(AvaloniaRenderer renderer, Border border, CodeBlock block, string? language)
    {
        var showLanguage = renderer.ShowCodeBlockLanguage && !string.IsNullOrEmpty(language);
        var showCopyButton = renderer.ShowCodeBlockCopyButton;
        if (!showLanguage && !showCopyButton)
        {
            renderer.WriteBlock(border);
            return;
        }

        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        if (showLanguage)
        {
            var label = new TextBlock { Text = language, VerticalAlignment = VerticalAlignment.Center };
            label.Classes.Add("markdown-code-language");
            toolbar.Children.Add(label);
        }
        if (showCopyButton)
        {
            var button = CreateCopyButton(GetCode(block));
            Grid.SetColumn(button, 1);
            toolbar.Children.Add(button);
        }

        var header = new Border { Child = toolbar };
        header.Classes.Add("markdown-code-header");

        var container = new StackPanel();
        container.Classes.Add("markdown-code-container");
        container.Children.Add(header);
        container.Children.Add(border);

        renderer.WriteBlock(container);
    }

    private static Button CreateCopyButton(string code)
    {
        // The icon's geometry, the tooltip and the accessible name come from the theme, so they
        // can be restyled or translated, and can change with the "copied" class.
        var button = new Button { Content = new PathIcon() };
        button.Classes.Add("markdown-code-copy");

        IDisposable? resetTimer = null;
        button.Click += async (_, e) =>
        {
            e.Handled = true;
            if (TopLevel.GetTopLevel(button)?.Clipboard is not { } clipboard)
                return;

            try
            {
                await clipboard.SetTextAsync(code);
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
