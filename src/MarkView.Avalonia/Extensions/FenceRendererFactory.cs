// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Creates the control shown in place of a fenced code block whose language was registered with a
/// <see cref="FenceRendererExtension"/>.
/// </summary>
/// <param name="code">The block's content, one line per source line joined with <c>\n</c>, without the fences.</param>
/// <param name="info">
/// The whole info string after the opening fence: the language, then any arguments after it
/// (<c>chart {height=200}</c> for <c>```chart {height=200}</c>).
/// </param>
/// <returns>
/// A new control for this block, or <see langword="null"/> to render the block as code instead.
/// </returns>
public delegate Control? FenceRendererFactory(string code, string info);
