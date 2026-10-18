// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Creates the control shown in place of a custom container (<c>:::name</c> … <c>:::</c>) whose name
/// was registered with a <see cref="ContainerRendererExtension"/>.
/// </summary>
/// <param name="content">
/// The container's blocks, already rendered as markdown. Place it inside the returned control (as an
/// expander's content, a card's body…): selection, find, links and the keyboard work in it as
/// anywhere else in the document.
/// </param>
/// <param name="arguments">
/// The text after the name on the opening line, trimmed (<c>Read this first</c> for
/// <c>:::card Read this first</c>), or an empty string. The default rendering shows it as a title.
/// </param>
/// <returns>
/// A new control holding <paramref name="content"/>, or <see langword="null"/> to render the container
/// as it would be without a renderer for its name.
/// </returns>
public delegate Control? ContainerRendererFactory(Control content, string arguments);
