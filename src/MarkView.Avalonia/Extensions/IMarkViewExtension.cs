// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Implemented by extension packages to plug into a renderer.
/// Called once per render pass, before <c>pipeline.Setup(renderer)</c>.
/// </summary>
/// <remarks>
/// The parameter type is the concrete <see cref="AvaloniaRenderer"/> by design:
/// extensions in this library are Avalonia-specific and require direct access to
/// renderer properties such as <see cref="AvaloniaRenderer.CodeHighlighter"/>,
/// <see cref="AvaloniaRenderer.ImageLoaders"/>, and
/// <see cref="AvaloniaRenderer.ReplaceOrAdd{TRenderer}"/>.
/// </remarks>
public interface IMarkViewExtension
{
    void Register(AvaloniaRenderer renderer);
}
