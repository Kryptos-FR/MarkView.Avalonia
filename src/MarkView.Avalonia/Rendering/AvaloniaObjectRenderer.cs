// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Renderers;
using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// Base class for all Avalonia markdown object renderers.
/// </summary>
/// <typeparam name="TObject">The type of Markdig AST node this renderer handles.</typeparam>
public abstract class AvaloniaObjectRenderer<TObject> : MarkdownObjectRenderer<AvaloniaRenderer, TObject>
    where TObject : MarkdownObject
{
}
