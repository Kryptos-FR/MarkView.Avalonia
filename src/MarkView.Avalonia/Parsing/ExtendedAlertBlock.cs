// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Extensions.Alerts;
using Markdig.Helpers;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// An <see cref="AlertBlock"/> whose first line carries Obsidian-style extras after <c>[!KIND]</c>:
/// a fold marker (<c>-</c> / <c>+</c>) and/or a custom title. Created by
/// <see cref="ExtendedAlertInlineParser"/>; a plain <c>[!KIND]</c> line stays a plain <see cref="AlertBlock"/>.
/// </summary>
internal sealed class ExtendedAlertBlock(StringSlice kind) : AlertBlock(kind)
{
    /// <summary>Whether the alert can be folded, and its initial state.</summary>
    public AlertFold Fold { get; init; }

    /// <summary>
    /// <c>true</c> when text follows the kind (and fold marker) on the first line. That text is left in
    /// place to be parsed as the inlines of the first paragraph, up to its first line break, so it keeps
    /// its emphasis, code spans and links; the renderer shows those inlines as the header.
    /// </summary>
    public bool HasTitle { get; init; }
}
