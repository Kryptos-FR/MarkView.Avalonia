// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Media;

using Markdig.Syntax;

using MarkView.Avalonia.Extensions;

namespace MarkView.Avalonia.Rendering.Blocks;

/// <summary>
/// The code block that ends the text of a streaming session, written so that each update lays out only the lines
/// that changed. A text block lays its whole text out again whenever any of it changes, which grows with the block
/// (about 45 ms for 11,000 characters), so the code is split into chunks of <see cref="ChunkLines"/> lines, each in a
/// text block of its own, stacked in a <c>markdown-code-chunks</c> panel inside the usual border and toolbar. An
/// update keeps the chunks whose lines are complete and unchanged and writes the rest again, highlighted from the
/// state the last kept chunk ended in.
/// </summary>
/// <remarks>
/// Only the session's updates write this form: once the block no longer ends the text, or the session completes, the
/// block is rendered again as a single text block, so the finished document is the same as without streaming. The
/// chunks are highlighted at once, even with lazy rendering, since a chunk goes on from the state the one before it
/// ended in, and an update highlights at most the chunks it writes. Text selection registers one entry per chunk,
/// joined with a line break, so the chunks select and copy as the lines they hold.
/// </remarks>
internal sealed class GrowingCodeBlock
{
    /// <summary>How many lines each chunk holds, the last one excepted.</summary>
    internal const int ChunkLines = 32;

    /// <summary>The class of the panel that stacks the chunks.</summary>
    internal const string ChunksClass = "markdown-code-chunks";

    private readonly AvaloniaRenderer _renderer;
    private readonly ICodeHighlighter? _highlighter;
    private readonly string? _language;
    private readonly StackPanel _chunks;
    private readonly List<Chunk> _written = [];

    private GrowingCodeBlock(AvaloniaRenderer renderer, CodeBlock block, ICodeHighlighter? highlighter, string? language, StackPanel chunks)
    {
        _renderer = renderer;
        Block = block;
        _highlighter = highlighter;
        _language = language;
        _chunks = chunks;
    }

    /// <summary>The block as of the last update.</summary>
    public CodeBlock Block { get; private set; }

    /// <summary>
    /// Writes <paramref name="block"/> as a growing code block, its lines highlighted by <paramref name="highlighter"/>,
    /// when it is the renderer's <see cref="AvaloniaRenderer.GrowingCodeTarget"/>, and records it as the renderer's
    /// <see cref="AvaloniaRenderer.WrittenGrowingCode"/>. Called by the code block renderers before they write a block
    /// their usual way; returns <c>false</c>, having written nothing, for any other block.
    /// </summary>
    public static bool TryWrite(AvaloniaRenderer renderer, CodeBlock block, ICodeHighlighter? highlighter)
    {
        if (!ReferenceEquals(renderer.GrowingCodeTarget, block))
            return false;

        var language = (block as FencedCodeBlock)?.Info;
        var border = new Border();
        border.Classes.Add("markdown-code-block");
        if (!string.IsNullOrEmpty(language))
            border.Classes.Add($"language-{language}");

        var chunks = new StackPanel();
        chunks.Classes.Add(ChunksClass);
        if (renderer.ShowCodeBlockLineNumbers)
            Grid.SetIsSharedSizeScope(chunks, true);

        var code = new GrowingCodeBlock(renderer, block, highlighter, language, chunks);
        code.WriteChunks(0, HighlightedCode.Lines(block));

        border.Child = CodeBlockToolbar.CapHeight(renderer, border, chunks);
        CodeBlockToolbar.Write(renderer, border, block, language, () => CodeBlockToolbar.GetCode(code.Block));
        renderer.WrittenGrowingCode = code;
        return true;
    }

    /// <summary>
    /// Whether <paramref name="block"/>, the block this one was written for as the text grew, can take its place: same
    /// kind of block, in the same language. The caller has checked it starts at the same place.
    /// </summary>
    public bool CanGrow(CodeBlock block) =>
        block.GetType() == Block.GetType() && (block as FencedCodeBlock)?.Info == _language;

    /// <summary>
    /// Brings the chunks up to date with <paramref name="block"/>: keeps those whose lines are all complete (a line
    /// followed them when they were written) and unchanged, and writes the others again.
    /// </summary>
    public void Grow(CodeBlock block)
    {
        var lines = HighlightedCode.Lines(block);
        var keep = 0;
        while (keep < _written.Count && _written[keep].IsComplete && _written[keep].Matches(lines))
            keep++;

        _chunks.Children.RemoveRange(keep, _chunks.Children.Count - keep);
        _written.RemoveRange(keep, _written.Count - keep);
        Block = block;
        WriteChunks(keep, lines);
    }

    private void WriteChunks(int start, List<ReadOnlyMemory<char>> lines)
    {
        var state = start > 0 ? _written[start - 1].EndState : null;
        for (var first = start * ChunkLines; first < lines.Count; first += ChunkLines)
        {
            var chunkLines = lines.GetRange(first, Math.Min(ChunkLines, lines.Count - first));
            var text = new TextBlock { TextWrapping = TextWrapping.NoWrap };
            HighlightedCode.WriteLines(text, _highlighter, _language, chunkLines, ref state);
            _chunks.Children.Add(CodeBlockToolbar.LayOutLines(_renderer, text, first + 1, chunkLines.Count, sharedGutter: true));
            _written.Add(new Chunk(first, chunkLines, IsComplete: first + chunkLines.Count < lines.Count, state));
        }
    }

    /// <param name="First">The index of its first line in the block.</param>
    /// <param name="Lines">Its lines, as written.</param>
    /// <param name="IsComplete">Whether a line followed its last one when it was written, so none of its lines can still grow.</param>
    /// <param name="EndState">The highlighter's state after its last line.</param>
    private sealed record Chunk(int First, List<ReadOnlyMemory<char>> Lines, bool IsComplete, object? EndState)
    {
        public bool Matches(List<ReadOnlyMemory<char>> lines)
        {
            if (First + Lines.Count > lines.Count)
                return false;
            for (var i = 0; i < Lines.Count; i++)
            {
                if (!Lines[i].Span.SequenceEqual(lines[First + i].Span))
                    return false;
            }
            return true;
        }
    }
}
