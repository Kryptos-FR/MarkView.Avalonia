// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

using Markdig.Syntax;

namespace MarkView.Avalonia.Rendering;

/// <summary>
/// A renderer of a container block that can stay the last block of a streamed document for a long time, such as a
/// language model's reasoning, and whose controls a streaming session keeps while it grows: the session writes the
/// block's shell once, then renders each update's new children into it, instead of rendering the whole block again.
/// </summary>
internal interface IGrowingBlockRenderer
{
    /// <summary>
    /// Writes the controls of <paramref name="block"/> without its children, and returns the panel its children go
    /// into, or <c>null</c> when the block renders nothing.
    /// </summary>
    Panel? WriteShell(AvaloniaRenderer renderer, ContainerBlock block);

    /// <summary>
    /// What the shell of <paramref name="block"/> shows, compared with <see cref="object.Equals(object, object)"/>:
    /// when it differs from the key the shell was written with, the block is rendered again.
    /// </summary>
    object? ShellKey(AvaloniaRenderer renderer, ContainerBlock block);
}
