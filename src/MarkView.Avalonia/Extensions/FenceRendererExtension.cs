// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using MarkView.Avalonia.Rendering;

namespace MarkView.Avalonia.Extensions;

/// <summary>
/// Renders the fenced code blocks of one language (<c>```chart</c>) as the control a
/// <see cref="FenceRendererFactory"/> returns, without writing a Markdig renderer.
/// </summary>
/// <remarks>
/// <para>
/// The language is matched case-insensitively against the first word of the info string. A fence
/// renderer takes precedence over every other renderer of fenced code blocks (Mermaid diagrams,
/// syntax highlighting, the plain code block) whatever order the extensions were registered in.
/// When several fence renderers share a language, the last one registered wins, so a viewer's own
/// <see cref="MarkdownViewer.Extensions"/> override <see cref="MarkdownViewerDefaults.Extensions"/>.
/// </para>
/// <para>
/// The control is written inside a <c>Border</c> with the <c>markdown-custom-fence</c> and
/// <c>markdown-fence-&lt;language&gt;</c> classes (the language in lower case), which selection copies as
/// the fenced source unless the control has its own <see cref="MarkdownSelection.CopyTextProperty"/>.
/// When the factory returns <see langword="null"/> or throws, the block renders as it would without
/// this extension.
/// </para>
/// </remarks>
public sealed class FenceRendererExtension : IMarkViewExtension
{
    /// <summary>
    /// Creates a fence renderer for <paramref name="language"/>.
    /// </summary>
    /// <param name="language">The fence language, e.g. <c>chart</c>. Matched case-insensitively.</param>
    /// <param name="factory">Creates the control for each block of that language.</param>
    public FenceRendererExtension(string language, FenceRendererFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(factory);
        Language = language;
        Factory = factory;
    }

    /// <summary>The fence language this extension renders.</summary>
    public string Language { get; }

    /// <summary>Creates the control for each block of <see cref="Language"/>.</summary>
    public FenceRendererFactory Factory { get; }

    public void Register(AvaloniaRenderer renderer) => renderer.FenceRenderers[Language] = Factory;
}
