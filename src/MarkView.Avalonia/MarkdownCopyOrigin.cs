// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia;

/// <summary>What a copy raising <see cref="MarkdownViewer.CopyingToClipboard"/> copies.</summary>
public enum MarkdownCopyOrigin
{
    /// <summary>The selected text, copied with the copy hotkey or <see cref="MarkdownViewer.CopyToClipboardAsync"/>.</summary>
    Selection,

    /// <summary>The selection as Markdown, copied with <see cref="MarkdownViewer.CopyAsMarkdownAsync"/>.</summary>
    SelectionAsMarkdown,

    /// <summary>The code of a code block, copied with its copy button.</summary>
    CodeBlock,

    /// <summary>The TeX source of a display formula (<c>MarkView.Avalonia.Math</c>), copied with its copy button.</summary>
    MathBlock,

    /// <summary>The address of a link, copied with the context menu's Copy link.</summary>
    Link,

    /// <summary>
    /// A whole table as tab-separated values, with an HTML table next to them, copied with the context menu's
    /// Copy table.
    /// </summary>
    Table,

    /// <summary>A whole table as Markdown, copied with the context menu's Copy table as Markdown.</summary>
    TableAsMarkdown,
}
