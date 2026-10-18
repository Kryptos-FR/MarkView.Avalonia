// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Renders the custom containers of one name (<c>:::card</c> … <c>:::</c>) as the control a
/// <see cref="ContainerRendererFactory"/> builds around their rendered blocks, without writing a
/// Markdig renderer. Custom containers need Markdig's <c>UseCustomContainers()</c> in the pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The name is matched case-insensitively. When several container renderers share a name, the last
/// one registered wins, so a viewer's own <see cref="MarkdownViewer.Extensions"/> override
/// <see cref="MarkdownViewerDefaults.Extensions"/>.
/// </para>
/// <para>
/// The control is written inside a <c>Border</c> with the <c>markdown-container-control</c> and
/// <c>markdown-container-&lt;name&gt;</c> classes (the name in lower case). Selection, find and the
/// keyboard reach the rendered blocks inside it, and the keyboard also reaches the control's own
/// focusable parts (an expander's header). When the factory returns <see langword="null"/> or throws,
/// the container renders as it would without this extension.
/// </para>
/// </remarks>
public sealed class ContainerRendererExtension : IMarkViewExtension
{
    /// <summary>
    /// Creates a container renderer for <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The container name, e.g. <c>card</c> for <c>:::card</c>. Matched case-insensitively.</param>
    /// <param name="factory">Creates the control for each container of that name.</param>
    public ContainerRendererExtension(string name, ContainerRendererFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);
        Name = name;
        Factory = factory;
    }

    /// <summary>The container name this extension renders.</summary>
    public string Name { get; }

    /// <summary>Creates the control for each container named <see cref="Name"/>.</summary>
    public ContainerRendererFactory Factory { get; }

    public void Register(AvaloniaRenderer renderer) => renderer.ContainerRenderers[Name] = Factory;
}
