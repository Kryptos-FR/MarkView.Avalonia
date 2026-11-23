// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Markdig.Syntax.Inlines;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// GitHub's mark for an image made for one theme: an image whose address ends with <c>#gh-dark-mode-only</c>
/// shows only in a dark theme, one whose address ends with <c>#gh-light-mode-only</c> only in a light theme.
/// </summary>
/// <remarks>
/// Two marked images next to each other, one for each theme, make a pair: with nothing but spaces or a line
/// break between them, and either both on their own or each the only content of a link to the same address.
/// A pair shows as its first image, whose address follows the theme like that of an HTML <c>&lt;picture&gt;</c>,
/// so that it is copied, focused and saved as one image. The image of an HTML <c>&lt;picture&gt;</c> is never
/// marked: its sources choose its address.
/// </remarks>
internal static class ThemeImages
{
    private const string DarkOnly = "#gh-dark-mode-only";
    private const string LightOnly = "#gh-light-mode-only";

    /// <summary>
    /// The theme <paramref name="image"/> is marked for: <c>true</c> for the dark theme, <c>false</c> for the
    /// light theme, <c>null</c> when it is not marked.
    /// </summary>
    public static bool? MarkOf(LinkInline image) =>
        image.GetData(typeof(HtmlPicture)) is not null ? null
        : image.Url?.EndsWith(DarkOnly, StringComparison.Ordinal) == true ? true
        : image.Url?.EndsWith(LightOnly, StringComparison.Ordinal) == true ? false
        : null;

    /// <summary>The address of <paramref name="image"/> without its mark, which no loader needs to see.</summary>
    public static string Unmarked(LinkInline image)
    {
        var url = image.Url ?? string.Empty;
        return MarkOf(image) switch
        {
            true => url[..^DarkOnly.Length],
            false => url[..^LightOnly.Length],
            null => url,
        };
    }

    /// <summary>
    /// The second image of the pair <paramref name="image"/> starts, or <c>null</c> when it starts none.
    /// </summary>
    public static LinkInline? PartnerOf(LinkInline image)
    {
        if (MarkOf(image) is not { } dark || IsSecond(image))
            return null;
        var unit = Unit(image);
        return ImageOf(Next(unit)) is { } next && MarkOf(next) == !dark && SameKind(unit, Unit(next)) ? next : null;
    }

    /// <summary>Whether <paramref name="image"/> is the second image of a pair, shown by the first one.</summary>
    public static bool IsSecond(LinkInline image) =>
        MarkOf(image) is not null && ImageOf(Previous(Unit(image))) is { } previous && PartnerOf(previous) == image;

    // An image that is the only content of a link pairs with its link.
    private static Inline Unit(LinkInline image) =>
        image.Parent is LinkInline { IsImage: false } link && link.FirstChild == image && image.NextSibling is null
            ? link
            : image;

    private static LinkInline? ImageOf(Inline? unit) => unit switch
    {
        LinkInline { IsImage: true } image => image,
        LinkInline { IsImage: false, FirstChild: LinkInline { IsImage: true, NextSibling: null } image } => image,
        _ => null,
    };

    private static bool SameKind(Inline first, Inline second) => (first, second) switch
    {
        (LinkInline { IsImage: true }, LinkInline { IsImage: true }) => true,
        (LinkInline { IsImage: false } a, LinkInline { IsImage: false } b) => a.Url == b.Url,
        _ => false,
    };

    private static Inline? Next(Inline inline)
    {
        var next = inline.NextSibling;
        while (next is not null && IsSpace(next))
            next = next.NextSibling;
        return next;
    }

    private static Inline? Previous(Inline inline)
    {
        var previous = inline.PreviousSibling;
        while (previous is not null && IsSpace(previous))
            previous = previous.PreviousSibling;
        return previous;
    }

    private static bool IsSpace(Inline inline) =>
        inline is LineBreakInline { IsHard: false }
        || inline is LiteralInline literal && literal.Content.IsEmptyOrWhitespace();
}
