// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// Parses GitHub's strikethrough: <c>~~text~~</c> and <c>~text~</c>. Markdig's <c>UseEmphasisExtras()</c>
/// makes a single tilde subscript; this makes it strikethrough instead, even when the emphasis extras
/// are also enabled. Added by <see cref="MarkdownExtensions.UseGitHubExtensions"/>.
/// </summary>
/// <remarks>
/// It does not use Markdig's emphasis extras extension itself, so <c>UseEmphasisExtras()</c> can still be
/// added after it for superscript, inserted and marked text.
/// </remarks>
internal sealed class GitHubStrikethroughExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        var parser = pipeline.InlineParsers.FindExact<EmphasisInlineParser>();
        if (parser is null)
            return;

        if (!parser.EmphasisDescriptors.Any(d => d.Character == '~'))
            parser.EmphasisDescriptors.Add(new EmphasisDescriptor('~', 1, 2, enableWithinWord: true));
        parser.TryCreateEmphasisInlineList.Add(CreateStrikethrough);
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }

    // A strikethrough is a '~' emphasis with two delimiters; renderers tell it from a subscript by that count.
    private static EmphasisInline? CreateStrikethrough(char emphasisChar, int delimiterCount) =>
        emphasisChar == '~' && delimiterCount == 1
            ? new EmphasisInline { DelimiterChar = '~', DelimiterCount = 2 }
            : null;
}
