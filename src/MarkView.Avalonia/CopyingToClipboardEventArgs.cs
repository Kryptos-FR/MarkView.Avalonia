// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Interactivity;

namespace MarkView.Avalonia;

/// <summary>
/// Event args for <see cref="MarkdownViewer.CopyingToClipboard"/>, raised before anything is written to
/// the clipboard. A handler can change what is written, or cancel the copy.
/// </summary>
public sealed class CopyingToClipboardEventArgs(MarkdownCopyOrigin origin, string text, string? html = null) : RoutedEventArgs
{
    /// <summary>What is being copied.</summary>
    public MarkdownCopyOrigin Origin { get; } = origin;

    /// <summary>
    /// The text that will be written, with lines separated by <c>'\n'</c>. The viewer writes it with the
    /// platform's line endings (<c>"\r\n"</c> on Windows), whatever line endings a handler sets it with.
    /// Nothing is written when it is set to <c>null</c> or an empty string.
    /// </summary>
    public string Text
    {
        get;
        set => field = value ?? string.Empty;
    } = text;

    /// <summary>
    /// The HTML written next to <see cref="Text"/>, so that pasting into a word processor, a chat or a web editor
    /// keeps the formatting; <c>null</c> to write none. Set for <see cref="MarkdownCopyOrigin.Selection"/> while
    /// <see cref="MarkdownViewer.IsHtmlCopyEnabled"/> is <c>true</c> on a platform with an HTML clipboard format
    /// (Windows, macOS and Linux), <c>null</c> otherwise. Setting it where the platform has no HTML format writes
    /// the text alone.
    /// </summary>
    public string? Html { get; set; } = html;

    /// <summary>
    /// Set to <c>true</c> to write nothing, for instance when the handler writes the clipboard itself.
    /// A code block's copy button then shows no confirmation.
    /// </summary>
    public bool Cancel { get; set; }
}
