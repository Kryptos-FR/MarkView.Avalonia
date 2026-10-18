// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Frozen;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// The names of the elements of HTML, obsolete ones included, and of MathML. A tag with another name, such as
/// the <c>&lt;string&gt;</c> of <c>List&lt;string&gt;</c> or a <c>&lt;T&gt;</c>, is not HTML anyone meant, and the
/// HTML subset shows it as text (see <see cref="HtmlInlineBuilder"/>). The elements inside an <c>&lt;svg&gt;</c>
/// need no name here: its content is dropped.
/// </summary>
internal static class HtmlElementNames
{
    // Stryker disable String : the names are data, each one only deciding whether its own tags show as text
    private static readonly FrozenSet<string> Names = new[]
    {
        // HTML
        "a", "abbr", "address", "area", "article", "aside", "audio", "b", "base", "bdi", "bdo", "blockquote", "body",
        "br", "button", "canvas", "caption", "cite", "code", "col", "colgroup", "data", "datalist", "dd", "del",
        "details", "dfn", "dialog", "div", "dl", "dt", "em", "embed", "fieldset", "figcaption", "figure", "footer",
        "form", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hgroup", "hr", "html", "i", "iframe", "img",
        "input", "ins", "kbd", "label", "legend", "li", "link", "main", "map", "mark", "menu", "meta", "meter", "nav",
        "noscript", "object", "ol", "optgroup", "option", "output", "p", "param", "picture", "pre", "progress", "q",
        "rp", "rt", "ruby", "s", "samp", "script", "search", "section", "select", "selectedcontent", "slot", "small",
        "source", "span", "strong", "style", "sub", "summary", "sup", "svg", "table", "tbody", "td", "template",
        "textarea", "tfoot", "th", "thead", "time", "title", "tr", "track", "u", "ul", "var", "video", "wbr",

        // Obsolete HTML, which browsers still parse
        "acronym", "applet", "basefont", "bgsound", "big", "blink", "center", "dir", "font", "frame", "frameset",
        "image", "isindex", "keygen", "listing", "marquee", "menuitem", "multicol", "nextid", "nobr", "noembed",
        "noframes", "plaintext", "rb", "rtc", "spacer", "strike", "tt", "xmp",

        // MathML
        "math", "annotation", "maction", "menclose", "merror", "mfenced", "mfrac", "mi", "mmultiscripts", "mn", "mo",
        "mover", "mpadded", "mphantom", "mprescripts", "mroot", "mrow", "ms", "mspace", "msqrt", "mstyle", "msub",
        "msubsup", "msup", "mtable", "mtd", "mtext", "mtr", "munder", "munderover", "none", "semantics",
    }.ToFrozenSet(StringComparer.Ordinal);
    // Stryker restore String

    /// <summary>
    /// Whether <paramref name="name"/>, in lower case, is the name of an element: one of HTML or MathML, or a
    /// custom element, whose name has a hyphen.
    /// </summary>
    public static bool Contains(string name) => Names.Contains(name) || name.Contains('-');
}
