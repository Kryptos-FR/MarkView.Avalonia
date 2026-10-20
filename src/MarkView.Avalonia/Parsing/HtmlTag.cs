// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Net;
using System.Text.RegularExpressions;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// A start or end tag of HTML, with its name in lower case and the attributes of the HTML subset it has
/// (see <see cref="MarkdownExtensions.UseHtmlSubset"/>): every other attribute is dropped.
/// </summary>
internal sealed partial class HtmlTag
{
    /// <summary>
    /// A start or end tag; quoted attribute values are consumed whole, so a <c>&gt;</c> inside one does not
    /// end the tag.
    /// </summary>
    internal const string Pattern =
        @"<(?<end>/)?(?<name>[a-zA-Z][a-zA-Z0-9-]*)(?<attributes>(?:\s+[^\s""'>/=]+(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'=<>`]+))?)*)\s*(?<self>/)?>";

    private static readonly string[] AllowedAttributes = ["href", "src", "alt", "title", "width", "height", "align", "dir"];

    [GeneratedRegex("^" + Pattern + "$", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"(?<name>[^\s""'>/=]+)(?:\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s""'=<>`]+)))?", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeRegex();

    private readonly Dictionary<string, string> _attributes;

    private HtmlTag(string name, bool isEnd, bool isSelfClosing, Dictionary<string, string> attributes)
    {
        Name = name;
        IsEnd = isEnd;
        IsSelfClosing = isSelfClosing;
        _attributes = attributes;
    }

    /// <summary>The tag name, in lower case.</summary>
    public string Name { get; }

    /// <summary><c>true</c> for an end tag (<c>&lt;/b&gt;</c>).</summary>
    public bool IsEnd { get; }

    /// <summary><c>true</c> for a tag written <c>&lt;name /&gt;</c>.</summary>
    public bool IsSelfClosing { get; }

    /// <summary>
    /// The value of the attribute <paramref name="name"/> (one of the subset's, in lower case), with its
    /// character references decoded, or <c>null</c> when the tag does not have it.
    /// </summary>
    public string? Attribute(string name) => _attributes.GetValueOrDefault(name);

    /// <summary>Parses a whole tag, such as the <c>Tag</c> of a Markdig <c>HtmlInline</c>, or returns <c>null</c>
    /// for anything else (a comment, a processing instruction…).</summary>
    public static HtmlTag? Parse(string tag) => TagRegex().Match(tag) is { Success: true } match ? FromMatch(match) : null;

    /// <summary>The tag a match of <see cref="Pattern"/> found.</summary>
    internal static HtmlTag FromMatch(Match match)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match attribute in AttributeRegex().Matches(match.Groups["attributes"].Value))
        {
            var name = attribute.Groups["name"].Value.ToLowerInvariant();
            // As in HTML, the first of two attributes with the same name wins.
            if (AllowedAttributes.Contains(name))
                attributes.TryAdd(name, WebUtility.HtmlDecode(attribute.Groups["value"].Value));
        }

        return new HtmlTag(
            match.Groups["name"].Value.ToLowerInvariant(),
            match.Groups["end"].Success,
            match.Groups["self"].Success,
            attributes);
    }
}
