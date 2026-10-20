// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls.Documents;

namespace MarkView.Avalonia.Rendering.Inlines;

/// <summary>
/// An invisible directional isolate character that the renderer adds around left-to-right text inside
/// right-to-left text: U+2066 LEFT-TO-RIGHT ISOLATE before it, U+2069 POP DIRECTIONAL ISOLATE after it. The
/// Bidirectional Algorithm then lays the text out left to right in one piece (without them, <c>foo()</c> in a
/// Hebrew sentence shows as <c>()foo</c>, its brackets taking the direction of the sentence), and the sentence
/// around it treats it as a single neutral character, so a number after it keeps the sentence's order. The
/// characters are not part of the document's text: copying a selection leaves them out.
/// </summary>
internal sealed class DirectionIsolate : Run
{
    private DirectionIsolate(string text) : base(text)
    {
    }

    /// <summary>A new U+2066 LEFT-TO-RIGHT ISOLATE, which opens an isolate.</summary>
    public static DirectionIsolate LeftToRight() => new("\u2066");

    /// <summary>A new U+2069 POP DIRECTIONAL ISOLATE, which closes the isolate opened before it.</summary>
    public static DirectionIsolate Pop() => new("\u2069");
}
