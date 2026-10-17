// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia;

/// <summary>
/// Controls when <see cref="MarkdownViewer"/> switches to its high-contrast palette.
/// </summary>
public enum HighContrastMode
{
    /// <summary>
    /// Follow the operating system's high-contrast setting. Default. Avalonia reports it on
    /// Windows, macOS ("Increase contrast") and in the browser (<c>prefers-contrast: more</c>),
    /// but not on Linux.
    /// </summary>
    System,

    /// <summary>Always use the high-contrast palette.</summary>
    Enabled,

    /// <summary>Never use the high-contrast palette.</summary>
    Disabled,
}
