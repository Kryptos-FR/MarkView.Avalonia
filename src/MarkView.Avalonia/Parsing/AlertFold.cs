// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Whether an alert can be folded, and its initial state (Obsidian-style <c>[!KIND]-</c> / <c>[!KIND]+</c>).
/// </summary>
internal enum AlertFold
{
    /// <summary>Plain alert (<c>[!KIND]</c>): always shows its body.</summary>
    None,

    /// <summary><c>[!KIND]-</c>: foldable, starts collapsed.</summary>
    Collapsed,

    /// <summary><c>[!KIND]+</c>: foldable, starts expanded.</summary>
    Expanded,
}
