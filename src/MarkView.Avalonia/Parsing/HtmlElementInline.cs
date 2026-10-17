// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Parsing;

/// <summary>The formatting an <see cref="HtmlElementInline"/> gives its content.</summary>
internal enum HtmlElementKind
{
    /// <summary><c>&lt;b&gt;</c>, <c>&lt;strong&gt;</c>.</summary>
    Bold,

    /// <summary><c>&lt;i&gt;</c>, <c>&lt;em&gt;</c>.</summary>
    Italic,

    /// <summary><c>&lt;del&gt;</c>, <c>&lt;s&gt;</c>, <c>&lt;strike&gt;</c>.</summary>
    Strikethrough,

    /// <summary><c>&lt;ins&gt;</c>, <c>&lt;u&gt;</c>.</summary>
    Underline,

    /// <summary><c>&lt;mark&gt;</c>.</summary>
    Mark,

    /// <summary><c>&lt;sub&gt;</c>.</summary>
    Subscript,

    /// <summary><c>&lt;sup&gt;</c>.</summary>
    Superscript,

    /// <summary><c>&lt;kbd&gt;</c>.</summary>
    Keyboard,
}

/// <summary>
/// An inline HTML formatting element, such as <c>&lt;b&gt;…&lt;/b&gt;</c> or <c>&lt;kbd&gt;…&lt;/kbd&gt;</c>:
/// the inlines between a start tag and its matching end tag. Created by <see cref="HtmlInlineBuilder"/>.
/// </summary>
internal sealed class HtmlElementInline(HtmlElementKind kind) : ContainerInline
{
    public HtmlElementKind Kind { get; } = kind;
}
