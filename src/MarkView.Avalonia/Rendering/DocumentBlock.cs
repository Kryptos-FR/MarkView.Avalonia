// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Associates a <see cref="TextBlock"/> in the rendered document with its extracted plain text.
/// Registered with <see cref="DocumentSelectionLayer"/> in document order.
/// </summary>
internal record DocumentBlock(TextBlock TextBlock, string PlainText);
