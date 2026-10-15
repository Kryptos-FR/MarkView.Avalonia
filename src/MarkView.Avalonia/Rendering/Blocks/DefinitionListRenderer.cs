// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Media;

using Markdig.Extensions.DefinitionLists;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// Renders a Markdig <see cref="DefinitionList"/> (Markdig's <c>UseDefinitionLists()</c>) as a
/// <c>StackPanel.markdown-definition-list</c>. Each term is a <c>markdown-definition-term</c> text block,
/// and each definition is an indented <c>StackPanel.markdown-definition</c> holding its blocks.
/// </summary>
public sealed class DefinitionListRenderer : AvaloniaObjectRenderer<DefinitionList>
{
    protected override void Write(AvaloniaRenderer renderer, DefinitionList obj)
    {
        var listPanel = new StackPanel { Spacing = 4 };
        listPanel.Classes.Add("markdown-definition-list");

        foreach (var item in obj.OfType<DefinitionItem>())
        {
            // An item holds its terms followed by the blocks of one definition; every further
            // ":" line of the same term starts a new item without terms.
            StackPanel? definition = null;
            foreach (var block in item)
            {
                if (block is DefinitionTerm term)
                {
                    var termBlock = new MarkdownSelectableTextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Renderer = renderer,
                    };
                    termBlock.Classes.Add("markdown-definition-term");
                    renderer.Push(termBlock.Inlines!);
                    renderer.WriteLeafInline(term);
                    renderer.Pop();
                    listPanel.Children.Add(termBlock);
                    definition = null;
                    continue;
                }

                if (definition is null)
                {
                    definition = new StackPanel { Spacing = 8 };
                    definition.Classes.Add("markdown-definition");
                    listPanel.Children.Add(definition);
                }

                renderer.Push(definition);
                renderer.Write(block);
                renderer.Pop();
            }
        }

        renderer.WriteBlock(listPanel);
    }
}
