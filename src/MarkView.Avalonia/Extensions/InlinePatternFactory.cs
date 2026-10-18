// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

using Avalonia.Controls.Documents;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Creates the inline shown in place of a match of an <see cref="InlinePatternExtension"/>'s pattern
/// in the document's text.
/// </summary>
/// <param name="match">The match, groups included (<c>bob</c> in <c>Groups[1]</c> for <c>@(\w+)</c> on <c>@bob</c>).</param>
/// <returns>
/// A new inline for this match (a <see cref="Run"/>, a <see cref="Span"/>, an
/// <see cref="InlineUIContainer"/> around a control…), or <see langword="null"/> to keep the matched
/// text as it is.
/// </returns>
public delegate Inline? InlinePatternFactory(Match match);
