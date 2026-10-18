// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Gives the address a match of a <see cref="LinkPatternExtension"/>'s pattern links to.
/// </summary>
/// <param name="match">The match, groups included (<c>123</c> in <c>Groups[1]</c> for <c>#(\d+)</c> on <c>#123</c>).</param>
/// <returns>
/// The link's address, absolute or relative to the viewer's <see cref="MarkdownViewer.BaseUri"/>, or
/// <see langword="null"/> to keep the matched text as plain text.
/// </returns>
public delegate string? LinkPatternFactory(Match match);
