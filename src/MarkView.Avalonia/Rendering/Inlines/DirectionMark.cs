// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// An invisible U+200E LEFT-TO-RIGHT MARK that the renderer adds on each side of left-to-right text inside
/// right-to-left text, so the Bidirectional Algorithm keeps the text in one piece: without them, <c>foo()</c>
/// in a Hebrew sentence shows as <c>()foo</c>, its brackets taking the direction of the sentence. The marks
/// are not part of the document's text: copying a selection leaves them out.
/// </summary>
/// <remarks>
/// A directional isolate (U+2066…U+2069) would be the textbook choice, but Avalonia 12.1 lays out the
/// neutral characters inside one as if it were not there; the marks give the same result for code.
/// </remarks>
internal sealed class DirectionMark : Run
{
    private DirectionMark() : base("\u200E")
    {
    }

    /// <summary>A new U+200E LEFT-TO-RIGHT MARK.</summary>
    public static DirectionMark LeftToRight() => new();
}
