// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Extensions.Yaml;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Suppresses rendering of Markdig <see cref="YamlFrontMatterBlock"/>. Must be registered before
/// <see cref="CodeBlockRenderer"/>: <see cref="YamlFrontMatterBlock"/> extends
/// <see cref="Markdig.Syntax.CodeBlock"/>, so without this renderer the raw YAML would render as
/// a visible code block.
/// </summary>
public sealed class YamlFrontMatterBlockRenderer : AvaloniaObjectRenderer<YamlFrontMatterBlock>
{
    protected override void Write(AvaloniaRenderer renderer, YamlFrontMatterBlock obj)
    {
        // Intentionally no-op: front matter is document metadata, not visible content.
    }
}
