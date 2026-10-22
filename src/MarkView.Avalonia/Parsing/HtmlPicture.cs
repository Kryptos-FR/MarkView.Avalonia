// Copyright (c) Nicolas Musset
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text.RegularExpressions;

namespace MarkView.Avalonia.Parsing;

/// <summary>
/// The <c>&lt;source&gt;</c> elements of an HTML <c>&lt;picture&gt;</c>, in order, stored as the data of the
/// image of its <c>&lt;img&gt;</c> (keyed by this type). As in a browser, the first source whose <c>media</c>
/// matches gives the address shown, and the <c>&lt;img&gt;</c> its own when none does; the only media matched
/// are <c>(prefers-color-scheme: light)</c> and <c>(prefers-color-scheme: dark)</c>, which follow the
/// application's theme, and a source without <c>media</c> matches both.
/// </summary>
internal sealed partial class HtmlPicture
{
    [GeneratedRegex(@"^\s*\(\s*prefers-color-scheme\s*:\s*(?<scheme>light|dark)\s*\)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ColorSchemeRegex();

    /// <param name="Light">Whether the source applies in a light theme.</param>
    /// <param name="Dark">Whether the source applies in a dark theme.</param>
    /// <param name="Url">The address of its first image candidate.</param>
    private readonly record struct Source(bool Light, bool Dark, string Url);

    private readonly List<Source> _sources = [];

    /// <summary>Whether any source applies in a light or a dark theme.</summary>
    public bool IsEmpty => _sources.Count == 0;

    /// <summary>
    /// Adds the source of a <c>&lt;source&gt;</c> tag with <paramref name="media"/> and <paramref name="srcset"/>
    /// attributes. One without an address, or with any other media (a width, a print query), never applies.
    /// </summary>
    public void Add(string? media, string? srcset)
    {
        if (FirstCandidate(srcset) is not { } url)
            return;
        if (string.IsNullOrWhiteSpace(media))
        {
            _sources.Add(new Source(Light: true, Dark: true, url));
        }
        else if (ColorSchemeRegex().Match(media) is { Success: true } match)
        {
            var dark = match.Groups["scheme"].Value.Equals("dark", StringComparison.OrdinalIgnoreCase);
            _sources.Add(new Source(Light: !dark, Dark: dark, url));
        }
    }

    /// <summary>The address of the first source that applies in a dark or light theme, or <c>null</c> when none does.</summary>
    public string? UrlFor(bool dark)
    {
        foreach (var source in _sources)
        {
            if (dark ? source.Dark : source.Light)
                return source.Url;
        }
        return null;
    }

    /// <summary>
    /// The address of the first image candidate of a <c>srcset</c> (<c>logo.png 1x, logo@2x.png 2x</c>), or
    /// <c>null</c> without one.
    /// </summary>
    private static string? FirstCandidate(string? srcset)
    {
        if (srcset is null)
            return null;
        var candidate = srcset.AsSpan().Trim();
        var comma = candidate.IndexOf(',');
        if (comma >= 0)
            candidate = candidate[..comma].Trim();
        var space = candidate.IndexOfAny(' ', '\t', '\n');
        if (space >= 0)
            candidate = candidate[..space];
        return candidate.IsEmpty ? null : candidate.ToString();
    }
}
