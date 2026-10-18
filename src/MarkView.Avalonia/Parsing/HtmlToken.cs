// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Net;
using System.Text.RegularExpressions;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// A tag, or the text between two tags, of a piece of HTML, as <see cref="Tokenize"/> splits it. Tolerant:
/// anything that is not a well-formed tag or comment is text.
/// </summary>
/// <param name="Raw">The source of the tag or text.</param>
/// <param name="Tag">The tag, or <c>null</c> for text.</param>
internal readonly partial record struct HtmlToken(string Raw, HtmlTag? Tag)
{
    [GeneratedRegex("<!--.*?-->|" + HtmlTag.Pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex MarkupRegex();

    [GeneratedRegex(@"[ \t\r\n\f]+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// The text a text token shows: runs of whitespace collapsed to a space, as HTML does, and character
    /// references decoded.
    /// </summary>
    public string Text => WebUtility.HtmlDecode(WhitespaceRegex().Replace(Raw, " "));

    /// <summary>Splits <paramref name="html"/> into tags and text. Comments are left out.</summary>
    public static List<HtmlToken> Tokenize(string html)
    {
        var tokens = new List<HtmlToken>();
        var position = 0;
        foreach (Match match in MarkupRegex().Matches(html))
        {
            // Stryker disable once Equality : an empty text token shows nothing
            if (match.Index > position)
                tokens.Add(new HtmlToken(html[position..match.Index], null));
            position = match.Index + match.Length;

            if (match.Groups["name"].Success)
                tokens.Add(new HtmlToken(match.Value, HtmlTag.FromMatch(match)));
        }

        // Stryker disable once Equality : an empty text token shows nothing
        if (position < html.Length)
            tokens.Add(new HtmlToken(html[position..], null));
        return tokens;
    }
}
